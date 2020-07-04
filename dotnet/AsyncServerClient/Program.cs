using System.Linq;
using System.Threading.Tasks;

namespace AsyncServer
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
                        await JSON.JsonEchoServer.LaunchServer();
                        break;
                    case "-clientJSON":
                        await JSON.JsonEchoClient.LaunchClient(args.Skip(1).ToArray());
                        break;
                    case "-serverRAW":
                        await RAW.TcpMultiThreadedTapEchoServer.LaunchServer();
                        break;
                    case "-clientRAW":
                        await RAW.TcpEchoClientAsync.LaunchClient(args.Skip(1).ToArray());
                        break;
                    default:
                        break;
                }
            }
        }
    }
}