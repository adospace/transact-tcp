using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class TcpServerPeerConnection : Connection
    {
        private readonly TcpClient _tcpToClient;

        public TcpServerPeerConnection(TcpClient tcpToClient, ConnectionSettings connectionSettings = null) 
            : base(connectionSettings)
        {
            _tcpToClient = tcpToClient;
        }

        protected override bool IsStreamConnected => (_tcpToClient?.Connected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _tcpToClient.ReceiveTimeout = _connectionSettings.KeepAliveMilliseconds * 2;
            _connectedStream = await CreateConnectedStreamAsync(_tcpToClient, cancellationToken);
        }

        protected virtual Task<Stream> CreateConnectedStreamAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(tcpClient.GetStream());
        }
    }
}
