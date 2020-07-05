using System.Linq;
using System.Threading.Tasks;
using AsyncServerClient.JSON;
using AsyncServerClient.RAW;
using AsyncServerClient.MIX;

namespace AsyncServerClient
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                string option = args[0];
                switch (option)
                {
                    case "-serverJSON":
                        await JsonEchoServer.LaunchServer();
                        break;
                    case "-clientJSON":
                        await JsonEchoClient.LaunchClient(args.Skip(1).ToArray());
                        break;
                    case "-serverRAW":
                        await TcpMultiThreadedTapEchoServer.LaunchServer();
                        break;
                    case "-clientRAW":
                        await TcpEchoClientAsync.LaunchClient(args.Skip(1).ToArray());
                        break;
                    case "-serverMIX":
                        await TcpMultiThreadedJsonEchoServer.LaunchServer();
                        break;
                    case "-clientMIX":
                        await JsonEchoClientSingle.LaunchClient();
                        break;
                    default:
                        break;
                }
            }
        }
    }
}