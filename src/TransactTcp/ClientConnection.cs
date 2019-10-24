using System;
using System.Collections.Generic;
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

            using (var reconnectionDelayEvent = new AutoResetEvent(false))
            {
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

                        _tcpClient.ReceiveTimeout = _connectionSettings.KeepAliveMilliseconds * 2;

                        if (!_connectionSettings.SslConnection)
                        {
                            _streamToClient = _tcpClient.GetStream();
                        }
                        else
                        {
                            var sslStream = new SslStream(
                                _tcpClient.GetStream(),
                                true,
                                _connectionSettings.SslValidateCertificateCallback == null ? null : new RemoteCertificateValidationCallback(_connectionSettings.SslValidateCertificateCallback),
                                null
                                );

                            await sslStream.AuthenticateAsClientAsync(
                                _connectionSettings.SslServerHost,
                                _connectionSettings.SslCertificate == null ? null : new X509CertificateCollection(new[] { _connectionSettings.SslCertificate }),
                                _connectionSettings.SslEnabledProtocols,
                                _connectionSettings.SslCheckCertificateRevocation);

                            cancellationToken.ThrowIfCancellationRequested();

                            _streamToClient = sslStream;
                        }

                        if (!connectTask.IsFaulted)
                            break;

                        if (reconnectionDelayEvent.WaitOne(_connectionSettings.ReconnectionDelayMilliseconds))
                            break;
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
        }

        protected override void OnDisconnect()
        {
            base.OnDisconnect();

            _tcpClient?.Close();
            _tcpClient = null;
        }
    }
}
