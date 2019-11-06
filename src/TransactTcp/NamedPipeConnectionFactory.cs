using ServiceActor;
using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    public static class NamedPipeConnectionFactory
    {
        private static IConnection CreateServer(NamedPipeConnectionEndPoint connectionEndPoint)
            => ServiceRef.Create<IConnection>(new NamedPipeServerConnection(connectionEndPoint));

        public static IConnection CreateServer(string localNamedPipeName, ServerConnectionSettings connectionSettings = null)
            => CreateServer(new NamedPipeConnectionEndPoint(localNamedPipeName: localNamedPipeName, connectionSettings: connectionSettings));


        private static IConnection CreateClient(NamedPipeConnectionEndPoint connectionEndPoint)
            => ServiceRef.Create<IConnection>(new NamedPipeClientConnection(connectionEndPoint));

        public static IConnection CreateClient(string remoteEndPointName, ClientConnectionSettings connectionSettings = null)
            => CreateClient(new NamedPipeConnectionEndPoint(remoteEndPointName: remoteEndPointName, connectionSettings: connectionSettings));

        public static IConnection CreateClient(string remoteEndPointHost, string remoteEndPointName, ClientConnectionSettings connectionSettings = null)
            => CreateClient(new NamedPipeConnectionEndPoint(remoteEndPointHost: remoteEndPointHost,  remoteEndPointName: remoteEndPointName, connectionSettings: connectionSettings));


        public static IConnectionListener CreateMultiPeerServer(string localNamedPipeName, ConnectionListenerSettings settings = null)
            => ServiceRef.Create<IConnectionListener>(new NamedPipeConnectionListener(localNamedPipeName, settings));

    }
}
