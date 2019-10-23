using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public static class ConnectionFactory
    {
        public static IConnection CreateServer(IPEndPoint remoteEndPoint, ConnectionSettings connectionSettings = null) 
            => ServiceActor.ServiceRef.Create<IConnection>(new ServerConnection(remoteEndPoint, connectionSettings));

        public static IConnection CreateServer(int port, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Any, port), connectionSettings);

        public static IConnection CreateServer(string localIp, int port, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Parse(localIp), port), connectionSettings);

        public static IConnection CreateServer(IPAddress localIp, int port, Action<IConnection, byte[]> receivedDataAction = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(localIp, port), connectionSettings);

        public static IConnection CreateClient(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => ServiceActor.ServiceRef.Create<IConnection>(new ClientConnection(remoteEndPoint, localEndPoint, connectionSettings));

        public static IConnection CreateClient(string remoteIp, int port, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(IPAddress.Parse(remoteIp), port), localEndPoint, connectionSettings);

        public static IConnection CreateClient(IPAddress remoteIp, int port, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(remoteIp, port), localEndPoint, connectionSettings);

    }
}
