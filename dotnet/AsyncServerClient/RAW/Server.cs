/**
 *
 * ISEL, LEIC, Concurrent Programming
 *
 * A TCP multithreaded server based on TAP interfaces and C# asynchronous methods.
 *
 * To build this server as a .NET Core project:
 *   1. Set the directory where the file server.cs resides as the current directory 
 *   2. To create the project file (<current-dir-name>.csproj) execute : dotnet new console<enter>
 *   3. Remove the file "Program.cs"
 *   4. To build the executable execute: dotnet build<enter>
 *   5. To run the executable execute: dotnet run<enter> 
 *
 * Carlos Martins, June, 2020
 **/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace AsyncServer.RAW
{
/**
 * A Tcp multithreaded echo server, using TAP interfaces and C# asynchronous methods.
 */
    public class TcpMultiThreadedTapEchoServer
    {
        private const int SERVER_PORT = 13000;
        private const int BUFFER_SIZE = 1024;
        private const int MIN_SERVICE_TIME = 50;
        private const int MAX_SERVICE_TIME = 500;

        // The maximum number of simultaneous connections allowed.
        private const int MAX_SIMULTANEOUS_CONNECTIONS = 20;

        // Constants used when we poll to detect that the server is idle.
        private const int WAIT_FOR_IDLE_TIME = 10000;
        private const int POLLING_INTERVAL = WAIT_FOR_IDLE_TIME / 20;

        // A random generator private to each thread.
        private ThreadLocal<Random> random =
            new ThreadLocal<Random>(() => new Random(Thread.CurrentThread.ManagedThreadId));

        // The listen server socket
        private TcpListener server;

        // Total number of connection requests.
        private volatile int requestCount = 0;

        // Construct the server
        public TcpMultiThreadedTapEchoServer()
        {
            // Create a listen socket and bind it to the server port in the localhos IP address.
            server = new TcpListener(IPAddress.Loopback, SERVER_PORT);

            // Start listen connections from listen socket.
            server.Start();
        }

        /**
         * Serves the connection represented by the specified TcpClient socket, using an asynchronous method.
         * If we want to cancel the processing of all already accepted connections, we
         * must propagate the received CancellationToken.
        */
        private async Task ServeConnectionAsync(TcpClient connection,
            CancellationToken cToken = default(CancellationToken))
        {
            using (connection)
            {
                try
                {
                    // Get a stream for reading and writing through the client socket.
                    NetworkStream stream = connection.GetStream();
                    byte[] requestBuffer = new byte[BUFFER_SIZE];

                    // Receive the request (we know that its size is smaller than BUFFER_SIZE bytes);
                    int bytesRead = await stream.ReadAsync(requestBuffer, 0, requestBuffer.Length);

                    Stopwatch sw = Stopwatch.StartNew();

                    // Convert the request content to ASCII and display it.
                    string request = Encoding.ASCII.GetString(requestBuffer, 0, bytesRead);
                    Console.WriteLine($"-->[{request}]");

                    /**
                     * Simulate asynchronously a random service time, and after that, send the response
                     * to the client.
                     */
                    await Task.Delay(random.Value.Next(MIN_SERVICE_TIME, MAX_SERVICE_TIME), cToken);

                    string response = request.ToUpper();
                    Console.WriteLine($"<--[{response}({sw.ElapsedMilliseconds} ms)]");

                    // Convert the response to a byte array and send it to the client.
                    byte[] responseBuffer = Encoding.ASCII.GetBytes(response);
                    await stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);

                    // Increment the number of processed requests.
                    Interlocked.Increment(ref requestCount);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /**
          * Asynchronous method that listens for connections and calls the ServeConnectionAsync method.
         * This method limits, by design, the maximum number of simultaneous connections.
         */
        public async Task ListenAsync(CancellationToken cToken)
        {
            // This set stores the set of all tasks launched but for which there is no
            // certainty that they have been completed.
            // Using this set we will limit the maximum number of active simultaneous
            // connections.
            var startedTasks = new HashSet<Task>();

            // Accept connections until the shutdown of the server is requested.
            while (!cToken.IsCancellationRequested)
            {
                try
                {
                    var connection = await server.AcceptTcpClientAsync();

                    /**
                     * Add the task returned by the ServeConnectionAsync method to the task set.
                     */
                    startedTasks.Add(ServeConnectionAsync(connection, cToken));

                    /**
                     * If the defined limit of connections was reached: (1) we start removing from the
                     * set all the already completed tasks; (2) if no tasl can be removed, await
                     * unconditionally until at least one of the active tasks complete its processing,
                     * and then remove it from the set.
                     */
                    if (startedTasks.Count >= MAX_SIMULTANEOUS_CONNECTIONS)
                    {
                        if (startedTasks.RemoveWhere(task => task.IsCompleted) == 0)
                            startedTasks.Remove(await Task.WhenAny(startedTasks));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // benign exception - occurs when when stop accepting connections
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"***{ex.GetType().Name}: {ex.Message}");
                }
            }

            /**
              * Before return, wait for completion of tasks processing of all accepted
             * connections.
             */
            if (startedTasks.Count > 0)
                await Task.WhenAll(startedTasks);
        }

        /**
         * Start shudown the server and wait for termination.
         */
        private async Task ShutdownAndWaitTerminationAsync(Task listenTask, CancellationTokenSource cts)
        {
            /**
             * Before we close the listener socket, try to accept all connections already accepted by
             * the operating system's sockets layer.
             * Since that it is possible that we never see no connections pending, due
             * to an uninterrupted arrival of connection requests, we poll only for
             * a limited amount of time.
             */
            for (int i = 0; i < WAIT_FOR_IDLE_TIME; i += POLLING_INTERVAL)
            {
                if (!server.Pending())
                    break;
                await Task.Delay(POLLING_INTERVAL);
            }

            /**
             * Stop accepting new connection requests.
             */
            server.Stop();

            /**
             * Set the cancellation token to the canceled state. Any client waiting client requests will
             * be cancelled, and all calls to the ServeConnectionAsync method will return. Note that only
             * will be cancelled the asynchronous operations to which the cancellation token was passed.
             * The others will terminate normally.
             */
            cts.Cancel();

            /**
             * Now wait until the completion of the ListenAsync method, before close the
             * application.
             */
            await listenTask;
        }

        /**
         * Server entry point
         */
        public static async Task LaunchServer()
        {
            // Create the instance of the echo server.
            TcpMultiThreadedTapEchoServer echoServer = new TcpMultiThreadedTapEchoServer();

            // The cancellation token source used to propagate the shutdonw of the server.
            CancellationTokenSource cts = new CancellationTokenSource();

            /**
             * Start listen and processing connectios.
             * This is an asynchronous method that returns on the first await that will
             * suspend the execution of the method.
            */
            Task listenTask = echoServer.ListenAsync(cts.Token);

            /**
             * Wait an <enter> press from the console to terminate the server.
             */
            Console.WriteLine("--Hit <enter> to exit the server...");
            await Console.In.ReadLineAsync();

            /**
             * After pressing Shutdown, trigger the server shutdown and wait for
             * all the processing in progress to finish.
             */
            await echoServer.ShutdownAndWaitTerminationAsync(listenTask, cts);

            // Display the number of requests processed
            Console.WriteLine($"--{echoServer.requestCount} requests were processed");
        }
    }
}