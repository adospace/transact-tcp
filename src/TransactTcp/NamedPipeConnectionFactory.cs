using ServiceActor;
using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    public static class NamedPipeConnectionFactory
    {
        public static IConnection CreateServer(NamedPipeConnectionEndPoint connectionEndPoint)
            => ServiceRef.Create<IConnection>(new NamedPipeServerConnection(connectionEndPoint));

        public static IConnection CreateServer(string localNamedPipeName, ConnectionSettings connectionSettings = null)
            => CreateServer(new NamedPipeConnectionEndPoint(localNamedPipeName: localNamedPipeName, connectionSettings: connectionSettings));


        public static IConnection CreateClient(NamedPipeConnectionEndPoint connectionEndPoint)
            => ServiceRef.Create<IConnection>(new NamedPipeClientConnection(connectionEndPoint));

        public static IConnection CreateClient(string remoteEndPointName, ConnectionSettings connectionSettings = null)
            => CreateClient(new NamedPipeConnectionEndPoint(remoteEndPointName: remoteEndPointName, connectionSettings: connectionSettings));

        public static IConnection CreateClient(string remoteEndPointHost, string remoteEndPointName, ConnectionSettings connectionSettings = null)
            => CreateClient(new NamedPipeConnectionEndPoint(remoteEndPointHost: remoteEndPointHost,  remoteEndPointName: remoteEndPointName, connectionSettings: connectionSettings));
    }
}
