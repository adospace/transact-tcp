using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp.Ssl
{
    internal class SslTcpServerConnection : TcpServerConnection
    {
        private readonly SslConnectionSettings _sslConnectionSettings;
        private readonly Action<IConnection, AuthenticationException> _onAuthenticationException;

        public SslTcpServerConnection(
            SslTcpConnectionEndPoint connectionEndPoint,
            Action<IConnection, AuthenticationException> onAuthenticationException = null)
            : base(connectionEndPoint)
        {
            _sslConnectionSettings = connectionEndPoint.SslConnectionSettings ?? new SslConnectionSettings();
            _onAuthenticationException = onAuthenticationException;
        }

        protected override async Task<Stream> CreateConnectedStreamAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var sslStream = new SslStream(
                _tcpToClient.GetStream(),
                true,
                _sslConnectionSettings.SslValidateCertificateCallback == null ? null : new RemoteCertificateValidationCallback(_sslConnectionSettings.SslValidateCertificateCallback),
                null);

            try
            {
                await sslStream.AuthenticateAsServerAsync(
                    _sslConnectionSettings.SslCertificate,
                    _sslConnectionSettings.SslClientCertificateRequired,
                    _sslConnectionSettings.SslEnabledProtocols,
                    _sslConnectionSettings.SslCheckCertificateRevocation);
            }
            catch (AuthenticationException ex)
            {
                _onAuthenticationException?.Invoke(this, ex);
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();

            return sslStream;
        }
    }
}
