using System;
using System.Threading.Tasks;

namespace AsyncServerClient
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                string option = args[0];
                switch (option)
                {
                    case "-server":
                        await TcpMultiThreadedJsonEchoServer.LaunchServer();
                        break;
                    case "-client":
                        await JsonEchoClientSingle.LaunchClient();
                        break;
                    default:
                        Console.WriteLine($"Invalid option {option}.");
                        break;
                }
            }
            else
                Console.WriteLine("Specify '-server' or '-client' as first argument.");
        }
    }
}