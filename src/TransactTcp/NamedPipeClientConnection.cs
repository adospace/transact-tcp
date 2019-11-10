using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class NamedPipeClientConnection : Connection
    {
        private readonly string _remoteNamedPipeName;
        private readonly string _remoteNamedPipeHost;
        private NamedPipeClientStream _pipeClientIn;
        private NamedPipeClientStream _pipeClientOut;
        private readonly ClientConnectionSettings _clientConnectionSettings;

        public NamedPipeClientConnection(
            NamedPipeConnectionEndPoint connectionEndPoint)
            : base(connectionEndPoint?.ConnectionSettings ?? new ClientConnectionSettings(keepAliveMilliseconds: 0 /*by default named pipe does't require keep alive messages*/))
        {
            _clientConnectionSettings = (ClientConnectionSettings)_connectionSettings;
            _remoteNamedPipeName = connectionEndPoint.RemoteEndPointName ?? throw new ArgumentNullException(nameof(connectionEndPoint.RemoteEndPointName));
            _remoteNamedPipeHost = connectionEndPoint.RemoteEndPointHost ?? throw new ArgumentNullException(nameof(connectionEndPoint.RemoteEndPointName));
        }

        protected override bool IsStreamConnected => (_pipeClientIn?.IsConnected).GetValueOrDefault() && (_pipeClientOut?.IsConnected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _pipeClientIn =
                    new NamedPipeClientStream(_remoteNamedPipeHost, _remoteNamedPipeName + "_OUT",
                        PipeDirection.In, PipeOptions.Asynchronous,
                        TokenImpersonationLevel.Impersonation);

            _pipeClientOut =
                new NamedPipeClientStream(_remoteNamedPipeHost, _remoteNamedPipeName + "_IN",
                    PipeDirection.Out, PipeOptions.Asynchronous,
                    TokenImpersonationLevel.Impersonation);

            cancellationToken.ThrowIfCancellationRequested();

            if (_connectionStateMachine.State == ConnectionState.Connecting)
            {
                await _pipeClientIn.ConnectAsync(_clientConnectionSettings.ReconnectionDelayMilliseconds, cancellationToken);
                await _pipeClientOut.ConnectAsync(_clientConnectionSettings.ReconnectionDelayMilliseconds, cancellationToken);
            }
            else
            {
                await _pipeClientIn.ConnectAsync(cancellationToken);
                await _pipeClientOut.ConnectAsync(cancellationToken);
            }

            _connectedStream = new NamedPipeConnectedStream(_pipeClientIn, _pipeClientOut);
        }

        protected override void OnDisconnect()
        {
            base.OnDisconnect();

            if (State == ConnectionState.LinkError && _clientConnectionSettings.AutoReconnect)
                BeginConnection();
        }
    }
}
