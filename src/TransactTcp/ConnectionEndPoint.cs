using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TransactTcp
{
    public class ConnectionEndPoint
    {
        public ConnectionEndPoint(IPEndPoint localEndPoint = null, IPEndPoint remoteEndPoint = null, ConnectionSettings connectionSettings = null)
        {
            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
            ConnectionSettings = connectionSettings;
        }

        public ConnectionEndPoint(IPAddress localAddress, int localPort, IPAddress remoteAddress, int remotePort, ConnectionSettings connectionSettings = null)
        {
            LocalEndPoint = new IPEndPoint(localAddress, localPort);
            RemoteEndPoint = new IPEndPoint(remoteAddress, remotePort);
            ConnectionSettings = connectionSettings;
        }

        public ConnectionEndPoint(string localAddress, int localPort, string remoteAddress, int remotePort, ConnectionSettings connectionSettings = null)
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Parse(localAddress), localPort);
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort);
            ConnectionSettings = connectionSettings;
        }

        public IPEndPoint LocalEndPoint { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public ConnectionSettings ConnectionSettings { get; }
    }
}
