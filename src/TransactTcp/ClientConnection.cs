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
    internal class ClientConnection : Connection
    {
        private readonly IPEndPoint _localEndPoint;
        private TcpClient _tcpClient;

        public ClientConnection(
            ConnectionEndPoint connectionEndPoint,
            IPEndPoint localEndPoint = null)
            : base(connectionEndPoint.RemoteEndPoint, connectionEndPoint.ConnectionSettings)
        {
            _localEndPoint = localEndPoint;
        }

        protected override bool IsStreamConnected => (_tcpClient?.Connected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _tcpClient = _localEndPoint == null ? new TcpClient() : new TcpClient(_localEndPoint);
            var cancellationCompletionSource = new TaskCompletionSource<bool>();

            using var reconnectionDelayEvent = new AutoResetEvent(false);
            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var connectTask = _tcpClient.ConnectAsync(_endPoint.Address, _endPoint.Port);

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
                        _tcpClient.ReceiveTimeout = _connectionSettings.KeepAliveMilliseconds * 2;
                        _connectedStream = await CreateConnectedStreamAsync(_tcpClient, cancellationToken);
                        break;
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
