﻿using System;
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
        public ConnectionSettings(
            int keepAliveMilliseconds = 500,
            int reconnectionDelayMilliseconds = 1000,
            X509Certificate sslCertificate = null,
            bool sslClientCertificateRequired = false,
            SslProtocols sslEnabledProtocols = SslProtocols.Default,
            bool sslCheckCertificateRevocation = false,
            Func<
              object,
              X509Certificate,
              X509Chain,
              SslPolicyErrors,
            bool> sslValidateServerCertificateCallback = null,
            string sslServerHost = null
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
        }


        public static ConnectionSettings Default { get; } = new ConnectionSettings();

        public int KeepAliveMilliseconds { get; }

        public int ReconnectionDelayMilliseconds { get; }

    }
}
