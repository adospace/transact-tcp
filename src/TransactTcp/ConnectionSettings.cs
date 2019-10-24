using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TransactTcp
{
    public class ConnectionSettings
    {
        private static bool ValidateServerCertificate(
#pragma warning disable IDE0060 // Remove unused parameter
              object sender,
              X509Certificate certificate,
              X509Chain chain,
#pragma warning restore IDE0060 // Remove unused parameter
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        public ConnectionSettings(
            int keepAliveMilliseconds = 500,
            int reconnectionDelayMilliseconds = 1000,
            bool sslConnection = false,
            X509Certificate sslCertificate = null,
            bool sslClientCertificateRequired = false,
            SslProtocols sslEnabledProtocols = SslProtocols.Default,
            bool sslCheckCertificateRevocation = false,
            Func<
              object,
              X509Certificate,
              X509Chain,
              SslPolicyErrors,
            bool> sslValidateServerCertificateCallback = null
            )
        {
            if (keepAliveMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keepAliveMilliseconds));
            }

            if (reconnectionDelayMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reconnectionDelayMilliseconds));
            }

            KeepAliveMilliseconds = keepAliveMilliseconds;
            ReconnectionDelayMilliseconds = reconnectionDelayMilliseconds;
            SslConnection = sslConnection;
            SslClientCertificateRequired = sslClientCertificateRequired;
            SslEnabledProtocols = sslEnabledProtocols;
            SslCheckCertificateRevocation = sslCheckCertificateRevocation;
            SslValidateServerCertificateCallback = sslValidateServerCertificateCallback ?? ValidateServerCertificate;

            if (SslConnection)
            {
                SslCertificate = sslCertificate ?? throw new ArgumentException("Ssl connection requires a certificate");
            }
        }


        public static ConnectionSettings Default { get; } = new ConnectionSettings();

        public int KeepAliveMilliseconds { get; }

        public int ReconnectionDelayMilliseconds { get; }

        public bool SslConnection { get; }

        public X509Certificate SslCertificate { get; }

        public bool SslClientCertificateRequired { get; }

        public SslProtocols SslEnabledProtocols { get;  }

        public bool SslCheckCertificateRevocation { get; }

        public Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> SslValidateServerCertificateCallback { get; }
        
        public string SslServerHost { get; }
    }
}
