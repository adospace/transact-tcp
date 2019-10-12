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
        public static IConnection CreateServer(IPEndPoint remoteEndPoint, Action<IConnection, byte[]> receivedDataAction, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null) 
            => ServiceActor.ServiceRef.Create<IConnection>(new ServerConnection(remoteEndPoint, receivedDataAction, connectionStateChangedAction));

        public static IConnection CreateServer(IPEndPoint remoteEndPoint, Func<IConnection, byte[], CancellationToken, Task> receivedDataActionAsync, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => ServiceActor.ServiceRef.Create<IConnection>(new ServerConnection(remoteEndPoint, receivedDataActionAsync, connectionStateChangedAction));

        public static IConnection CreateServer(int port, Action<IConnection, byte[]> receivedDataAction, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Any, port), receivedDataAction, connectionStateChangedAction);

        public static IConnection CreateServer(string localIp, int port, Action<IConnection, byte[]> receivedDataAction, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Parse(localIp), port), receivedDataAction, connectionStateChangedAction);

        public static IConnection CreateServer(IPAddress localIp, int port, Action<IConnection, byte[]> receivedDataAction, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(localIp, port), receivedDataAction, connectionStateChangedAction);

        public static IConnection CreateServer(int port, Func<IConnection, byte[], CancellationToken, Task> receivedDataActionAsync, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Any, port), receivedDataActionAsync, connectionStateChangedAction);

        public static IConnection CreateServer(string localIp, int port, Func<IConnection, byte[], CancellationToken, Task> receivedDataActionAsync, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(IPAddress.Parse(localIp), port), receivedDataActionAsync, connectionStateChangedAction);

        public static IConnection CreateServer(IPAddress localIp, int port, Func<IConnection, byte[], CancellationToken, Task> receivedDataActionAsync, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateServer(new IPEndPoint(localIp, port), receivedDataActionAsync, connectionStateChangedAction);

        public static IConnection CreateClient(IPEndPoint remoteEndPoint, Action<IConnection, byte[]> receivedAction, IPEndPoint localEndPoint = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => ServiceActor.ServiceRef.Create<IConnection>(new ClientConnection(remoteEndPoint, receivedAction, localEndPoint, connectionStateChangedAction));

        public static IConnection CreateClient(IPEndPoint remoteEndPoint, Func<IConnection, byte[], CancellationToken, Task> receivedDataActionAsync, IPEndPoint localEndPoint = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => ServiceActor.ServiceRef.Create<IConnection>(new ClientConnection(remoteEndPoint, receivedDataActionAsync, localEndPoint, connectionStateChangedAction));

        public static IConnection CreateClient(string remoteIp, int port, Action<IConnection, byte[]> receivedAction, IPEndPoint localEndPoint = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(IPAddress.Parse(remoteIp), port), receivedAction, localEndPoint, connectionStateChangedAction);

        public static IConnection CreateClient(IPAddress remoteIp, int port, Action<IConnection, byte[]> receivedAction, IPEndPoint localEndPoint = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(remoteIp, port), receivedAction, localEndPoint, connectionStateChangedAction);

        public static IConnection CreateClient(string remoteIp, int port, Func<IConnection, byte[], CancellationToken, Task> receivedDataActionAsync, IPEndPoint localEndPoint = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(IPAddress.Parse(remoteIp), port), receivedDataActionAsync, localEndPoint, connectionStateChangedAction);

        public static IConnection CreateClient(IPAddress remoteIp, int port, Func<IConnection, byte[], CancellationToken, Task> receivedDataActionAsync, IPEndPoint localEndPoint = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null, ConnectionSettings connectionSettings = null)
            => CreateClient(new IPEndPoint(remoteIp, port), receivedDataActionAsync, localEndPoint, connectionStateChangedAction);

    }
}
