using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class NamedPipeServerPeerConnection : Connection
    {
        private readonly NamedPipeServerStream _pipeConnectedWithClient;

        public NamedPipeServerPeerConnection(
            NamedPipeServerStream pipeConnectedWithClient, 
            ConnectionSettings connectionSettings = null) 
            : base(connectionSettings ?? new ConnectionSettings(keepAliveMilliseconds: 0 /*by default named pipe does't require keep alive messages*/))
        {
            _pipeConnectedWithClient = pipeConnectedWithClient;
            _connectionSettings.AutoReconnect = false;
        }

        protected override bool IsStreamConnected => (_pipeConnectedWithClient?.IsConnected).GetValueOrDefault();

        protected override Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _connectedStream = _pipeConnectedWithClient;
            return Task.CompletedTask;
        }

        protected virtual Task<Stream> CreateConnectedStreamAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(tcpClient.GetStream());
        }
    }
}
