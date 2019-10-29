using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TransactTcp.Ssl
{
    public class SslTcpConnectionEndPoint : TcpConnectionEndPoint
    {
        public SslTcpConnectionEndPoint(IPEndPoint localEndPoint = null, IPEndPoint remoteEndPoint = null, SslConnectionSettings connectionSettings = null)
            : base(localEndPoint, remoteEndPoint, connectionSettings)
        {
            SslConnectionSettings = connectionSettings;
        }

        public SslTcpConnectionEndPoint(IPAddress localAddress, int localPort, IPAddress remoteAddress, int remotePort, SslConnectionSettings connectionSettings = null)
            : base(localAddress, localPort, remoteAddress, remotePort, connectionSettings)
        {
            SslConnectionSettings = connectionSettings;
        }

        public SslConnectionSettings SslConnectionSettings { get; } = new SslConnectionSettings();
    }
}
