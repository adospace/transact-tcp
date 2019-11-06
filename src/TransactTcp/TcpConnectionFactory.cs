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
    public static class TcpConnectionFactory
    {
        private static IConnection CreateServer(TcpConnectionEndPoint connectionEndPoint)
            => ServiceRef.Create<IConnection>(new TcpServerConnection(connectionEndPoint));
        
        public static IConnection CreateServer(IPEndPoint localEndPoint, ServerConnectionSettings connectionSettings = null) 
            => CreateServer(new TcpConnectionEndPoint(localEndPoint: localEndPoint, connectionSettings: connectionSettings));

        public static IConnection CreateServer(int localPort, ServerConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Any, localPort), connectionSettings);

        public static IConnection CreateServer(string localIp, int localPort, ServerConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Parse(localIp), localPort), connectionSettings);

        public static IConnection CreateServer(IPAddress localIp, int localPort, ServerConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(localIp, localPort), connectionSettings);


        private static IConnection CreateClient(TcpConnectionEndPoint endPoint)
            => ServiceRef.Create<IConnection>(new TcpClientConnection(endPoint));

        public static IConnection CreateClient(IPEndPoint remoteEndPoint, ClientConnectionSettings connectionSettings = null)
            => CreateClient(new TcpConnectionEndPoint(remoteEndPoint: remoteEndPoint, connectionSettings: connectionSettings));

        public static IConnection CreateClient(string remoteIp, int remotePort, ClientConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(IPAddress.Parse(remoteIp), remotePort), connectionSettings);

        public static IConnection CreateClient(IPAddress remoteIp, int remotePort, ClientConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(remoteIp, remotePort), connectionSettings);


        private static IConnection CreateSslServer(TcpConnectionEndPoint endPoint)
            => ServiceRef.Create<IConnection>(new SslTcpServerConnection(endPoint));

        public static IConnection CreateSslServer(IPEndPoint localEndPoint, SslServerConnectionSettings connectionSettings = null)
            => CreateSslServer(new TcpConnectionEndPoint(localEndPoint: localEndPoint, connectionSettings: connectionSettings));

        public static IConnection CreateSslServer(int localPort, SslServerConnectionSettings connectionSettings = null)
            => CreateSslServer(new IPEndPoint(IPAddress.Any, localPort), connectionSettings);

        public static IConnection CreateSslServer(string localIp, int localPort, SslServerConnectionSettings connectionSettings = null)
            => CreateSslServer(new IPEndPoint(IPAddress.Parse(localIp), localPort), connectionSettings);

        public static IConnection CreateSslServer(IPAddress localIp, int localPort, SslServerConnectionSettings connectionSettings = null)
            => CreateSslServer(new IPEndPoint(localIp, localPort), connectionSettings);


        private static IConnection CreateSslClient(TcpConnectionEndPoint endPoint)
            => ServiceRef.Create<IConnection>(new SslTcpClientConnection(endPoint));

        public static IConnection CreateSslClient(IPEndPoint remoteEndPoint, SslClientConnectionSettings connectionSettings = null)
            => CreateSslClient(new TcpConnectionEndPoint(remoteEndPoint: remoteEndPoint, connectionSettings: connectionSettings));

        public static IConnection CreateSslClient(string remoteIp, int remotePort, SslClientConnectionSettings connectionSettings = null)
            => CreateSslClient(new IPEndPoint(IPAddress.Parse(remoteIp), remotePort), connectionSettings);

        public static IConnection CreateSslClient(IPAddress remoteIp, int remotePort, SslClientConnectionSettings connectionSettings = null)
            => CreateSslClient(new IPEndPoint(remoteIp, remotePort), connectionSettings);


        private static IConnection CreateRedundantServer(TcpConnectionEndPoint[] endPoints)
            => ServiceRef.Create<IConnection>(new RedundantConnection(endPoints.Select(endpoint => CreateServer(endpoint)).ToArray()));

        public static IConnection CreateRedundantServer(IPEndPoint[] localEndPoints, ServerConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(localEndPoints.Select(ipEndpoint => CreateServer(ipEndpoint, connectionSettings)).ToArray()));

        public static IConnection CreateRedundantServer(IPAddress[] localAddresses, int localPort, ServerConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(localAddresses.Select(localAddress => CreateServer(localAddress, localPort, connectionSettings: connectionSettings)).ToArray()));


        private static IConnection CreateRedundantClient(TcpConnectionEndPoint[] endPoints)
            => ServiceRef.Create<IConnection>(new RedundantConnection(endPoints.Select(endpoint => CreateClient(endpoint)).ToArray()));

        public static IConnection CreateRedundantClient(IPEndPoint[] remoteEndPoints, ClientConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(remoteEndPoints.Select(ipEndpoint => CreateClient(ipEndpoint, connectionSettings: connectionSettings)).ToArray()));

        public static IConnection CreateRedundantClient(IPAddress[] remoteAddresses, int remotePort, ClientConnectionSettings connectionSettings = null)
            => ServiceRef.Create<IConnection>(new RedundantConnection(remoteAddresses.Select(remoteAddress => CreateClient(remoteAddress, remotePort, connectionSettings: connectionSettings)).ToArray()));

        public static IConnectionListener CreateMultiPeerServer(IPEndPoint localEndPoint, ConnectionListenerSettings settings = null)
            => ServiceRef.Create<IConnectionListener>(new TcpConnectionListener(localEndPoint, settings));

        public static IConnectionListener CreateMultiPeerServer(int localPort, ConnectionListenerSettings settings = null)
            => CreateMultiPeerServer(new IPEndPoint(IPAddress.Any, localPort), settings);

        public static IConnectionListener CreateMultiPeerServer(string localIp, int localPort, ConnectionListenerSettings settings = null)
            => CreateMultiPeerServer(new IPEndPoint(IPAddress.Parse(localIp), localPort), settings);

        public static IConnectionListener CreateMultiPeerServer(IPAddress localIp, int localPort, ConnectionListenerSettings settings = null)
            => CreateMultiPeerServer(new IPEndPoint(localIp, localPort), settings);

    }
}
