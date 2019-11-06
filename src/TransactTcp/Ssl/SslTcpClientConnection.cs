using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp.Ssl
{
    internal class SslTcpClientConnection : TcpClientConnection
    {
        private readonly SslClientConnectionSettings _sslConnectionSettings;
        private readonly Action<IConnection, AuthenticationException> _onAuthenticationException;

        public SslTcpClientConnection(
            TcpConnectionEndPoint connectionEndPoint,
            Action<IConnection, AuthenticationException> onAuthenticationException = null)
            : base(connectionEndPoint)
        {
            _sslConnectionSettings = ((SslClientConnectionSettings)connectionEndPoint.ConnectionSettings) ?? new SslClientConnectionSettings();
            _onAuthenticationException = onAuthenticationException;
        }

        protected override async Task<Stream> CreateConnectedStreamAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var sslStream = new SslStream(
                tcpClient.GetStream(),
                true,
                _sslConnectionSettings.SslValidateCertificateCallback == null ? null : new RemoteCertificateValidationCallback(_sslConnectionSettings.SslValidateCertificateCallback),
                null);

            try
            {
                await sslStream.AuthenticateAsClientAsync(
                    _sslConnectionSettings.SslServerHost,
                    _sslConnectionSettings.SslCertificate == null ? null : new X509CertificateCollection(new[] { _sslConnectionSettings.SslCertificate }),
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
