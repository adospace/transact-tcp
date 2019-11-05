using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;
using TransactTcp.Ssl;
using TransactTcp.Tests.Resources;

namespace TransactTcp.Tests
{
    [TestClass]
    public class MessagePassingTests
    {
        [TestMethod]
        public async Task MessageLargerThan64KBShouldBeTransimittedWithoutProblems()
        {
            using var serverStateChangedEvent = new AutoResetEvent(false);
            using var clientStateChangedEvent = new AutoResetEvent(false);

            using var serverReceivedDataEvent = new AutoResetEvent(false);

            const int messageSize = 1024 * 128; //128kb

            using var server = TcpConnectionFactory.CreateServer(15000);

            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15000);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                    clientStateChangedEvent.Set();
            });

            server.Start((connection, data) =>
            {
                Assert.AreEqual(messageSize, data.Length);
                serverReceivedDataEvent.Set();
            },

            connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                    serverStateChangedEvent.Set();
            });

            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            await client.SendDataAsync(new byte[messageSize]);

            serverReceivedDataEvent.WaitOne(10000).ShouldBeTrue();
        }

        [TestMethod]
        public async Task MessageReceivedWithStreamShouldWork()
        {
            using var serverStateChangedEvent = new AutoResetEvent(false);
            using var clientStateChangedEvent = new AutoResetEvent(false);

            using var serverReceivedDataEvent = new AutoResetEvent(false);

            int currentMessageSize = -1;

            using var server = TcpConnectionFactory.CreateServer(15001);

            using var client = TcpConnectionFactory.CreateClient(
                IPAddress.Loopback,
                15001);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                    clientStateChangedEvent.Set();
            });

            server.Start(receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
            {
                var bytesRead = await stream.ReadAsync(new byte[stream.Length], 0, (int)stream.Length, cancellationToken);
                Assert.AreEqual(currentMessageSize, bytesRead);
                serverReceivedDataEvent.Set();
            },
            connectionStateChangedAction: (connection, fromState, toState) =>
            {
                if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                    serverStateChangedEvent.Set();
            });

            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            await client.SendDataAsync(new byte[currentMessageSize = 10]);

            serverReceivedDataEvent.WaitOne(10000).ShouldBeTrue();

            await client.SendDataAsync(new byte[currentMessageSize = 120]);

            serverReceivedDataEvent.WaitOne(10000).ShouldBeTrue();
        }

        [TestMethod]
        public async Task MessagesShouldPassThruRedundantChannelWhenNotAllChildConnectionsAreSlowOrDown()
        {
            var toxiproxyServerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TransactTcp.Tests", "toxiproxy-server-windows-amd64.exe");

            Directory.CreateDirectory(Path.GetDirectoryName(toxiproxyServerPath));

            await File.WriteAllBytesAsync(toxiproxyServerPath,
                Utils.LoadResourceAsByteArray("toxiproxy-server-windows-amd64.exe"));

            foreach (var existentToxiserverProcess in Process.GetProcessesByName("toxiproxy-server-windows-amd64.exe").ToList())
                existentToxiserverProcess.Kill();
             

            using var toxyproxyServerProcess = Process.Start(toxiproxyServerPath);

            try
            {
                //Setting up Toxiproxy proxies
                var connection = new Connection();
                var client = connection.Client();

                var interface1Proxy = new Proxy()
                {
                    Name = "interface1Proxy",
                    Enabled = true,
                    Listen = "127.0.0.1:12000",
                    Upstream = "127.0.0.1:12001"
                };

                await client.AddAsync(interface1Proxy);

                var interface2Proxy = new Proxy()
                {
                    Name = "interface2Proxy",
                    Enabled = true,
                    Listen = "127.0.0.1:13000",
                    Upstream = "127.0.0.1:13001"
                };

                await client.AddAsync(interface2Proxy);

                using var serverConnection = TcpConnectionFactory.CreateRedundantServer(new[] { new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12001), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13001) });
                using var clientConnection = TcpConnectionFactory.CreateRedundantClient(new[] { new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12000), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 13000) });

                using var serverConnectedEvent = new AutoResetEvent(false);
                using var clientConnectedEvent = new AutoResetEvent(false);
                using var errorsOnServerSideEvent = new AutoResetEvent(false);
                using var errorsOnClientSideEvent = new AutoResetEvent(false);

                int counterOfMessagesArrivedAtServer = 0;
                serverConnection.Start(
                    receivedAction: (c, data) => 
                    {
                        if (BitConverter.ToInt32(data) != counterOfMessagesArrivedAtServer)
                            errorsOnServerSideEvent.Set();
                        counterOfMessagesArrivedAtServer++;
                    },
                    connectionStateChangedAction: (c, fromState, toState) => 
                    {
                        if (toState == ConnectionState.Connected)
                            serverConnectedEvent.Set();
                    });

                int counterOfMessagesArrivedAtClient = 0;
                clientConnection.Start(
                    receivedAction: (c, data) => 
                    {
                        if (BitConverter.ToInt32(data) != counterOfMessagesArrivedAtClient)
                            errorsOnClientSideEvent.Set();
                        counterOfMessagesArrivedAtClient++;
                    },
                    connectionStateChangedAction: (c, fromState, toState) => 
                    {
                        if (toState == ConnectionState.Connected)
                            clientConnectedEvent.Set();
                    });

                WaitHandle.WaitAll(new[] { serverConnectedEvent, clientConnectedEvent }, 5000).ShouldBeTrue();

                var cancellationTokenSource = new CancellationTokenSource();
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(async () =>
                {
                    var counter = 0;
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        await clientConnection.SendDataAsync(BitConverter.GetBytes(counter));
                        await serverConnection.SendDataAsync(BitConverter.GetBytes(counter));
                        await Task.Delay(500, cancellationTokenSource.Token);
                        counter++;
                    }

                }, cancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                await Task.Delay(1000);

                interface1Proxy.Enabled = false;
                await client.UpdateAsync(interface1Proxy);

                WaitHandle.WaitAll(new[] { errorsOnServerSideEvent, errorsOnClientSideEvent }, 2000).ShouldBeFalse();

                interface1Proxy.Enabled = true;
                await client.UpdateAsync(interface1Proxy);

                interface2Proxy.Enabled = false;
                await client.UpdateAsync(interface2Proxy);

                WaitHandle.WaitAll(new[] { errorsOnServerSideEvent, errorsOnClientSideEvent }, 2000).ShouldBeFalse();

                interface2Proxy.Enabled = true;
                await client.UpdateAsync(interface2Proxy);

                var latencyProxy = new LatencyToxic()
                {
                    Name = "latencyToxicInterface2",
                    Stream = ToxicDirection.DownStream,
                    Toxicity = 1.0,
                };
                latencyProxy.Attributes.Jitter = 100;
                latencyProxy.Attributes.Latency = 300;

                await interface1Proxy.AddAsync(latencyProxy);

                WaitHandle.WaitAll(new[] { errorsOnServerSideEvent, errorsOnClientSideEvent }, 2000).ShouldBeFalse();

                var slicerToxic = new SlicerToxic()
                {
                    Name = "slicerToxicInterface1",
                    Stream = ToxicDirection.UpStream,
                    Toxicity = 1.0,
                };
                slicerToxic.Attributes.AverageSize = 10;
                slicerToxic.Attributes.Delay = 5;
                slicerToxic.Attributes.SizeVariation = 1;

                await interface1Proxy.AddAsync(slicerToxic);

                WaitHandle.WaitAll(new[] { errorsOnServerSideEvent, errorsOnClientSideEvent }, 4000).ShouldBeFalse();

                interface2Proxy.Enabled = false;
                await client.UpdateAsync(interface2Proxy);

                WaitHandle.WaitAll(new[] { errorsOnServerSideEvent, errorsOnClientSideEvent }, 2000).ShouldBeFalse();

                cancellationTokenSource.Cancel();
            }
            finally
            {
                toxyproxyServerProcess.Kill();
            }
        }

        [TestMethod]
        public async Task ServerAndClientShouldJustWorkInSsl()
        {

            using var server = TcpConnectionFactory.CreateSslServer(11000,
                new SslConnectionSettings(
                    sslCertificate: new X509Certificate(Utils.LoadResourceAsByteArray("transact-tcp_pfx"), "password")
                    ));

            using var client = TcpConnectionFactory.CreateSslClient(IPAddress.Loopback, 11000, connectionSettings:
                new SslConnectionSettings(
                    sslServerHost: "transact-tcp",
                    sslValidateServerCertificateCallback: (
                         object sender,
                         X509Certificate certificate,
                         X509Chain chain,
                         SslPolicyErrors sslPolicyErrors) => true //pass everything
                    ));

            using var serverConnectedEvent = new AutoResetEvent(false);
            using var clientConnectedEvent = new AutoResetEvent(false);
            using var receivedFromClientEvent = new AutoResetEvent(false);
            using var receivedFromServerEvent = new AutoResetEvent(false);

            client.Start(
                receivedAction: (c, data) =>
                {
                    if (Encoding.UTF8.GetString(data) == "SENT FROM SERVER")
                        receivedFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) clientConnectedEvent.Set(); }
                );

            server.Start(
                receivedAction: (c, data) =>
                {
                    if (Encoding.UTF8.GetString(data) == "SENT FROM CLIENT")
                        receivedFromClientEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) serverConnectedEvent.Set(); }
                );

            WaitHandle.WaitAll(new[] { clientConnectedEvent, serverConnectedEvent }, 4000).ShouldBeTrue();

            await client.SendDataAsync(Encoding.UTF8.GetBytes("SENT FROM CLIENT"));
            await server.SendDataAsync(Encoding.UTF8.GetBytes("SENT FROM SERVER"));


            WaitHandle.WaitAll(new[] { receivedFromClientEvent, receivedFromServerEvent }, 4000).ShouldBeTrue();
        }

        [TestMethod]
        public async Task SendMessagesUsingMemoryBuffer()
        {
            using var server = TcpConnectionFactory.CreateServer(11001);

            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 11001);

            using var serverConnectedEvent = new AutoResetEvent(false);
            using var clientConnectedEvent = new AutoResetEvent(false);
            using var receivedFromClientEvent = new AutoResetEvent(false);
            using var receivedFromServerEvent = new AutoResetEvent(false);

            client.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                    await stream.ReadAsync(memoryOwner.Memory, cancellationToken);
                    if (Encoding.UTF8.GetString(memoryOwner.Memory.Span) == "SENT FROM SERVER")
                        receivedFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) clientConnectedEvent.Set(); }
                );

            server.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                    await stream.ReadAsync(memoryOwner.Memory, cancellationToken);
                    if (Encoding.UTF8.GetString(memoryOwner.Memory.Span) == "SENT FROM CLIENT")
                        receivedFromClientEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) serverConnectedEvent.Set(); }
                );

            WaitHandle.WaitAll(new[] { clientConnectedEvent, serverConnectedEvent }, 4000).ShouldBeTrue();

            await client.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("SENT FROM CLIENT")));
            await server.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("SENT FROM SERVER")));

            WaitHandle.WaitAll(new[] { receivedFromClientEvent, receivedFromServerEvent }, 10000).ShouldBeTrue();
        }

        [TestMethod]
        public async Task ConnectionListenerShouldAcceptNewConnection()
        {
            using var multiPeerServer = TcpConnectionFactory.CreateMultiPeerServer(14000);

            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 14000);

            using var serverConnectedEvent = new AutoResetEvent(false);
            using var clientConnectedEvent = new AutoResetEvent(false);
            using var receivedBackFromServerEvent = new AutoResetEvent(false);

            IConnection newConnection = null;

            multiPeerServer.Start((listener, c) =>
            {
                newConnection = c;
                newConnection.Start(
                    receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                    {
                        using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                        var buffer = memoryOwner.Memory.Slice(0, (int)stream.Length);
                        await stream.ReadAsync(buffer, cancellationToken);
                        var pingString = Encoding.UTF8.GetString(buffer.Span);
                        await connection.SendDataAsync(
                            new Memory<byte>(Encoding.UTF8.GetBytes($"SERVER RECEIVED: {pingString}")));
                    },
                    connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) serverConnectedEvent.Set(); }
                    );
            });

            client.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                    var buffer = memoryOwner.Memory.Slice(0, (int)stream.Length);
                    await stream.ReadAsync(buffer, cancellationToken);
                    if (Encoding.UTF8.GetString(buffer.Span) == "SERVER RECEIVED: PING")
                        receivedBackFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) clientConnectedEvent.Set(); }
                );

            WaitHandle.WaitAll(new[] { clientConnectedEvent, serverConnectedEvent }, 40000).ShouldBeTrue();

            await client.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("PING")));

            WaitHandle.WaitAll(new[] { receivedBackFromServerEvent }, 100000).ShouldBeTrue();

            multiPeerServer.Stop();
        }

        [TestMethod]
        public async Task NamedPipeConnectionPointPointShouldSendAndReceiveMessages()
        {
            using var serverConnectedEvent = new AutoResetEvent(false);
            using var clientConnectedEvent = new AutoResetEvent(false);
            using var serverDisconnectedEvent = new AutoResetEvent(false);
            using var clientDisconnectedEvent = new AutoResetEvent(false);

            var server = NamedPipeConnectionFactory.CreateServer("testpipe");
            var client = NamedPipeConnectionFactory.CreateClient("testpipe");

            using var serverReceivedMessageEvent = new AutoResetEvent(false);
            using var clientReceivedMessageEvent = new AutoResetEvent(false);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
                {
                    Debug.WriteLine($"Client state changed to {toState}");
                    if (toState == ConnectionState.Connected)
                        clientConnectedEvent.Set();
                    else if (toState == ConnectionState.Disconnected)
                        clientDisconnectedEvent.Set();
                },
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    Debug.WriteLine($"Client received message");
                    using var sr = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    if (await sr.ReadLineAsync() == "MESSAGE FROM SERVER")
                        clientReceivedMessageEvent.Set();
                });

            //start server after client just to prove that it works
            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
                {
                    Debug.WriteLine($"Server state changed to {toState}");
                    if (toState == ConnectionState.Connected)
                        serverConnectedEvent.Set();
                    else if (toState == ConnectionState.Disconnected)
                        serverDisconnectedEvent.Set();
                },
                receivedActionStreamAsync: async (connection, stream, cancellationToken) => 
                {
                    Debug.WriteLine($"Server received message");
                    using var sr = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    if (await sr.ReadLineAsync() == "MESSAGE FROM CLIENT")
                        serverReceivedMessageEvent.Set();
                });

            serverConnectedEvent.WaitOne(10000).ShouldBeTrue();
            clientConnectedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Connected);
            client.State.ShouldBe(ConnectionState.Connected);

            await server.SendAsync(async (stream, cancellationToken) =>
            {
                using var sw = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
                await sw.WriteLineAsync("MESSAGE FROM SERVER");
            });
            await client.SendAsync(async (stream, cancellationToken) =>
            {
                using var sw = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
                await sw.WriteLineAsync("MESSAGE FROM CLIENT");
            });

            serverReceivedMessageEvent.WaitOne(10000).ShouldBeTrue();
            clientReceivedMessageEvent.WaitOne(10000).ShouldBeTrue();

            server.Stop();
            server.Start();

            serverConnectedEvent.WaitOne(10000).ShouldBeTrue();
            clientConnectedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Connected);
            client.State.ShouldBe(ConnectionState.Connected);

            await server.SendDataAsync(Encoding.UTF8.GetBytes("MESSAGE FROM SERVER\n"));
            await client.SendDataAsync(Encoding.UTF8.GetBytes("MESSAGE FROM CLIENT\n"));

            serverReceivedMessageEvent.WaitOne(10000).ShouldBeTrue();
            clientReceivedMessageEvent.WaitOne(10000).ShouldBeTrue();

            server.Stop();
            client.Stop();

            serverDisconnectedEvent.WaitOne(10000).ShouldBeTrue();
            clientDisconnectedEvent.WaitOne(10000).ShouldBeTrue();

            server.State.ShouldBe(ConnectionState.Disconnected);
            client.State.ShouldBe(ConnectionState.Disconnected);
        }

        [TestMethod]
        public async Task NamedPipeConnectionListenerShouldAcceptNewConnection()
        {
            using var multiPeerServer = NamedPipeConnectionFactory.CreateMultiPeerServer("testpipe");

            using var client1 = NamedPipeConnectionFactory.CreateClient("testpipe");
            using var client2 = NamedPipeConnectionFactory.CreateClient("testpipe");

            using var serverConnectedEvent = new AutoResetEvent(false);
            using var client1ConnectedEvent = new AutoResetEvent(false);
            using var client2ConnectedEvent = new AutoResetEvent(false);
            using var receivedClient1BackFromServerEvent = new AutoResetEvent(false);
            using var receivedClient2BackFromServerEvent = new AutoResetEvent(false);

            multiPeerServer.Start((listener, c) =>
            {
                c.Start(
                    receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                    {
                        using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                        var buffer = memoryOwner.Memory.Slice(0, (int)stream.Length);
                        await stream.ReadAsync(buffer, cancellationToken);
                        var pingString = Encoding.UTF8.GetString(buffer.Span);
                        await connection.SendDataAsync(
                            new Memory<byte>(Encoding.UTF8.GetBytes($"SERVER RECEIVED: {pingString}")));
                    },
                    connectionStateChangedAction: (c, fromState, toState) =>
                    {
                        if (toState == ConnectionState.Connected) serverConnectedEvent.Set();
                    });
            });

            client1.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                    var buffer = memoryOwner.Memory.Slice(0, (int)stream.Length);
                    await stream.ReadAsync(buffer, cancellationToken);
                    if (Encoding.UTF8.GetString(buffer.Span) == "SERVER RECEIVED: PING FROM CLIENT1")
                        receivedClient1BackFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) client1ConnectedEvent.Set(); }
                );

            client2.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                    var buffer = memoryOwner.Memory.Slice(0, (int)stream.Length);
                    await stream.ReadAsync(buffer, cancellationToken);
                    if (Encoding.UTF8.GetString(buffer.Span) == "SERVER RECEIVED: PING FROM CLIENT2")
                        receivedClient2BackFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { if (toState == ConnectionState.Connected) client2ConnectedEvent.Set(); }
                );


            WaitHandle.WaitAll(new[] { client1ConnectedEvent, client2ConnectedEvent, serverConnectedEvent }, 40000).ShouldBeTrue();

            await client1.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("PING FROM CLIENT1")));
            await client2.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("PING FROM CLIENT2")));

            WaitHandle.WaitAll(new[] { receivedClient1BackFromServerEvent, receivedClient2BackFromServerEvent }, 100000).ShouldBeTrue();

            multiPeerServer.Stop();
        }
    }
}
