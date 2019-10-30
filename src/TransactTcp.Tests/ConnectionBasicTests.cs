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

            var server = TcpConnectionFactory.CreateServer(15000);

            var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15000);

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
            var server = TcpConnectionFactory.CreateServer(15000);

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
            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15000);

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
                15000);

            var client = TcpConnectionFactory.CreateClient(
                IPAddress.Loopback,
                15000);

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

            var server = TcpConnectionFactory.CreateServer(15000);

            var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15000);

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

            var server = NamedPipeConnectionFactory.CreateServer("testpipe");

            var client = NamedPipeConnectionFactory.CreateClient("testpipe");

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

    }
}
