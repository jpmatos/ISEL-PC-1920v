/**
 * ISEL, LEIC, Concurrent Programming
 *
 * Client of the TCP JSON echo server.
 *
 * To build this client as a .NET Core project:
 *   1. Set the directory where the file client.cs resides as the current directory 
 *   2. To create the project file (<current-dir-name>.csproj) execute : dotnet new console<enter>
 *   3. Remove the file "Program.cs"
 *   4. To add the NuGet package Newtonsoft.Json to the project execute: dotnet add package Newtonsoft.Json<enter>
 *   5. To build the executable execute: dotnet build<enter>
 *   6. To run the executable execute: dotnet run<enter> 
 *
 * Note: When using Visual Studio, add the Newtonsoft.Json package, consulting:
 *    https://docs.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-in-visual-studio
 *
 * Carlos Martins, June 2020
 **/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AsyncServerClient.MIX
{
    class JsonEchoClientSingle
    {
        public class RequestPayload
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
                        request = new Request
                        {
                            Method = "CREATE",
                            Headers = new Dictionary<String, String>(),
                            Path = "path01"
                        };
                        break;
                    case 2:
                        request = new Request
                        {
                            Method = "PUT",
                            Headers = new Dictionary<String, String>(),
                            Path = "path01",
                            Payload = JObject.FromObject(new RequestPayload
                            {
                                Message = "message01"
                            })
                        };
                        break;
                    case 3:
                        request = new Request
                        {
                            Method = "TRANSFER",
                            Headers = new Dictionary<String, String>
                            {
                                {"timeout", "10000"}
                            },
                            Path = "path01",
                            Payload = JObject.FromObject(new RequestPayload
                            {
                                Message = "message01"
                            })
                        };
                        break;
                    case 4:
                        request = new Request
                        {
                            Method = "TAKE",
                            Headers = new Dictionary<String, String>
                            {
                                {"timeout", "10000"}
                            },
                            Path = "path01",
                        };
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
                        } 
                        while (reader.TokenType != JsonToken.StartObject &&
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
                        Console.WriteLine($"<--{response.ToString()}");
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