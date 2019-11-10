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
        private NamedPipeServerStream _pipeServerIn;
        private NamedPipeServerStream _pipeServerOut;
        private readonly string _localEndPointName;
        private readonly ServerConnectionSettings _serverConnectionSettings;

        public NamedPipeServerConnection(
           NamedPipeConnectionEndPoint connectionEndPoint)
            : base(connectionEndPoint?.ConnectionSettings ?? new ServerConnectionSettings(keepAliveMilliseconds: 0 /*by default named pipe does't require keep alive messages*/))
        {
            _serverConnectionSettings = (ServerConnectionSettings)_connectionSettings;
            _localEndPointName = connectionEndPoint.LocalEndPointName ?? throw new ArgumentNullException("connectionEndPoint.LocalEndPointName");
        }

        protected override bool IsStreamConnected => (_pipeServerIn?.IsConnected).GetValueOrDefault() && (_pipeServerOut?.IsConnected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _pipeServerIn =
                new NamedPipeServerStream(_localEndPointName + "_IN", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _pipeServerOut =
                new NamedPipeServerStream(_localEndPointName + "_OUT", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            if (State == ConnectionState.Connecting)
            {
                //when connecting accept connection with a timeout
                using var timeoutCancellationTokenSource =
                    new CancellationTokenSource(_serverConnectionSettings.ConnectionTimeoutMilliseconds);

                timeoutCancellationTokenSource.Token.Register(() =>
                {
                    _pipeServerIn.Close();
                    _pipeServerOut.Close();
                });

                await _pipeServerIn.WaitForConnectionAsync(cancellationToken);
                await _pipeServerOut.WaitForConnectionAsync(cancellationToken);
            }
            else // if (State == ConnectionState.LinkError)
            {
                await _pipeServerIn.WaitForConnectionAsync(cancellationToken);
                await _pipeServerOut.WaitForConnectionAsync(cancellationToken);
            }

            _connectedStream = new NamedPipeConnectedStream(_pipeServerIn, _pipeServerOut);
        }

        protected override void OnDisconnect()
        {
            base.OnDisconnect();

            if (State == ConnectionState.LinkError)
                BeginConnection();
        }
    }

}
