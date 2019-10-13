using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    public class ConnectionSettings
    {
        public static ConnectionSettings Default { get; } = new ConnectionSettings();

        public int KeepAliveMilliseconds { get; set; } = 500;

        public int ReconnectionDelay { get; set; } = 1000;
    }
}
