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
            bool autoReconnect = false
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
            AutoReconnect = autoReconnect;
        }

        public int KeepAliveMilliseconds { get; }

        public int ReconnectionDelayMilliseconds { get; }

        public bool AutoReconnect { get; internal set; }
    }
}
