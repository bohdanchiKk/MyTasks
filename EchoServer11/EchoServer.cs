using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace TestServerApp
{
    public class EchoServer
    {
        private readonly int _port;
        private readonly Func<IPAddress, int, ITcpListenerWrapper> _listenerFactory;
        private readonly Func<TcpClient, ITcpClientWrapper> _clientFactory;
        private ITcpListenerWrapper? _listener;
        private CancellationTokenSource? _cancellationTokenSource;

        public EchoServer(int port)
            : this(port,
                  (addr, p) => new TcpListenerWrapper(addr, p),
                  (client) => new TcpClientWrapper(client))
        {
        }

        public EchoServer(
            int port,
            Func<IPAddress, int, ITcpListenerWrapper> listenerFactory,
            Func<TcpClient, ITcpClientWrapper> clientFactory)
        {
            _port = port;
            _listenerFactory = listenerFactory;
            _clientFactory = clientFactory;
        }


        public async Task StartAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _listener = _listenerFactory(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine($"Server started on port {_port}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine("Client connected.");

                    _ = Task.Run(() => HandleClientAsync(_clientFactory(client), _cancellationTokenSource.Token));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }

            Console.WriteLine("Server shutdown.");
        }

        public async Task HandleClientAsync(ITcpClientWrapper client, CancellationToken token)
        {
            using (client)
            using (INetworkStreamWrapper stream = client.GetStream())
            {
                try
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while (!token.IsCancellationRequested && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead, token);
                        Console.WriteLine($"Echoed {bytesRead} bytes to the client.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful cancellation: do not treat as error
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
                finally
                {
                    client.Close();
                    Console.WriteLine("Client disconnected.");
                }
            }
        }

        public void Stop()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }

            Console.WriteLine("Server stopped.");
        }
    }
}