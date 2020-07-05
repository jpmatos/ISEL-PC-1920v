/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * Client of echo server TAP interfaces and C# asynchronous methods.
 *
 *
 * To build this client as a .NET Core project:
 *   1. Set the directory where the file client.cs resides as the current directory 
 *   2. To create the project file (<current-dir-name>.csproj) execute : dotnet new console<enter>
 *   3. Remove the file "Program.cs"
 *   4. To add the NuGet package Newtonsoft.Json to the project execute: dotnet add package Newtonsoft.Json<enter>
 *   5. To build the executable execute: dotnet build<enter>
 *   6. To run the executable execute: dotnet run<enter> 
 *
 * Carlos Martins, June 2020
 *
 **/

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncServerClient.RAW
{
/**
 * Tcp client for a echo server.
 */

    class TcpEchoClientAsync
    {
        private const int SERVER_PORT = 13000;
        private const int BUFFER_SIZE = 1024;
        private static volatile int requestCount = 0;

        /**
         * Send a request to the server and display its response.
         */
        private static async Task SendRequestAndReceiveResponseAsync(string host, string requestMessage)
        {
            using (TcpClient connection = new TcpClient())
            {
                try
                {
                    // Start a stop watch timer to compute the roundtrip time.
                    Stopwatch sw = Stopwatch.StartNew();

                    /**
                      * Connect the TcpClient socket to the server and get the associated stream.
                     */
                    Console.WriteLine("Waiting Connection...");
                    await connection.ConnectAsync(host, SERVER_PORT);
                    NetworkStream stream = connection.GetStream();
                    Console.WriteLine("Connected.");

                    /**
                     * Translate the message to a byte stream and send it to the server.
                     */
                    byte[] requestBuffer = Encoding.ASCII.GetBytes(requestMessage);
                    await stream.WriteAsync(requestBuffer, 0, requestBuffer.Length);
                    Console.WriteLine($"-->[{requestMessage}]");

                    /**
                     * Receive the server's response and display it.
                     */
                    byte[] responseBuffer = new byte[BUFFER_SIZE];
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length)) > 0)
                        Console.WriteLine(
                            $"<--[{Encoding.ASCII.GetString(responseBuffer, 0, bytesRead)} ({sw.ElapsedMilliseconds}) ms]");
                    sw.Stop();
                    Interlocked.Increment(ref requestCount);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /**
         * Send continuously batches of requests until a key is pressed.
         */
        private const int REQS_PER_BATCH = 25;

        public static async Task LaunchClient(string[] args)
        {
            bool executeOnce = false;

            string request = (args.Length > 0) ? args[0] : "--default request message to be echoed--";

            Task[] tasks = new Task[REQS_PER_BATCH];
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                for (int i = 0; i < REQS_PER_BATCH; i++)
                    tasks[i] = SendRequestAndReceiveResponseAsync("localhost", String.Format($"#{i:D2}: {request}"));
                await Task.WhenAll(tasks);
            } while (!(executeOnce || Console.KeyAvailable));

            Console.WriteLine($"--completed {requestCount} request in {sw.ElapsedMilliseconds} ms");
        }
    }
}