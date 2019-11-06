using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    public class ServerConnectionSettings : ConnectionSettings
    {
        public ServerConnectionSettings(int keepAliveMilliseconds = 500,
            int connectionTimeoutMilliseconds = 10000)
            : base(keepAliveMilliseconds)
        {
            if (connectionTimeoutMilliseconds < 0) //->0 to disable keep alive
            {
                throw new ArgumentOutOfRangeException(nameof(keepAliveMilliseconds));
            }

            ConnectionTimeoutMilliseconds = connectionTimeoutMilliseconds;
        }

        public int ConnectionTimeoutMilliseconds { get; } = 10000;
    }
}
