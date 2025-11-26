using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestServerApp;

namespace EchoServerTests
{
    [TestFixture]
    public class ProgramTests
    {
        [Test]
        public async Task Main_Should_RunWithoutExceptions()
        {
            var args = Array.Empty<string>();

            // Arrange fake console that will emit 'q'
            var console = new FakeConsole();
            console.Enqueue(ConsoleKey.Q);

            // Run program with injected dependencies
            var server = new EchoServer(5000);
            var sender = new UdpTimedSender("127.0.0.1", 60000);

            await TestServerApp.TestServerApp.Run(server, sender, console, 10);

            Assert.Pass("Main завершився без винятків.");
        }
    }
}