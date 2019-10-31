using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TransactTcp
{
    /// <summary>
    /// Enumerate connection settings
    /// </summary>
    public class ConnectionSettings
    {
        /// <summary>
        /// Construct a <see cref="ConnectionSettings"/> object
        /// </summary>
        /// <param name="keepAliveMilliseconds">Keep alive period, 0 to disable keep alive management</param>
        /// <param name="reconnectionDelayMilliseconds">Delay to wait before retry a connection to server</param>
        /// <param name="autoReconnect">Enable/disable automatic re-connection to server</param>
        public ConnectionSettings(
            int keepAliveMilliseconds = 500,
            int reconnectionDelayMilliseconds = 1000,
            bool autoReconnect = true
            )
        {
            if (keepAliveMilliseconds < 0) //->0 to disable keep alive
            {
                throw new ArgumentOutOfRangeException(nameof(keepAliveMilliseconds));
            }

            if (reconnectionDelayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reconnectionDelayMilliseconds));
            }

            KeepAliveMilliseconds = keepAliveMilliseconds;
            ReconnectionDelayMilliseconds = reconnectionDelayMilliseconds;
            AutoReconnect = autoReconnect;
        }

        /// <summary>
        /// Time to wait before send a keep alive to other peer
        /// </summary>
        public int KeepAliveMilliseconds { get; }

        /// <summary>
        /// Time to wait before try to re-connect to server
        /// </summary>
        public int ReconnectionDelayMilliseconds { get; }

        /// <summary>
        /// True if automatic reconnection is enabled
        /// </summary>
        public bool AutoReconnect { get; internal set; }
    }
}
