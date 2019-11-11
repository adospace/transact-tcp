using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
using Serilog;
using Shouldly;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp.Tests
{
    [TestClass]
    public class ConnectionBasicTests
    {
        private static ILogger _logger = Log.ForContext<ConnectionBasicTests>();

        [TestMethod]
        public void ServerAndClientShouldConnectAndDisconnectWithoutErrors()
        {
            using var server = TcpConnectionFactory.CreateServer(15007);
            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15007);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            AssertEx.IsTrue(() => server.State == ConnectionState.Connected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Connected);

            server.Stop();
            client.Stop();

            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Disconnected);
        }

        [TestMethod]
        public void CancelServerPendingConnectionShouldJustWork()
        {
            using var server = TcpConnectionFactory.CreateServer(15001);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            server.Stop();

            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
        }

        [TestMethod]
        public void CancelClientPendingConnectionShouldJustWork()
        {
            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15002);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            client.Stop();

            AssertEx.IsTrue(() => client.State == ConnectionState.Disconnected);
        }

        [TestMethod]
        public void StartServerAfterClientShouldWork()
        {
            using var server = TcpConnectionFactory.CreateServer(
                15003);

            using var client = TcpConnectionFactory.CreateClient(
                IPAddress.Loopback,
                15003);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            AssertEx.IsTrue(() => server.State == ConnectionState.Connected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Connected);

            server.Stop();
            client.Stop();

            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
        }

        [TestMethod]
        public void ClientShouldReconnectToServerAfterServerRestart()
        {
            _logger.Verbose("Begin test ClientShouldReconnectToServerAfterServerRestart");

            using var server = TcpConnectionFactory.CreateServer(15004);
            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15004);

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                _logger.Verbose($"Server state change from {fromState} to {toState}");
            });

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                _logger.Verbose($"Client state change from {fromState} to {toState}");
            });

            AssertEx.IsTrue(()=> server.State == ConnectionState.Connected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Connected);

            _logger.Verbose($"Stop server");
            server.Stop();

            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
            AssertEx.IsTrue(() => client.State == ConnectionState.LinkError);

            _logger.Verbose($"Start server");
            server.Start();

            _logger.Verbose($"Stop server");
            server.Stop();
            _logger.Verbose($"Stop client");
            client.Stop();

            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Disconnected);

            _logger.Verbose("End test ClientShouldReconnectToServerAfterServerRestart");
        }

        [TestMethod]
        public void NamedPipeServerAndClientShouldConnectAndDisconnectWithoutErrors()
        {
            using var server = NamedPipeConnectionFactory.CreateServer("NamedPipeServerAndClientShouldConnectAndDisconnectWithoutErrors");
            using var client = NamedPipeConnectionFactory.CreateClient("NamedPipeServerAndClientShouldConnectAndDisconnectWithoutErrors");

            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            AssertEx.IsTrue(() => server.State == ConnectionState.Connected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Connected);

            server.Stop();
            client.Stop();

            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Disconnected);
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
