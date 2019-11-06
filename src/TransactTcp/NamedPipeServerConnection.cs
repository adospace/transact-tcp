using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class NamedPipeServerConnection : Connection
    {
        private NamedPipeServerStream _pipeServer;
        private readonly string _localEndPointName;
        private readonly ServerConnectionSettings _serverConnectionSettings;

        public NamedPipeServerConnection(
           NamedPipeConnectionEndPoint connectionEndPoint)
            : base(connectionEndPoint?.ConnectionSettings ?? new ServerConnectionSettings(keepAliveMilliseconds: 0 /*by default named pipe does't require keep alive messages*/))
        {
            _serverConnectionSettings = (ServerConnectionSettings)_connectionSettings;
            _localEndPointName = connectionEndPoint.LocalEndPointName ?? throw new ArgumentNullException("connectionEndPoint.LocalEndPointName");
        }

        protected override bool IsStreamConnected => (_pipeServer?.IsConnected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _pipeServer =
                new NamedPipeServerStream(_localEndPointName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            if (State == ConnectionState.Connecting)
            {
                //when connecting accept connection with a timeout
                using var timeoutCancellationTokenSource =
                    new CancellationTokenSource(_serverConnectionSettings.ConnectionTimeoutMilliseconds);

                timeoutCancellationTokenSource.Token.Register(() => _pipeServer.Close());

                await _pipeServer.WaitForConnectionAsync(cancellationToken);
            }
            else // if (State == ConnectionState.LinkError)
            {
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
            }

            _connectedStream = _pipeServer;
        }

        protected override void OnDisconnect()
        {
            base.OnDisconnect();

            if (State == ConnectionState.LinkError)
                BeginConnection();
        }
    }

}
