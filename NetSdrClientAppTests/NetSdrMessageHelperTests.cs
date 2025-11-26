using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NetSdrClientApp.Networking;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        private static TcpListener StartListener(out int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return listener;
        }

        private static FieldInfo GetField(string name) =>
            typeof(TcpClientWrapper).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static MethodInfo GetStartListeningMethod() =>
            typeof(TcpClientWrapper).GetMethod("StartListeningAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)!;

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetControlItemMessage_ShouldBuildMessageCorrectly()
        {
            var msg = NetSdrMessageHelper.GetControlItemMessage(
                NetSdrMessageHelper.MsgTypes.Ack,
                NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency,
                new byte[5]);

            Assert.That(msg.Length, Is.EqualTo(9)); // 2(header)+2(code)+5
        }

        [Test]
        public void GetDataItemMessage_ShouldBuildMessageCorrectly()
        {
            var msg = NetSdrMessageHelper.GetDataItemMessage(
                NetSdrMessageHelper.MsgTypes.DataItem0,
                new byte[10]);

            Assert.That(msg.Length, Is.EqualTo(12)); // header + seq
        }

        [Test]
        public void GetMessage_ShouldHandleDataItemMaxEdgeCase()
        {
            // ���� DataItem � ������� = _maxDataItemMessageLength - 2 => �� ����� 0
            var method = typeof(NetSdrMessageHelper)
                .GetMethod("GetHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            byte[] result = (byte[])method.Invoke(null, new object[] { NetSdrMessageHelper.MsgTypes.DataItem0, 8192 })!;
            ushort val = BitConverter.ToUInt16(result);
            var type = (NetSdrMessageHelper.MsgTypes)(val >> 13);
            Assert.That(type, Is.EqualTo(NetSdrMessageHelper.MsgTypes.DataItem0));
        }


        [Test]
        public void TranslateMessage_ShouldParseDataItemCorrectly()
        {
            var type = NetSdrMessageHelper.MsgTypes.DataItem3;
            ushort seqNum = 42;
            byte[] seqBytes = BitConverter.GetBytes(seqNum);
            byte[] data = { 10, 20, 30, 40 };

            var headerMethod = typeof(NetSdrMessageHelper)
                .GetMethod("GetHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            byte[] header = (byte[])headerMethod.Invoke(null, new object[] { type, seqBytes.Length + data.Length })!;

            var msg = header.Concat(seqBytes).Concat(data).ToArray();

            bool ok = NetSdrMessageHelper.TranslateMessage(
                msg,
                out var parsedType,
                out var parsedCode,
                out var parsedSeq,
                out var body);

            Assert.That(ok, Is.True);
            Assert.That(parsedType, Is.EqualTo(type));
            Assert.That(parsedSeq, Is.EqualTo(seqNum));
            Assert.That(parsedCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(body, Is.EqualTo(data));
        }


        [Test]
        public void TranslateHeader_ShouldHandleDataItemZeroLength()
        {
            var method = typeof(NetSdrMessageHelper)
                .GetMethod("TranslateHeader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            ushort encoded = (ushort)(((int)NetSdrMessageHelper.MsgTypes.DataItem1 << 13) + 0);
            byte[] header = BitConverter.GetBytes(encoded);
            object[] args = { header, null!, null! };

            method.Invoke(null, args);
            Assert.Pass(); // ���� ��� ������� DataItem edge-case
        }

        [Test]
        public void GetSamples_ShouldReturnExpectedIntegers()
        {
            ushort bits = 16;
            byte[] body = { 0x01, 0x02, 0x03, 0x04 };

            var samples = NetSdrMessageHelper.GetSamples(bits, body).ToArray();

            Assert.That(samples.Length, Is.GreaterThan(0));
        }

        [Test]
        public void GetSamples_ShouldThrow_WhenSampleSizeTooLarge()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(64, new byte[] { 1, 2, 3 }).ToList());
        }

        [Test]
        public void GetSamples_ShouldHandleIncompleteSamples()
        {
            // body �� ������� ������ ������
            ushort bits = 8;
            byte[] body = { 10, 20, 30 }; // 3 �����

            var samples = NetSdrMessageHelper.GetSamples(bits, body).ToArray();

            Assert.That(samples.Length, Is.EqualTo(3)); // 3 ���� �� 1 �����
        }

        [Test]
        public void GetControlItemMessage_ShouldThrow_WhenTooLong()
        {
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;

            // ��������� ����� ���� ����
            var tooLong = new byte[9000]; // > 8191 - ���� � ����

            var ex = Assert.Throws<ArgumentException>(() =>
                NetSdrMessageHelper.GetControlItemMessage(type, code, tooLong)
            );

            Assert.That(ex.Message, Does.Contain("Message length exceeds allowed value"));
        }

        [Test]
        public async Task Disconnect_CanBeCalledMultipleTimes_AfterConnect()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);
            wrapper.Connect();
            using var serverClient = await acceptTask;

            Assert.DoesNotThrow(() => wrapper.Disconnect());
            Assert.DoesNotThrow(() => wrapper.Disconnect());

            listener.Stop();
        }

        [Test]
        public void Connected_ShouldBeFalse_ByDefault()
        {
            var client = new TcpClientWrapper("127.0.0.1", 12345);
            Assert.That(client.Connected, Is.False);
        }

        [Test]
        public void Disconnect_WhenNoClient_ShouldNotThrow()
        {
            var client = new TcpClientWrapper("127.0.0.1", 12345);
            Assert.DoesNotThrow(() => client.Disconnect());
        }
        [Test]
        public async Task Connect_ShouldConnect_AndSecondCall_DoesNotReconnect()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            // перший Connect – реальне підключення
            wrapper.Connect();
            using var serverClient = await acceptTask;

            Assert.That(wrapper.Connected, Is.True, "Після першого Connect має бути підключено.");

            // другий Connect – гілка Already connected
            Assert.DoesNotThrow(() => wrapper.Connect());

            wrapper.Disconnect();
            listener.Stop();
        }

        [Test]
        public void Connect_WithInvalidHost_ShouldNotThrow_AndStayDisconnected()
        {
            var wrapper = new TcpClientWrapper("256.256.256.256", 12345);
            Assert.DoesNotThrow(() => wrapper.Connect());
            Assert.That(wrapper.Connected, Is.False);
        }

        // ----------- DISCONNECT ------------

        [Test]
        public async Task Disconnect_WhenConnected_ShouldClearState()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);
            wrapper.Connect();
            using var serverClient = await acceptTask;

            Assert.That(wrapper.Connected, Is.True);

            wrapper.Disconnect();

            Assert.That(wrapper.Connected, Is.False);
            listener.Stop();
        }

        // ----------- SEND MESSAGE ------------

        [Test]
        public void SendMessageAsync_WhenNotConnected_ShouldThrow()
        {
            var wrapper = new TcpClientWrapper("127.0.0.1", 12345);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrapper.SendMessageAsync("test"));

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrapper.SendMessageAsync(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public async Task SendMessageAsync_Bytes_ShouldSendToServer()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();
            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            wrapper.Connect();
            using var serverClient = await acceptTask;
            using var serverStream = serverClient.GetStream();

            byte[] toSend = { 0x01, 0x02, 0x03, 0x04 };

            await wrapper.SendMessageAsync(toSend);

            byte[] buffer = new byte[1024];
            int read = await serverStream.ReadAsync(buffer, 0, buffer.Length);

            Assert.That(read, Is.EqualTo(toSend.Length));
            CollectionAssert.AreEqual(toSend, buffer.Take(read).ToArray());

            wrapper.Disconnect();
            listener.Stop();
        }

        [Test]
        public async Task SendMessageAsync_String_ShouldSendUtf8Encoded()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();
            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            wrapper.Connect();
            using var serverClient = await acceptTask;
            using var serverStream = serverClient.GetStream();

            string text = "HELLO";
            var expected = Encoding.UTF8.GetBytes(text);

            await wrapper.SendMessageAsync(text);

            byte[] buffer = new byte[1024];
            int read = await serverStream.ReadAsync(buffer, 0, buffer.Length);

            CollectionAssert.AreEqual(expected, buffer.Take(read).ToArray());

            wrapper.Disconnect();
            listener.Stop();
        }

        // ----------- StartListeningAsync – guard ------------

        [Test]
        public async Task StartListeningAsync_WhenNotConnected_ShouldReturnImmediately()
        {
            var wrapper = new TcpClientWrapper("127.0.0.1", 12345);

            var method = GetStartListeningMethod();
            var task = (Task)method.Invoke(wrapper, null)!;

            await task; // просто переконуємось, що все закінчилося без винятків
        }

        // ----------- StartListeningAsync – bytesRead > 0, bytesRead == 0, MessageReceived, Moq ------------

        [Test]
        public async Task StartListeningAsync_ShouldRaiseMessageReceived_AndStopOnRemoteClose()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            // Moq-обробник події
            var handlerMock = new Mock<EventHandler<byte[]>>();
            var tcs = new TaskCompletionSource<byte[]>();

            handlerMock
                .Setup(h => h(wrapper, It.IsAny<byte[]>()))
                .Callback<object, byte[]>((sender, data) =>
                {
                    tcs.TrySetResult(data);
                });

            wrapper.MessageReceived += handlerMock.Object;

            wrapper.Connect();
            using var serverClient = await acceptTask;
            using var serverStream = serverClient.GetStream();

            byte[] msg = Encoding.ASCII.GetBytes("PING");
            await serverStream.WriteAsync(msg, 0, msg.Length);
            await serverStream.FlushAsync();

            // Закриваємо серверну сторону, щоб на клієнті eventually bytesRead == 0
            serverClient.Close();
            listener.Stop();

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            Assert.That(tcs.Task.IsCompleted, Is.True, "Подія MessageReceived не була викликана.");

            CollectionAssert.AreEqual(msg, tcs.Task.Result);
            handlerMock.Verify(h => h(wrapper, It.IsAny<byte[]>()), Times.AtLeastOnce);

            wrapper.Disconnect();
        }

        // ----------- StartListeningAsync – catch(Exception) через NullReferenceException (_cts == null) ------------

        [Test]
        public async Task StartListeningAsync_ShouldHandleUnexpectedExceptionInCatch()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();

            // Створюємо wrapper, але НЕ викликаємо Connect()
            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            // Створюємо реальне TCP-зʼєднання вручну
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var serverClient = await acceptTask;
            using var stream = client.GetStream();

            // Підставляємо приватні поля: _tcpClient та _stream, але _cts залишаємо null
            GetField("_tcpClient").SetValue(wrapper, client);
            GetField("_stream").SetValue(wrapper, stream);
            // _cts = null -> при зверненні до _cts!.Token буде NullReferenceException, яка потрапить в catch(Exception)

            var method = GetStartListeningMethod();
            var task = (Task)method.Invoke(wrapper, null)!;
            await task; // виняток повинен бути перехоплений всередині

            client.Close();
            listener.Stop();
        }

        // ----------- StartListeningAsync – catch(OperationCanceledException) ------------

        [Test]
        public async Task StartListeningAsync_ShouldHandleOperationCanceledException()
        {
            var listener = StartListener(out var port);
            var acceptTask = listener.AcceptTcpClientAsync();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var serverClient = await acceptTask;
            using var stream = client.GetStream();

            // Підставляємо поля, включно з власним CancellationTokenSource
            GetField("_tcpClient").SetValue(wrapper, client);
            GetField("_stream").SetValue(wrapper, stream);
            var cts = new CancellationTokenSource();
            GetField("_cts").SetValue(wrapper, cts);

            var method = GetStartListeningMethod();
            var listenTask = (Task)method.Invoke(wrapper, null)!;

            // Даємо трішки часу, щоб ReadAsync почав чекати, а потім скасовуємо
            await Task.Delay(50);
            cts.Cancel();

            await listenTask; // OperationCanceledException має бути перехоплено в catch(OperationCanceledException)

            client.Close();
            listener.Stop();
        }
    }
}