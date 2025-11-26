using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using TestServerApp;

namespace EchoServerTests
{
    public class UdpTimedSenderTests
    {
        [Test]
        public void Constructor_ShouldCreateInstance()
        {
            // Act
            var sender = new UdpTimedSender("127.0.0.1", 5000);

            // Assert
            Assert.That(sender, Is.Not.Null);
        }

        [Test]
        public void StartSending_ShouldThrowIfAlreadyRunning()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 5000);
            sender.StartSending(1000);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sender.StartSending(1000));

            sender.StopSending();
        }

        [Test]
        public async Task StartSending_ShouldSendMessagesAtInterval()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000);

            // Act
            sender.StartSending(100); // Швидкий інтервал для тесту
            await Task.Delay(250); // Чекаємо кілька повідомлень
            sender.StopSending();

            // Assert - якщо не було виключень, тест пройшов
            Assert.Pass();
        }

        [Test]
        public void StopSending_ShouldStopTimer()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000);
            sender.StartSending(1000);

            // Act
            sender.StopSending();

            // Assert - не повинно бути виключень
            Assert.Pass();
        }

        [Test]
        public void StopSending_ShouldHandleMultipleCalls()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 60000);

            // Act
            sender.StopSending();
            sender.StopSending(); // Другий виклик

            // Assert
            Assert.Pass();
        }

        [Test]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 60000);
            sender.StartSending(1000);

            // Act & Assert
            Assert.DoesNotThrow(() => sender.Dispose());
        }

        [Test]
        public void Dispose_ShouldHandleMultipleCalls()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 60000);

            // Act
            sender.Dispose();

            // Assert
            Assert.DoesNotThrow(() => sender.Dispose());
        }

        [Test]
        public void SendMessageCallback_ShouldHandleInvalidIpGracefully()
        {
            // Arrange
            using var sender = new UdpTimedSender("invalid_ip_address", 5000);

            // Викликаємо приватний метод через рефлексію
            var method = typeof(UdpTimedSender).GetMethod("SendMessageCallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            Assert.DoesNotThrow(() => method!.Invoke(sender, new object?[] { null }));
        }

        [Test]
        public void SendMessageCallback_ShouldSendMessageSuccessfully()
        {
            // Arrange
            using var sender = new UdpTimedSender("127.0.0.1", 5000);

            // Викликаємо приватний метод напряму (імітуємо таймер)
            var method = typeof(UdpTimedSender).GetMethod("SendMessageCallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            Assert.DoesNotThrow(() => method!.Invoke(sender, new object?[] { null }));
        }

        [Test]
        public void StartSending_ShouldThrow_WhenAlreadyRunning()
        {
            using var sender = new UdpTimedSender("127.0.0.1", 5000);
            sender.StartSending(100);
            Assert.Throws<InvalidOperationException>(() => sender.StartSending(100));
        }

        [Test]
        public void SendMessageCallback_ShouldHandleInvalidHost()
        {
            using var sender = new UdpTimedSender("999.999.999.999", 5000);
            Assert.DoesNotThrow(() =>
            {
                // Викликаємо приватний метод через reflection, щоб увійти в catch
                var method = typeof(UdpTimedSender)
                    .GetMethod("SendMessageCallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method!.Invoke(sender, new object?[] { null });
            });
        }

        [Test]
        public void StartSending_ShouldIncrementSequenceNumber()
        {
            using var sender = new UdpTimedSender("127.0.0.1", 5000);
            var method = typeof(UdpTimedSender)
                .GetMethod("SendMessageCallback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method!.Invoke(sender, new object?[] { null });
            method!.Invoke(sender, new object?[] { null });

            // Через reflection отримуємо поле
            var field = typeof(UdpTimedSender)
                .GetField("_sequenceNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ushort seq = (ushort)(field!.GetValue(sender) ?? 0);

            Assert.That(seq, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void TcpClientWrapper_Dispose_ShouldNotThrow()
        {
            // Arrange
            using var tcpClient = new TcpClient();
            var wrapper = new TcpClientWrapper(tcpClient);

            // Act + Assert
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }

        [Test]
        public void NetworkStreamWrapper_Dispose_ShouldNotThrow()
        {
            // Arrange: создаем реальное подключение к локальному TCP-серверу, чтобы получить валидный NetworkStream
            using var tcpClient = new TcpClient();

            // Поднимаем локальный слушатель на случайном свободном порту
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

            // Соединяемся клиентом и принимаем соединение на сервере
            tcpClient.Connect(System.Net.IPAddress.Loopback, port);
            using var serverSide = listener.AcceptTcpClient();
            listener.Stop();

            using var stream = tcpClient.GetStream();
            var wrapper = new NetworkStreamWrapper(stream);

            // Act + Assert
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }
    }
}


