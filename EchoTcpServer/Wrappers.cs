using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TestServerApp
{
    public class TcpListenerWrapper : ITcpListenerWrapper
    {
        private readonly TcpListener _listener;

        public TcpListenerWrapper(IPAddress address, int port)
        {
            _listener = new TcpListener(address, port);
        }

        public void Start() => _listener.Start();

        public void Stop() => _listener.Stop();

        public async Task<TcpClient> AcceptTcpClientAsync()
            => await _listener.AcceptTcpClientAsync();
    }

    public class TcpClientWrapper : ITcpClientWrapper
    {
        private readonly TcpClient _client;

        public TcpClientWrapper(TcpClient client)
        {
            _client = client;
        }

        public INetworkStreamWrapper GetStream()
            => new NetworkStreamWrapper(_client.GetStream());

        public void Close() => _client.Close();

        public void Dispose() => _client.Dispose();
    }


    public class NetworkStreamWrapper : INetworkStreamWrapper
    {
        private readonly NetworkStream _stream;

        public NetworkStreamWrapper(NetworkStream stream)
        {
            _stream = stream;
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => await _stream.ReadAsync(buffer, offset, count, token);

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => await _stream.WriteAsync(buffer, offset, count, token);

        public void Dispose() => _stream.Dispose();
    }
}