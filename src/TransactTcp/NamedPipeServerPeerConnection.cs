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
        private readonly NamedPipeServerStream _streamIn;
        private readonly NamedPipeServerStream _streamOut;

        public NamedPipeServerPeerConnection(
            NamedPipeServerStream streamIn, 
            NamedPipeServerStream streamOut,
            ConnectionSettings connectionSettings = null) 
            : base(connectionSettings ?? new ServerConnectionSettings(keepAliveMilliseconds: 0 /*by default named pipe does't require keep alive messages*/))
        {
            _streamIn = streamIn;
            _streamOut = streamOut;
        }

        protected override bool IsStreamConnected => (_streamIn?.IsConnected).GetValueOrDefault() && (_streamOut?.IsConnected).GetValueOrDefault();

        protected override Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _connectedStream = new NamedPipeConnectedStream(_streamIn, _streamOut);

            return Task.CompletedTask;
        }


    }
}
