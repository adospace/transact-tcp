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
            bool useBufferedStream = false,
            bool enableMessageFraming = true
            )
        {
            if (keepAliveMilliseconds < 0) //->0 to disable keep alive
            {
                throw new ArgumentOutOfRangeException(nameof(keepAliveMilliseconds));
            }

            KeepAliveMilliseconds = keepAliveMilliseconds;
            UseBufferedStream = useBufferedStream;
            EnableMessageFraming = enableMessageFraming;
        }

        /// <summary>
        /// Time to wait before send a keep alive to other peer
        /// </summary>
        public int KeepAliveMilliseconds { get; }

        /// <summary>
        /// Indicates if connected stream should be wrapped with a <see cref="System.IO.BufferedStream"/> (False by default)
        /// </summary>
        public bool UseBufferedStream { get; }

        /// <summary>
        /// Indicates if connection should automatically manage the message framing
        /// </summary>
        /// <remarks>Note that both peer should consistent <see cref="EnableMessageFraming"/> setting (both either true or false).
        /// Named pipe transport doesn't support mesage framing and this settings doesn't have effect.
        /// Disabling mesage framing also disable keep alive management.</remarks>
        public bool EnableMessageFraming { get; }
    }
}
