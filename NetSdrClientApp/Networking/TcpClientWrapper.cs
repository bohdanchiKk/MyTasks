using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{

    public class TcpClientWrapper : ITcpClient
    {
        private string _host;
        private int _port;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        public bool Connected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public event EventHandler<byte[]>? MessageReceived;

        public TcpClientWrapper(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            if (Connected)
            {
                Console.WriteLine($"Already connected to {_host}:{_port}");
                return;
            }

            _tcpClient = new TcpClient();
            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();

                Console.WriteLine($"Connected to {_host}:{_port}");

                _ = StartListeningAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (_tcpClient == null)
            {
                Console.WriteLine("No active connection to disconnect.");
                return;
            }

            _cts?.Cancel();

            try
            {
                _stream?.Close();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch
            {
                // ignore cleanup errors
            }

            _cts?.Dispose();
            _cts = null;
            _tcpClient = null;
            _stream = null;

            Console.WriteLine("Disconnected.");
        }

        public Task SendMessageAsync(byte[] data) => SendInternalAsync(data);

        public Task SendMessageAsync(string str) => SendInternalAsync(Encoding.UTF8.GetBytes(str));

        private async Task SendInternalAsync(byte[] data)
        {
            // ВИПРАВЛЕНО: замінено  на ||
            if (!Connected || _stream == null || !_stream.CanWrite)
            {
                throw new InvalidOperationException("Not connected to a server.");
            }

            var hex = data.Length > 0 ? string.Join(" ", data.Select(b => b.ToString("X2"))) : "<empty>";
            Console.WriteLine($"Message sent: {hex}");

            await _stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        }

        private async Task StartListeningAsync()
        {
            // ВИПРАВЛЕНО: замінено  на ||
            if (!Connected || _stream == null || !_stream.CanRead)
            {
                Console.WriteLine("Cannot start listener: no active connection or stream not readable.");
                return;
            }

            try
            {
                Console.WriteLine("Starting listening for incoming messages.");

                while (!_cts!.Token.IsCancellationRequested)
                {
                    byte[] buffer = new byte[8194];
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token).ConfigureAwait(false);

                    if (bytesRead > 0)
                    {
                        var received = buffer.AsSpan(0, bytesRead).ToArray();
                        MessageReceived?.Invoke(this, received);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // очікувана ситуація при відключенні
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in listening loop: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Listener stopped.");
            }
        }
    }
}