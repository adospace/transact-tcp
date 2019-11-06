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
            bool useBufferedStream = false
            )
        {
            if (keepAliveMilliseconds < 0) //->0 to disable keep alive
            {
                throw new ArgumentOutOfRangeException(nameof(keepAliveMilliseconds));
            }

            KeepAliveMilliseconds = keepAliveMilliseconds;
            UseBufferedStream = useBufferedStream;
        }

        /// <summary>
        /// Time to wait before send a keep alive to other peer
        /// </summary>
        public int KeepAliveMilliseconds { get; }

        /// <summary>
        /// Indicates if connected strema should be wrapped with a <see cref="System.IO.BufferedStream"/> (False by default)
        /// </summary>
        public bool UseBufferedStream { get; }
    }
}
