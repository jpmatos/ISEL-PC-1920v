using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsyncServerClient.MIX
{
    public class TcpMultiThreadedJsonEchoServer
    {
        private const int SERVER_PORT = 13000;
        private const int MIN_SERVICE_TIME = 50;
        private const int MAX_SERVICE_TIME = 500;
        private const int MAX_SIMULTANEOUS_CONNECTIONS = 20;
        private const int WAIT_FOR_IDLE_TIME = 10000;
        private const int POLLING_INTERVAL = WAIT_FOR_IDLE_TIME / 20;
        private TcpListener server;
        private volatile int requestCount = 0;
        private Dictionary<string, TransferQueueAsync<JObject>> queues;
        private static readonly JsonSerializer serializer = new JsonSerializer();

        private ThreadLocal<Random> random =
            new ThreadLocal<Random>(() => new Random(Thread.CurrentThread.ManagedThreadId));


        public TcpMultiThreadedJsonEchoServer()
        {
            queues = new Dictionary<string, TransferQueueAsync<JObject>>();
            server = new TcpListener(IPAddress.Loopback, SERVER_PORT);
            server.Start();
        }

        //Entry Point
        public static async Task LaunchServer()
        {
            TcpMultiThreadedJsonEchoServer echoEchoServer = new TcpMultiThreadedJsonEchoServer();
            CancellationTokenSource cts = new CancellationTokenSource();
            Task listenTask = echoEchoServer.ListenAsync(cts.Token);

            Console.WriteLine("--Hit <enter> to exit the server...");
            await Console.In.ReadLineAsync();

            await echoEchoServer.ShutdownAndWaitTerminationAsync(listenTask, cts);

            Console.WriteLine($"--{echoEchoServer.requestCount} requests were processed");
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
                    Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
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
            Console.WriteLine("Shutting down server...");

            await listenTask;
        }

        //Server Connection
        private async Task ServeConnectionAsync(TcpClient connection, CancellationToken cToken = default(CancellationToken))
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
                        Console.WriteLine($"[{requestCount}] reached end of input stream, ending.");
                        return;
                    }

                    // Load root JSON object
                    JObject json = await JObject.LoadAsync(reader);
                    Request request = json.ToObject<Request>();

                    Response response = new Response
                    {
                        Status = 405,
                    };
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
                            response = await TakeHandler(request.Path, request.Headers.GetValueOrDefault("timeout"));
                            break;
                        default:
                            response = new Response
                            {
                                Status = 405
                            };
                            break;
                    }

                    serializer.Serialize(writer, response);
                    await writer.FlushAsync();

                    Interlocked.Increment(ref requestCount);
                }
                catch (JsonReaderException e)
                {
                    Console.WriteLine($"[{requestCount}] Error reading JSON: {e.Message}, continuing");
                    Response response = new Response {Status = 400,};
                    serializer.Serialize(writer, response);
                    await writer.FlushAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[{requestCount}] Unexpected exception, closing connection {e.Message}");
                }
            }
        }

        private Response PutHandler(string requestPath, JObject requestPayload)
        {
            if (!queues.TryGetValue(requestPath, out TransferQueueAsync<JObject> queue))
            {
                Console.WriteLine($"[{requestCount}] Put NoQueue '{requestPath}'");
                return new Response
                {
                    Status = 404
                };
            }

            queue.Put(requestPayload);
            Console.WriteLine($"[{requestCount}] Put Success");

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
                Console.WriteLine($"[{requestCount}] Created list {requestPath}");
            }
            else
            {
                Console.WriteLine($"[{requestCount}] List {requestPath} exists");
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
                Console.WriteLine($"[{requestCount}] Transfer NoQueue '{requestPath}'");
                return new Response
                {
                    Status = 404
                };
            }

            int timeout = Int32.Parse(timeoutStr);
            Task<bool> task = queue.Transfer(requestPayload, timeout);
            Console.WriteLine($"[{requestCount}] Transfer Task Created");
            try
            {
                bool result = await task;
                if (result)
                {
                    Console.WriteLine($"[{requestCount}] Transfer Success");
                    return new Response
                    {
                        Status = 200
                    };
                }

                //timeout
                Console.WriteLine($"[{requestCount}] Transfer Timeout");
                return new Response
                {
                    Status = 204
                };
            }
            catch (TaskCanceledException)
            {
                //cancelled
                Console.WriteLine($"[{requestCount}] Transfer Cancelled");
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
                Console.WriteLine($"[{requestCount}] Take NoQueue '{requestPath}'");
                return new Response
                {
                    Status = 404
                };
            }

            int timeout = Int32.Parse(timeoutStr);
            Task<JObject> task = queue.Take(timeout);
            Console.WriteLine($"[{requestCount}] Take Task Created");
            try
            {
                JObject result = await task;
                if (result != null)
                {
                    Console.WriteLine($"[{requestCount}] Take Success");
                    return new Response
                    {
                        Status = 200,
                        Payload = result
                    };
                }

                //timeout
                Console.WriteLine($"[{requestCount}] Take Timeout");
                return new Response
                {
                    Status = 204
                };
            }
            catch (TaskCanceledException)
            {
                //cancelled
                Console.WriteLine($"[{requestCount}] Take Cancelled");
                return new Response
                {
                    Status = 204
                };
            }
        }
    }
}