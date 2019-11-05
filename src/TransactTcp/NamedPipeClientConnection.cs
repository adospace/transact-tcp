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
        private NamedPipeClientStream _pipeClient;

        public NamedPipeClientConnection(
            NamedPipeConnectionEndPoint connectionEndPoint)
            : base(connectionEndPoint?.ConnectionSettings ?? new ConnectionSettings(keepAliveMilliseconds: 0 /*by default named pipe does't require keep alive messages*/))
        {
            _remoteNamedPipeName = connectionEndPoint.RemoteEndPointName ?? throw new ArgumentNullException(nameof(connectionEndPoint.RemoteEndPointName));
            _remoteNamedPipeHost = connectionEndPoint.RemoteEndPointHost ?? throw new ArgumentNullException(nameof(connectionEndPoint.RemoteEndPointName));
        }

        protected override bool IsStreamConnected => (_pipeClient?.IsConnected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationTokenSource cancellationTokenSource)
        {
            var cancellationToken = cancellationTokenSource.Token;

            _pipeClient =
                    new NamedPipeClientStream(_remoteNamedPipeHost, _remoteNamedPipeName,
                        PipeDirection.InOut, PipeOptions.Asynchronous,
                        TokenImpersonationLevel.Impersonation);

            cancellationToken.ThrowIfCancellationRequested();

            if (_connectionStateMachine.State == ConnectionState.Connecting)
            {
                await _pipeClient.ConnectAsync(_connectionSettings.ReconnectionDelayMilliseconds, cancellationToken);
            }
            else
            {
                await _pipeClient.ConnectAsync(cancellationToken);
            }

            _connectedStream = _pipeClient;
        }
    }
}
