using ServiceActor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TransactTcp.Ssl;

namespace TransactTcp
{
    public static class ConnectionFactory
    {
        public static IConnection CreateServer(ConnectionEndPoint connectionEndPoint)
            => ServiceRef.Create<IConnection>(new ServerConnection(connectionEndPoint));
        
        public static IConnection CreateServer(IPEndPoint localEndPoint, ConnectionSettings connectionSettings = null) 
            => CreateServer(new ConnectionEndPoint(localEndPoint: localEndPoint, connectionSettings: connectionSettings));

        public static IConnection CreateServer(int localPort, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Any, localPort), connectionSettings);

        public static IConnection CreateServer(string localIp, int localPort, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Parse(localIp), localPort), connectionSettings);

        public static IConnection CreateServer(IPAddress localIp, int localPort, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(localIp, localPort), connectionSettings);


        public static IConnection CreateClient(ConnectionEndPoint endPoint, IPEndPoint localEndPoint = null)
            => ServiceRef.Create<IConnection>(new ClientConnection(endPoint, localEndPoint));

        public static IConnection CreateClient(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new ConnectionEndPoint(remoteEndPoint: remoteEndPoint, connectionSettings: connectionSettings), localEndPoint);

        public static IConnection CreateClient(string remoteIp, int remotePort, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(IPAddress.Parse(remoteIp), remotePort), localEndPoint, connectionSettings);

        public static IConnection CreateClient(IPAddress remoteIp, int remotePort, IPEndPoint localEndPoint = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(remoteIp, remotePort), localEndPoint, connectionSettings);


        public static IConnection CreateSslServer(SslConnectionEndPoint connectionEndPoint)
            => ServiceRef.Create<IConnection>(new SslServerConnection(connectionEndPoint));

        public static IConnection CreateSslServer(IPEndPoint localEndPoint, SslConnectionSettings connectionSettings = null)
            => CreateSslServer(new SslConnectionEndPoint(localEndPoint: localEndPoint, connectionSettings: connectionSettings));

        public static IConnection CreateSslServer(int localPort, SslConnectionSettings connectionSettings = null)
            => CreateSslServer(new IPEndPoint(IPAddress.Any, localPort), connectionSettings);

        public static IConnection CreateSslServer(string localIp, int localPort, SslConnectionSettings connectionSettings = null)
            => CreateSslServer(new IPEndPoint(IPAddress.Parse(localIp), localPort), connectionSettings);

        public static IConnection CreateSslServer(IPAddress localIp, int localPort, SslConnectionSettings connectionSettings = null)
            => CreateSslServer(new IPEndPoint(localIp, localPort), connectionSettings);


        public static IConnection CreateSslClient(SslConnectionEndPoint endPoint, IPEndPoint localEndPoint = null)
            => ServiceRef.Create<IConnection>(new SslClientConnection(endPoint, localEndPoint));

        public static IConnection CreateSslClient(IPEndPoint remoteEndPoint, IPEndPoint localEndPoint = null, SslConnectionSettings connectionSettings = null)
            => CreateSslClient(new SslConnectionEndPoint(remoteEndPoint: remoteEndPoint, connectionSettings: connectionSettings), localEndPoint);

        public static IConnection CreateSslClient(string remoteIp, int remotePort, IPEndPoint localEndPoint = null, SslConnectionSettings connectionSettings = null)
            => CreateSslClient(new IPEndPoint(IPAddress.Parse(remoteIp), remotePort), localEndPoint, connectionSettings);

        public static IConnection CreateSslClient(IPAddress remoteIp, int remotePort, IPEndPoint localEndPoint = null, SslConnectionSettings connectionSettings = null)
            => CreateSslClient(new IPEndPoint(remoteIp, remotePort), localEndPoint, connectionSettings);


        public static IConnection CreateRedundantServer(ConnectionEndPoint[] endPoints)
            => ServiceRef.Create<IConnection>(new RedundantConnection(endPoints.Select(endpoint => CreateServer(endpoint)).ToArray()));

        public static IConnection CreateRedundantServer(IPEndPoint[] localEndPoints, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(localEndPoints.Select(ipEndpoint => CreateServer(ipEndpoint, connectionSettings)).ToArray()));

        public static IConnection CreateRedundantServer(IPAddress[] localAddresses, int localPort, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(localAddresses.Select(localAddress => CreateServer(localAddress, localPort, connectionSettings: connectionSettings)).ToArray()));


        public static IConnection CreateRedundantClient(ConnectionEndPoint[] endPoints)
            => ServiceRef.Create<IConnection>(new RedundantConnection(endPoints.Select(endpoint => CreateClient(endpoint)).ToArray()));

        public static IConnection CreateRedundantClient(IPEndPoint[] remoteEndPoints, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(remoteEndPoints.Select(ipEndpoint => CreateClient(ipEndpoint, connectionSettings: connectionSettings)).ToArray()));

        public static IConnection CreateRedundantClient(IPAddress[] remoteAddresses, int remotePort, ConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(remoteAddresses.Select(remoteAddress => CreateClient(remoteAddress, remotePort, connectionSettings: connectionSettings)).ToArray()));

        public static IConnectionListener CreateMultiPeerServer(IPEndPoint localEndPoint, ConnectionListenerSettings settings = null)
            => ServiceRef.Create<IConnectionListener>(new ConnectionListener(localEndPoint, settings));

        public static IConnectionListener CreateMultiPeerServer(int localPort, ConnectionListenerSettings settings = null)
            => CreateMultiPeerServer(new IPEndPoint(IPAddress.Any, localPort), settings);

        public static IConnectionListener CreateMultiPeerServer(string localIp, int localPort, ConnectionListenerSettings settings = null)
            => CreateMultiPeerServer(new IPEndPoint(IPAddress.Parse(localIp), localPort), settings);

        public static IConnectionListener CreateMultiPeerServer(IPAddress localIp, int localPort, ConnectionListenerSettings settings = null)
            => CreateMultiPeerServer(new IPEndPoint(localIp, localPort), settings);

    }
}
