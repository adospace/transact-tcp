using ServiceActor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public static class ConnectionFactory
    {
        public static IConnection CreateServer(IPEndPoint localEndPoint, ConnectionSettings connectionSettings = null) 
            => ServiceRef.Create<IConnection>(new ServerConnection(localEndPoint, connectionSettings));

        public static IConnection CreateServer(int localPort, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Any, localPort), connectionSettings);

        public static IConnection CreateServer(string localIp, int localPort, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Parse(localIp), localPort), connectionSettings);

        public static IConnection CreateServer(IPAddress localIp, int localPort, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(localIp, localPort), connectionSettings);

        public static IConnection CreateClient(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new ClientConnection(remoteEndPoint, localEndPoint, connectionSettings));

        public static IConnection CreateClient(string remoteIp, int remotePort, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(IPAddress.Parse(remoteIp), remotePort), localEndPoint, connectionSettings);

        public static IConnection CreateClient(IPAddress remoteIp, int remotePort, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(remoteIp, remotePort), localEndPoint, connectionSettings);

        public static IConnection CreateRedundantServer(IPEndPoint[] localEndPoints, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(localEndPoints.Select(ipEndpoint => CreateServer(ipEndpoint, connectionSettings)).ToArray()));

        public static IConnection CreateRedundantServer(IPAddress[] localAddresses, int localPort, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(localAddresses.Select(ipAddress => CreateServer(ipAddress, localPort, connectionSettings: connectionSettings)).ToArray()));

        public static IConnection CreateRedundantClient(IPEndPoint[] remoteEndPoints, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(remoteEndPoints.Select(ipEndpoint => CreateClient(ipEndpoint, connectionSettings: connectionSettings)).ToArray()));

        public static IConnection CreateRedundantClient(IPAddress[] remoteAddresses, int remotePort, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(remoteAddresses.Select(ipAddress => CreateClient(ipAddress, remotePort, connectionSettings: connectionSettings)).ToArray()));

    }
}
