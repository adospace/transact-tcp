using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Net;
using System.Threading;

namespace TransactTcp.Tests
{
    [TestClass]
    public class ConnectionBasicTests
    {
        [TestMethod]
        public void ServerAndClientShouldConnectAndDisconnectWithoutErrors()
        {
            using var serverStateChangedEvent = new AutoResetEvent(false);
            using var clientStateChangedEvent = new AutoResetEvent(false);

            var server = TcpConnectionFactory.CreateServer(15007);

            var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15007);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected)
                    serverStateChangedEvent.Set();
            });

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected)
                    clientStateChangedEvent.Set();
            });

            serverStateChangedEvent.WaitOne(100000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(100000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Connected);
            client.State.ShouldBe(ConnectionState.Connected);

            server.Stop();
            client.Stop();

            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Disconnected);
            client.State.ShouldBe(ConnectionState.Disconnected);
        }

        [TestMethod]
        public void CancelServerPendingConnectionShouldJustWork()
        {
            using var serverStateChangedEvent = new AutoResetEvent(false);
            var server = TcpConnectionFactory.CreateServer(15001);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Disconnected)
                    serverStateChangedEvent.Set();
            });

            server.Stop();

            serverStateChangedEvent.WaitOne(100000).ShouldBeTrue();
            server.State.ShouldBe(ConnectionState.Disconnected);
        }

        [TestMethod]
        public void CancelClientPendingConnectionShouldJustWork()
        {
            using var clientStateChangedEvent = new AutoResetEvent(false);
            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15002);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Disconnected)
                    clientStateChangedEvent.Set();
            });

            client.Stop();

            clientStateChangedEvent.WaitOne(100000).ShouldBeTrue();
            client.State.ShouldBe(ConnectionState.Disconnected);
        }

        [TestMethod]
        public void StartServerAfterClientShouldWork()
        {
            using var serverStateChangedEvent = new AutoResetEvent(false);
            using var clientStateChangedEvent = new AutoResetEvent(false);

            var server = TcpConnectionFactory.CreateServer(
                15003);

            var client = TcpConnectionFactory.CreateClient(
                IPAddress.Loopback,
                15003);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected)
                    clientStateChangedEvent.Set();
            });

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected)
                    serverStateChangedEvent.Set();
            });

            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Connected);
            client.State.ShouldBe(ConnectionState.Connected);

            server.Stop();
            client.Stop();

            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Disconnected);
            client.State.ShouldBe(ConnectionState.Disconnected);
        }

        [TestMethod]
        public void ClientShouldReconnectToServerAfterServerRestart()
        {
            using var serverStateChangedEvent = new AutoResetEvent(false);
            using var clientStateChangedEvent = new AutoResetEvent(false);

            var server = TcpConnectionFactory.CreateServer(15004);

            var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15004);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                    serverStateChangedEvent.Set();
            });

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                    clientStateChangedEvent.Set();
            });

            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Connected);
            client.State.ShouldBe(ConnectionState.Connected);

            server.Stop();

            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Disconnected);
            client.State.ShouldBe(ConnectionState.LinkError);

            server.Start();
            
            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.Stop();
            client.Stop();

            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Disconnected);
            client.State.ShouldBe(ConnectionState.Disconnected);

        }

        [TestMethod]
        public void NamedPipeServerAndClientShouldConnectAndDisconnectWithoutErrors()
        {
            using var serverStateChangedEvent = new AutoResetEvent(false);
            using var clientStateChangedEvent = new AutoResetEvent(false);

            using var server = NamedPipeConnectionFactory.CreateServer("NamedPipeServerAndClientShouldConnectAndDisconnectWithoutErrors");

            using var client = NamedPipeConnectionFactory.CreateClient("NamedPipeServerAndClientShouldConnectAndDisconnectWithoutErrors");

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected)
                    serverStateChangedEvent.Set();
            });

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected)
                    clientStateChangedEvent.Set();
            });

            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Connected);
            client.State.ShouldBe(ConnectionState.Connected);

            server.Stop();
            client.Stop();

            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Disconnected);
            client.State.ShouldBe(ConnectionState.Disconnected);
        }

        [TestMethod]
        public void ClientShouldMoveStateToLinkErrorIfServerDoesntExist()
        {
            var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15005);
            using var connectionLinkErrorEvent = new AutoResetEvent(false);
            using var connectionOkEvent = new AutoResetEvent(false);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.LinkError)
                    connectionLinkErrorEvent.Set();
                else if (toState == ConnectionState.Connected)
                    connectionOkEvent.Set();
            });

            connectionLinkErrorEvent.WaitOne(10000).ShouldBeTrue();

            var server = TcpConnectionFactory.CreateServer(15005);

            server.Start();

            connectionOkEvent.WaitOne(10000).ShouldBeTrue();
        }

        [TestMethod]
        public void NamedPipeClientShouldMoveStateToLinkErrorIfServerDoesntExist()
        {
            using var client = NamedPipeConnectionFactory.CreateClient("NamedPipeClientShouldMoveStateToLinkErrorIfServerDoesntExist");
            using var connectionLinkErrorEvent = new AutoResetEvent(false);
            using var connectionOkEvent = new AutoResetEvent(false);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.LinkError)
                    connectionLinkErrorEvent.Set();
                else if (toState == ConnectionState.Connected)
                    connectionOkEvent.Set();
            });

            connectionLinkErrorEvent.WaitOne(10000).ShouldBeTrue();

            using var server = NamedPipeConnectionFactory.CreateServer("NamedPipeClientShouldMoveStateToLinkErrorIfServerDoesntExist");

            server.Start();

            connectionOkEvent.WaitOne(10000).ShouldBeTrue();
        }

        [TestMethod]
        public void ServerShouldMoveStateToLinkErrorIfClientDoesntConnect()
        {
            var server = TcpConnectionFactory.CreateServer(15006, new ServerConnectionSettings(connectionTimeoutMilliseconds: 1000));
            using var connectionLinkErrorEvent = new AutoResetEvent(false);
            using var connectionOkEvent = new AutoResetEvent(false);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.LinkError)
                    connectionLinkErrorEvent.Set();
                else if (toState == ConnectionState.Connected)
                    connectionOkEvent.Set();
            });

            connectionLinkErrorEvent.WaitOne(10000).ShouldBeTrue();

            var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15006);

            client.Start();

            connectionOkEvent.WaitOne(10000).ShouldBeTrue();
        }

        [TestMethod]
        public void NamedPipeServerShouldMoveStateToLinkErrorIfClientDoesntConnect()
        {
            var server = NamedPipeConnectionFactory.CreateServer("NamedPipeServerShouldMoveStateToLinkErrorIfClientDoesntConnect", new ServerConnectionSettings(connectionTimeoutMilliseconds: 1000));
            using var connectionLinkErrorEvent = new AutoResetEvent(false);
            using var connectionOkEvent = new AutoResetEvent(false);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.LinkError)
                    connectionLinkErrorEvent.Set();
                else if (toState == ConnectionState.Connected)
                    connectionOkEvent.Set();
            });

            connectionLinkErrorEvent.WaitOne(10000).ShouldBeTrue();

            var client = NamedPipeConnectionFactory.CreateClient("NamedPipeServerShouldMoveStateToLinkErrorIfClientDoesntConnect");

            client.Start();

            connectionOkEvent.WaitOne(10000).ShouldBeTrue();
        }
    }
}
