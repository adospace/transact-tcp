using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TransactTcp.Ssl
{
    public class SslClientConnectionSettings : ClientConnectionSettings
    {
        public SslClientConnectionSettings(
            int keepAliveMilliseconds = 500,
            int reconnectionDelayMilliseconds = 1000,
            bool autoReconnect = true,
            bool useBufferedStream = false,
            X509Certificate sslCertificate = null,
            bool sslClientCertificateRequired = false,
            SslProtocols sslEnabledProtocols = SslProtocols.Tls12,
            bool sslCheckCertificateRevocation = false,
            Func<
              object,
              X509Certificate,
              X509Chain,
              SslPolicyErrors,
            bool> sslValidateServerCertificateCallback = null,
            string sslServerHost = null
            )
            : base(keepAliveMilliseconds, reconnectionDelayMilliseconds, autoReconnect, useBufferedStream)
        {
            if (keepAliveMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(keepAliveMilliseconds));
            }

            SslCertificate = sslCertificate;
            SslClientCertificateRequired = sslClientCertificateRequired;
            SslEnabledProtocols = sslEnabledProtocols;
            SslCheckCertificateRevocation = sslCheckCertificateRevocation;
            SslValidateCertificateCallback = sslValidateServerCertificateCallback;
            SslServerHost = sslServerHost;
        }


        public X509Certificate SslCertificate { get; }

        public bool SslClientCertificateRequired { get; }

        public SslProtocols SslEnabledProtocols { get; }

        public bool SslCheckCertificateRevocation { get; }

        public Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> SslValidateCertificateCallback { get; }

        public string SslServerHost { get; }
    }
}
