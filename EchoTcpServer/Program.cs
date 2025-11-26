using System;
using System.Threading.Tasks;

namespace TestServerApp
{
    public class TestServerApp
    {
        public static async Task Main(string[] args)
        {
            string host = "127.0.0.1";
            int port = 60000;
            int intervalMilliseconds = 5000;

            await Run(new EchoServer(5000), new UdpTimedSender(host, port), new SystemConsole(), intervalMilliseconds);
        }

        public static async Task Run(EchoServer server, UdpTimedSender sender, IConsole console, int intervalMilliseconds)
        {
            _ = Task.Run(() => server.StartAsync());

            console.WriteLine("Press any key to stop sending...");
            sender.StartSending(intervalMilliseconds);

            console.WriteLine("Press 'q' to quit...");
            while (console.ReadKey(intercept: true).Key != ConsoleKey.Q)
            {
            }

            sender.StopSending();
            server.Stop();
            console.WriteLine("Sender stopped.");

            await Task.CompletedTask;
        }
    }
}