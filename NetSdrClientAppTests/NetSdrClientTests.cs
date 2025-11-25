using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    //TODO: cover the rest of the NetSdrClient code here

    [Test]
public async Task StopIQNoConnectionTest()
{
    // Act
    await _client.StopIQAsync();

    // Assert
    _updMock.Verify(udp => udp.StopListening(), Times.Never);
    Assert.That(_client.IQStarted, Is.False);
}

[Test]
public async Task ChangeFrequencyAsyncTest()
{
    // Arrange 
    await ConnectAsyncTest();
    long frequency = 1000000000; // 1 GHz
    int channel = 1;

    // Act
    await _client.ChangeFrequencyAsync(frequency, channel);

    // Assert
    _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(4)); // 3 from Connect + 1 from ChangeFrequency
}

[Test]
public async Task ChangeFrequencyAsyncNoConnectionTest()
{
    // Arrange
    long frequency = 1000000000;
    int channel = 1;

    // Act
    await _client.ChangeFrequencyAsync(frequency, channel);

    // Assert
    _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
}

[Test]
public async Task SendTcpRequest_SuccessfulTest()
{
    // Arrange
    await ConnectAsyncTest();
    var testMessage = new byte[] { 0x01, 0x02, 0x03 };

    // Act
    var task = _client.SendTcpRequest(testMessage);

    // Simulate response
    var responseData = new byte[] { 0x04, 0x05, 0x06 };
    _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, responseData);

    var result = await task;

    // Assert
    Assert.That(result, Is.EqualTo(responseData));
    _tcpMock.Verify(tcp => tcp.SendMessageAsync(testMessage), Times.Once);
}

[Test]
public async Task SendTcpRequest_NoConnectionTest()
{
    // Arrange
    var testMessage = new byte[] { 0x01, 0x02, 0x03 };

    // Act
    var result = await _client.SendTcpRequest(testMessage);

    // Assert
    Assert.That(result, Is.Empty);
    _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
}

[Test]
public void UdpClient_MessageReceived_Test()
{
    // Arrange
    var testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
    var mockSender = new Mock<IUdpClient>().Object;

    // Act & Assert - Should not throw exception
    Assert.DoesNotThrow(() => 
        _client.GetType()
            .GetMethod("_udpClient_MessageReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_client, new object[] { mockSender, testData })
    );
}

[Test]
public void TcpClient_MessageReceived_WithResponseTask_Test()
{
    // Arrange
    var testData = new byte[] { 0x01, 0x02, 0x03 };
    var mockSender = new Mock<ITcpClient>().Object;

    // Set up a response task first
    var sendTask = _client.SendTcpRequest(new byte[] { 0x00 });

    // Act
    _client.GetType()
        .GetMethod("_tcpClient_MessageReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .Invoke(_client, new object[] { mockSender, testData });

    // Assert - Task should be completed
    Assert.That(sendTask.IsCompleted, Is.True);
}

[Test]
public void TcpClient_MessageReceived_WithoutResponseTask_Test()
{
    // Arrange
    var testData = new byte[] { 0x01, 0x02, 0x03 };
    var mockSender = new Mock<ITcpClient>().Object;

    // Act & Assert - Should not throw when no response task exists
    Assert.DoesNotThrow(() =>
        _client.GetType()
            .GetMethod("_tcpClient_MessageReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_client, new object[] { mockSender, testData })
    );
}

[Test]
public void Constructor_Test()
{
    // Arrange
    var tcpMock = new Mock<ITcpClient>();
    var udpMock = new Mock<IUdpClient>();

    // Act
    var client = new NetSdrClient(tcpMock.Object, udpMock.Object);

    // Assert
    Assert.That(client, Is.Not.Null);
    Assert.That(client.IQStarted, Is.False);
    tcpMock.VerifyAdd(tcp => tcp.MessageReceived += It.IsAny<EventHandler<byte[]>>(), Times.Once);
    udpMock.VerifyAdd(udp => udp.MessageReceived += It.IsAny<EventHandler<byte[]>>(), Times.Once);
}

[Test]
public async Task MultipleOperations_IntegrationTest()
{
    // Arrange
    await ConnectAsyncTest();

    // Act - Perform multiple operations
    await _client.StartIQAsync();
    await _client.ChangeFrequencyAsync(950000000, 1);
    await _client.ChangeFrequencyAsync(1050000000, 2);
    await _client.StopIQAsync();
    _client.Disconect();

    // Assert
    Assert.That(_client.IQStarted, Is.False);
    _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
    _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
    _updMock.Verify(udp => udp.StopListening(), Times.Once);
}


}

