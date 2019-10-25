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

namespace TransactTcp
{
    internal class SslClientConnection : ClientConnection
    {
        private readonly SslConnectionSettings _sslConnectionSettings;
        private readonly Action<IConnection, AuthenticationException> _onAuthenticationException;

        public SslClientConnection(
            SslConnectionEndPoint connectionEndPoint,
            IPEndPoint localEndPoint = null,
            Action<IConnection, AuthenticationException> onAuthenticationException = null) 
            : base(connectionEndPoint, localEndPoint)
        {
            _sslConnectionSettings = connectionEndPoint.SslConnectionSettings ?? SslConnectionSettings.Default;
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
