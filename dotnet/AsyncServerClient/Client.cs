using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AsyncServerClient.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsyncServerClient
{
    class JsonEchoClientSingle
    {
        private class RequestPayload
        {
            public string Message { get; set; }

            public override String ToString()
            {
                return $"[ Message: {Message}]";
            }
        }

        private const int SERVER_PORT = 13000;
        private static volatile int requestCount = 0;
        private static JsonSerializer serializer = new JsonSerializer();

        //Entry Point
        public static async Task LaunchClient()
        {
            bool cont = true;
            while (cont)
            {
                Console.WriteLine("Pick an option:\n" +
                                  "[1] - CREATE\n" +
                                  "[2] - PUT\n" +
                                  "[3] - TRANSFER\n" +
                                  "[4] - TAKE\n" +
                                  "[0] - Exit");
                Request request;
                int oper = Convert.ToInt32(Console.ReadLine());
                switch (oper)
                {
                    case 1:
                        request = BuildCreateRequest();
                        break;
                    case 2:
                        request = BuildPutRequest();
                        break;
                    case 3:
                        request = BuildTransferRequest();
                        break;
                    case 4:
                        request = BuildTakeRequest();
                        break;
                    case 0:
                        cont = false;
                        continue;
                    default:
                        Console.WriteLine("No operation.");
                        continue;
                }

                await SendRequestAndReceiveResponseAsync("localhost", request);
            }
        }

        private static Request BuildTakeRequest()
        {
            Console.WriteLine("Path:");
            string path = Console.ReadLine();
            Console.WriteLine("Timeout:");
            string timeout = Console.ReadLine();
            return new Request
            {
                Method = "TAKE",
                Headers = new Dictionary<String, String>
                {
                    {"timeout", timeout}
                },
                Path = path,
            };
        }

        private static Request BuildTransferRequest()
        {
            Console.WriteLine("Path:");
            string path = Console.ReadLine();
            Console.WriteLine("Message:");
            string message = Console.ReadLine();
            Console.WriteLine("Timeout:");
            string timeout = Console.ReadLine();
            return new Request
            {
                Method = "TRANSFER",
                Headers = new Dictionary<String, String>
                {
                    {"timeout", timeout}
                },
                Path = path,
                Payload = JObject.FromObject(new RequestPayload
                {
                    Message = message
                })
            };
        }

        private static Request BuildPutRequest()
        {
            Console.WriteLine("Path:");
            string path = Console.ReadLine();
            Console.WriteLine("Message:");
            string message = Console.ReadLine();
            return new Request
            {
                Method = "PUT",
                Headers = new Dictionary<String, String>(),
                Path = path,
                Payload = JObject.FromObject(new RequestPayload
                {
                    Message = message
                })
            };
        }

        private static Request BuildCreateRequest()
        {
            Console.WriteLine("Path:");
            string path = Console.ReadLine();
            return new Request
            {
                Method = "CREATE",
                Headers = new Dictionary<String, String>(),
                Path = path
            };
        }

        static async Task SendRequestAndReceiveResponseAsync(string server, Request request)
        {
            using (TcpClient connection = new TcpClient())
            {
                try
                {
                    await connection.ConnectAsync(server, SERVER_PORT);

                    // Add some headers for test purposes 
                    request.Headers.Add("agent", "json-client");
                    // request.Headers.Add("timeout", "10000");

                    // Translate the message to JSON and send it to the echo server.
                    JsonTextWriter writer = new JsonTextWriter(new StreamWriter(connection.GetStream()));
                    serializer.Serialize(writer, request);
                    await writer.FlushAsync();

                    // Receive the server's response and display it.
                    JsonTextReader reader = new JsonTextReader(new StreamReader(connection.GetStream()))
                    {
                        SupportMultipleContent = true
                    };
                    try
                    {
                        // Consume any bytes until start of JSON object ('{')
                        do
                        {
                            await reader.ReadAsync();
                        } while (reader.TokenType != JsonToken.StartObject &&
                                 reader.TokenType != JsonToken.None);

                        if (reader.TokenType == JsonToken.None)
                        {
                            Console.WriteLine("***error: reached end of input stream, ending.");
                            return;
                        }

                        // Read the response JSON object
                        JObject jresponse = await JObject.LoadAsync(reader);

                        // Back to the .NET world
                        Response response = jresponse.ToObject<Response>();
                        Console.WriteLine($"<--{response}");
                    }
                    catch (JsonReaderException jre)
                    {
                        Console.WriteLine($"***error: error reading JSON: {jre.Message}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"-***error: exception: {e.Message}");
                    }

                    Interlocked.Increment(ref requestCount);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"--***error:[{request.Payload}] {ex.Message}");
                }
            }
        }
    }
}