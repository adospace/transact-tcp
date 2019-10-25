using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TransactTcp
{
    public class SslConnectionEndPoint : ConnectionEndPoint
    {
        public SslConnectionEndPoint(IPEndPoint localEndPoint = null, IPEndPoint remoteEndPoint = null, SslConnectionSettings connectionSettings = null) 
            : base(localEndPoint, remoteEndPoint, connectionSettings)
        {
            SslConnectionSettings = connectionSettings;
        }

        public SslConnectionEndPoint(IPAddress localAddress, int localPort, IPAddress remoteAddress, int remotePort, SslConnectionSettings connectionSettings = null) 
            : base(localAddress, localPort, remoteAddress, remotePort, connectionSettings)
        {
            SslConnectionSettings = connectionSettings;
        }

        public SslConnectionSettings SslConnectionSettings { get; } = new SslConnectionSettings();
    }
}
