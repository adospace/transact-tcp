using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class TcpClientConnection : Connection
    {
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private TcpClient _tcpClient;

        public TcpClientConnection(
            TcpConnectionEndPoint connectionEndPoint)
            : base(connectionEndPoint?.ConnectionSettings)
        {
            _remoteEndPoint = connectionEndPoint.RemoteEndPoint ?? throw new ArgumentNullException(nameof(connectionEndPoint.RemoteEndPoint));
            _localEndPoint = connectionEndPoint.LocalEndPoint;
        }

        protected override bool IsStreamConnected => (_tcpClient?.Connected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationTokenSource cancellationTokenSource)
        {
            var cancellationToken = cancellationTokenSource.Token;

            _tcpClient = _localEndPoint == null ? new TcpClient() : new TcpClient(_localEndPoint);
            var cancellationCompletionSource = new TaskCompletionSource<bool>();

            using var reconnectionDelayEvent = new AutoResetEvent(false);
            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var connectTask = _tcpClient.ConnectAsync(_remoteEndPoint.Address, _remoteEndPoint.Port);

                    using (cancellationToken.Register(() =>
                    {
                        reconnectionDelayEvent.Set();
                        cancellationCompletionSource.TrySetResult(true);
                    }))
                    {
                        if (connectTask != await Task.WhenAny(connectTask, cancellationCompletionSource.Task))
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }
                    }

                    if (!connectTask.IsFaulted)
                    {
                        _connectedStream = await CreateConnectedStreamAsync(_tcpClient, cancellationToken);
                        break;
                    }

                    if (_connectionStateMachine.State == ConnectionState.Connecting)
                    {
                        throw new SocketException();
                    }

                    if (reconnectionDelayEvent.WaitOne(_connectionSettings.ReconnectionDelayMilliseconds))
                        break;
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        protected virtual Task<Stream> CreateConnectedStreamAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(tcpClient.GetStream());
        }

        protected override void OnDisconnect()
        {
            base.OnDisconnect();

            _tcpClient?.Close();
            _tcpClient = null;
        }
    }
}
