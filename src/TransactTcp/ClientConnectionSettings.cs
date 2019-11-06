using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    public class ClientConnectionSettings : ConnectionSettings
    {
        public ClientConnectionSettings(int keepAliveMilliseconds = 500,
            int reconnectionDelayMilliseconds = 1000,
            bool autoReconnect = true,
            bool useBufferedStream = false)
            : base(keepAliveMilliseconds, useBufferedStream)
        {
            if (reconnectionDelayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reconnectionDelayMilliseconds));
            }
            ReconnectionDelayMilliseconds = reconnectionDelayMilliseconds;
            AutoReconnect = autoReconnect;
        }

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
