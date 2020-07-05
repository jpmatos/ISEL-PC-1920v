using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AsyncServerClient.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsyncServerClient
{
    public class TcpMultiThreadedJsonEchoServer
    {
        private const int SERVER_PORT = 13000;
        private const int MAX_SIMULTANEOUS_CONNECTIONS = 20;
        private const int WAIT_FOR_IDLE_TIME = 10000;
        private const int POLLING_INTERVAL = WAIT_FOR_IDLE_TIME / 20;
        private static Logger logger;
        private TcpListener server;
        private volatile int requestCount = 0;
        private Dictionary<string, TransferQueueAsync<JObject>> queues;
        private static readonly JsonSerializer serializer = new JsonSerializer();

        private TcpMultiThreadedJsonEchoServer()
        {
            logger = new Logger(5);
            queues = new Dictionary<string, TransferQueueAsync<JObject>>();
            server = new TcpListener(IPAddress.Loopback, SERVER_PORT);
            server.Start();
        }

        //Entry Point
        public static async Task LaunchServer()
        {
            TcpMultiThreadedJsonEchoServer echoEchoServer = new TcpMultiThreadedJsonEchoServer();
            logger.Init();
            
            CancellationTokenSource cts = new CancellationTokenSource();
            Task listenTask = echoEchoServer.ListenAsync(cts.Token);

            logger.Log("--Hit <enter> to exit the server...");
            await Console.In.ReadLineAsync();

            await echoEchoServer.ShutdownAndWaitTerminationAsync(listenTask, cts);

            logger.Log($"--{echoEchoServer.requestCount} requests were processed");

            await logger.FinishLogs();
        }

        //Listen for connection
        private async Task ListenAsync(CancellationToken cToken)
        {
            var startedTasks = new HashSet<Task>();
            while (!cToken.IsCancellationRequested)
            {
                try
                {
                    var connection = await server.AcceptTcpClientAsync();
                    startedTasks.Add(ServeConnectionAsync(connection, cToken));
                    if (startedTasks.Count >= MAX_SIMULTANEOUS_CONNECTIONS)
                    {
                        if (startedTasks.RemoveWhere(task => task.IsCompleted) == 0)
                            startedTasks.Remove(await Task.WhenAny(startedTasks));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Benign exception - occurs when when stop accepting connections
                }
                catch (Exception ex)
                {
                    logger.Log($"***{ex.GetType().Name}: {ex.Message}");
                }
            }

            if (startedTasks.Count > 0)
                await Task.WhenAll(startedTasks);
        }

        //Start Shutdown
        private async Task ShutdownAndWaitTerminationAsync(Task listenTask, CancellationTokenSource cts)
        {
            for (int i = 0; i < WAIT_FOR_IDLE_TIME; i += POLLING_INTERVAL)
            {
                if (!server.Pending())
                    break;
                await Task.Delay(POLLING_INTERVAL);
            }

            server.Stop();
            cts.Cancel();
            logger.Log("Shutting down server...");

            await listenTask;
        }

        //Server Connection
        private async Task ServeConnectionAsync(TcpClient connection,
            CancellationToken cToken = default(CancellationToken))
        {
            using (connection)
            {
                var stream = connection.GetStream();
                var reader = new JsonTextReader(new StreamReader(stream))
                {
                    SupportMultipleContent = true
                };
                var writer = new JsonTextWriter(new StreamWriter(stream));
                try
                {
                    // Consume any bytes until start of object character ('{')
                    do
                    {
                        await reader.ReadAsync();
                    } while (reader.TokenType != JsonToken.StartObject &&
                             reader.TokenType != JsonToken.None);

                    if (reader.TokenType == JsonToken.None)
                    {
                        logger.Log($"[{requestCount}] reached end of input stream, ending.");
                        return;
                    }

                    // Load root JSON object
                    JObject json = await JObject.LoadAsync(reader);
                    Request request = json.ToObject<Request>();

                    Response response;
                    if (!cToken.IsCancellationRequested)
                        switch (request.Method)
                        {
                            case "CREATE":
                                response = CreateHandler(request.Path, cToken);
                                break;
                            case "PUT":
                                response = PutHandler(request.Path, request.Payload);
                                break;
                            case "TRANSFER":
                                response = await TransferHandler(request.Path, request.Payload,
                                    request.Headers.GetValueOrDefault("timeout"));
                                break;
                            case "TAKE":
                                response = await TakeHandler(request.Path,
                                    request.Headers.GetValueOrDefault("timeout"));
                                break;
                            default:
                                response = new Response
                                {
                                    Status = 405
                                };
                                break;
                        }
                    else
                        response = new Response
                        {
                            Status = 503
                        };

                    serializer.Serialize(writer, response);
                    await writer.FlushAsync();

                    Interlocked.Increment(ref requestCount);
                }
                catch (JsonReaderException e)
                {
                    logger.Log($"[{requestCount}] Error reading JSON: {e.Message}, continuing");
                    Response response = new Response
                    {
                        Status = 400
                    };
                    serializer.Serialize(writer, response);
                    await writer.FlushAsync();
                }
                catch (Exception e)
                {
                    logger.Log($"[{requestCount}] Unexpected exception, closing connection {e.Message}");
                    Response response = new Response
                    {
                        Status = 500
                    };
                    serializer.Serialize(writer, response);
                    await writer.FlushAsync();
                }
            }
        }

        private Response PutHandler(string requestPath, JObject requestPayload)
        {
            if (!queues.TryGetValue(requestPath, out TransferQueueAsync<JObject> queue))
            {
                logger.Log($"[{requestCount}] Put NoQueue '{requestPath}'");
                return new Response
                {
                    Status = 404
                };
            }

            queue.Put(requestPayload);
            logger.Log($"[{requestCount}] Put Success");

            return new Response
            {
                Status = 200
            };
        }

        private Response CreateHandler(string requestPath, CancellationToken cToken)
        {
            if (!queues.ContainsKey(requestPath))
            {
                queues.Add(requestPath, new TransferQueueAsync<JObject>(cToken));
                logger.Log($"[{requestCount}] Created list {requestPath}");
                logger.Log($"[{requestCount}] Created list {requestPath}");
            }
            else
            {
                logger.Log($"[{requestCount}] List {requestPath} exists");
            }

            return new Response
            {
                Status = 200
            };
        }

        private async Task<Response> TransferHandler(string requestPath, JObject requestPayload, string timeoutStr)
        {
            if (!queues.TryGetValue(requestPath, out TransferQueueAsync<JObject> queue))
            {
                logger.Log($"[{requestCount}] Transfer NoQueue '{requestPath}'");
                return new Response
                {
                    Status = 404
                };
            }

            int timeout = Int32.Parse(timeoutStr);
            Task<bool> task = queue.Transfer(requestPayload, timeout);
            logger.Log($"[{requestCount}] Transfer Task Created");
            try
            {
                bool result = await task;
                if (result)
                {
                    logger.Log($"[{requestCount}] Transfer Success");
                    return new Response
                    {
                        Status = 200
                    };
                }

                //timeout
                logger.Log($"[{requestCount}] Transfer Timeout");
                return new Response
                {
                    Status = 204
                };
            }
            catch (TaskCanceledException)
            {
                //cancelled
                logger.Log($"[{requestCount}] Transfer Cancelled");
                return new Response
                {
                    Status = 204
                };
            }
        }

        private async Task<Response> TakeHandler(string requestPath, string timeoutStr)
        {
            if (!queues.TryGetValue(requestPath, out TransferQueueAsync<JObject> queue))
            {
                logger.Log($"[{requestCount}] Take NoQueue '{requestPath}'");
                return new Response
                {
                    Status = 404
                };
            }

            int timeout = Int32.Parse(timeoutStr);
            Task<JObject> task = queue.Take(timeout);
            logger.Log($"[{requestCount}] Take Task Created");
            try
            {
                JObject result = await task;
                if (result != null)
                {
                    logger.Log($"[{requestCount}] Take Success");
                    return new Response
                    {
                        Status = 200,
                        Payload = result
                    };
                }

                //timeout
                logger.Log($"[{requestCount}] Take Timeout");
                return new Response
                {
                    Status = 204
                };
            }
            catch (TaskCanceledException)
            {
                //cancelled
                logger.Log($"[{requestCount}] Take Cancelled");
                return new Response
                {
                    Status = 204
                };
            }
        }
    }
}