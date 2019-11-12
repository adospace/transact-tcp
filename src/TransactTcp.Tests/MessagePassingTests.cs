using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
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
            var serverReceivedDataEvent = new AsyncAutoResetEvent(false);

            const int messageSize = 1024 * 128; //128kb

            using var server = TcpConnectionFactory.CreateServer(15010);

            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15010);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            server.Start((connection, data) =>
            {
                Assert.AreEqual(messageSize, data.Length);
                serverReceivedDataEvent.Set();
            },

            connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            await client.WaitForStateAsync(ConnectionState.Connected);
            await server.WaitForStateAsync(ConnectionState.Connected);

            await client.SendDataAsync(new byte[messageSize]);

            (await serverReceivedDataEvent.WaitAsync(10000)).ShouldBeTrue();
        }

        [TestMethod]
        public async Task MessageReceivedWithStreamShouldWork()
        {
            var serverReceivedDataEvent = new AsyncAutoResetEvent(false);

            int currentMessageSize = -1;

            using var server = TcpConnectionFactory.CreateServer(15021);

            using var client = TcpConnectionFactory.CreateClient(
                IPAddress.Loopback,
                15021);

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            server.Start(receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
            {
                var bytesRead = await stream.ReadAsync(new byte[stream.Length], 0, (int)stream.Length, cancellationToken);
                Assert.AreEqual(currentMessageSize, bytesRead);
                serverReceivedDataEvent.Set();
            },
            connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            await client.WaitForStateAsync(ConnectionState.Connected);
            await server.WaitForStateAsync(ConnectionState.Connected);

            await client.SendDataAsync(new byte[currentMessageSize = 10]);

            (await serverReceivedDataEvent.WaitAsync(10000)).ShouldBeTrue();

            await client.SendDataAsync(new byte[currentMessageSize = 120]);

            (await serverReceivedDataEvent.WaitAsync(10000)).ShouldBeTrue();
        }

        [TestMethod]
        public async Task MessageReceivedWithStreamAndUsingBufferStreamShouldWork()
        {
            var serverReceivedDataEvent = new AsyncAutoResetEvent(false);

            int currentMessageSize = -1;

            using var server = TcpConnectionFactory.CreateServer(15022, new ServerConnectionSettings(useBufferedStream: true));
            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15022, new ClientConnectionSettings(useBufferedStream: true));

            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            server.Start(receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
            {
                var bytesRead = await stream.ReadAsync(new byte[stream.Length], 0, (int)stream.Length, cancellationToken);
                Assert.AreEqual(currentMessageSize, bytesRead);
                serverReceivedDataEvent.Set();
            },
            connectionStateChangedAction: (connection, fromState, toState) =>
            {
            });

            await client.WaitForStateAsync(ConnectionState.Connected);
            await server.WaitForStateAsync(ConnectionState.Connected);

            await client.SendDataAsync(new byte[currentMessageSize = 10]);

            (await serverReceivedDataEvent.WaitAsync(10000)).ShouldBeTrue();

            await client.SendDataAsync(new byte[currentMessageSize = 120]);

            (await serverReceivedDataEvent.WaitAsync(10000)).ShouldBeTrue();
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
                new SslServerConnectionSettings(
                    sslCertificate: new X509Certificate(Utils.LoadResourceAsByteArray("transact-tcp_pfx"), "password")
                    ));

            using var client = TcpConnectionFactory.CreateSslClient(IPAddress.Loopback, 11000, connectionSettings:
                new SslClientConnectionSettings(
                    sslServerHost: "transact-tcp",
                    sslValidateServerCertificateCallback: (
                         object sender,
                         X509Certificate certificate,
                         X509Chain chain,
                         SslPolicyErrors sslPolicyErrors) => true //pass everything
                    ));

            var receivedFromClientEvent = new AsyncAutoResetEvent(false);
            var receivedFromServerEvent = new AsyncAutoResetEvent(false);

            client.Start(
                receivedAction: (c, data) =>
                {
                    if (Encoding.UTF8.GetString(data) == "SENT FROM SERVER")
                        receivedFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { }
                );

            server.Start(
                receivedAction: (c, data) =>
                {
                    if (Encoding.UTF8.GetString(data) == "SENT FROM CLIENT")
                        receivedFromClientEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { }
                );

            //WaitHandle.WaitAll(new[] { clientConnectedEvent, serverConnectedEvent }, 4000).ShouldBeTrue();
            await client.WaitForStateAsync(ConnectionState.Connected);
            await server.WaitForStateAsync(ConnectionState.Connected);

            await client.SendDataAsync(Encoding.UTF8.GetBytes("SENT FROM CLIENT"));
            await server.SendDataAsync(Encoding.UTF8.GetBytes("SENT FROM SERVER"));

            //WaitHandle.WaitAll(new[] { receivedFromClientEvent, receivedFromServerEvent }, 4000).ShouldBeTrue();

            (await receivedFromClientEvent.WaitAsync(10000)).ShouldBeTrue();
            (await receivedFromServerEvent.WaitAsync(10000)).ShouldBeTrue();
        }

        [TestMethod]
        public async Task SendMessagesUsingMemoryBuffer()
        {
            using var server = TcpConnectionFactory.CreateServer(11001);

            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 11001);

            var receivedFromClientEvent = new AsyncAutoResetEvent(false);
            var receivedFromServerEvent = new AsyncAutoResetEvent(false);

            client.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                    await stream.ReadAsync(memoryOwner.Memory, cancellationToken);
                    if (Encoding.UTF8.GetString(memoryOwner.Memory.Span) == "SENT FROM SERVER")
                        receivedFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { }
                );

            server.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)stream.Length);
                    await stream.ReadAsync(memoryOwner.Memory, cancellationToken);
                    if (Encoding.UTF8.GetString(memoryOwner.Memory.Span) == "SENT FROM CLIENT")
                        receivedFromClientEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) => { }
                );

            //WaitHandle.WaitAll(new[] { clientConnectedEvent, serverConnectedEvent }, 4000).ShouldBeTrue();

            await client.WaitForStateAsync(ConnectionState.Connected);
            await server.WaitForStateAsync(ConnectionState.Connected);

            await client.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("SENT FROM CLIENT")));
            await server.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("SENT FROM SERVER")));

            //WaitHandle.WaitAll(new[] { receivedFromClientEvent, receivedFromServerEvent }, 10000).ShouldBeTrue();
            (await receivedFromClientEvent.WaitAsync(10000)).ShouldBe(true);
            (await receivedFromServerEvent.WaitAsync(10000)).ShouldBe(true);
        }

        [TestMethod]
        public async Task ConnectionListenerShouldAcceptNewConnection()
        {
            using var multiPeerServer = TcpConnectionFactory.CreateMultiPeerServer(14000);

            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 14000);

            var receivedBackFromServerEvent = new AsyncAutoResetEvent(false);

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
                    connectionStateChangedAction: (c, fromState, toState) =>
                    {
                    }
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
                connectionStateChangedAction: (c, fromState, toState) => { }
                );

            await client.WaitForStateAsync(ConnectionState.Connected);

            await client.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("PING")));

            (await receivedBackFromServerEvent.WaitAsync(100000)).ShouldBeTrue();

            multiPeerServer.Stop();
        }

        [TestMethod]
        public async Task NamedPipeConnectionPointPointShouldSendAndReceiveMessages()
        {
            using var server = NamedPipeConnectionFactory.CreateServer("NamedPipeConnectionPointPointShouldSendAndReceiveMessages");
            using var client = NamedPipeConnectionFactory.CreateClient("NamedPipeConnectionPointPointShouldSendAndReceiveMessages");

            var serverReceivedMessageEvent = new AsyncAutoResetEvent(false);
            var clientReceivedMessageEvent = new AsyncAutoResetEvent(false);

            
            client.Start(connectionStateChangedAction: (connection, fromState, toState) =>
                {
                    Debug.WriteLine($"Client state changed to {toState}");
                },
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var sr = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    var messageFromServer = await sr.ReadLineAsync();
                    if (messageFromServer == null) throw new InvalidOperationException();

                    if (messageFromServer == "MESSAGE FROM SERVER")
                    {
                        Debug.WriteLine($"Client received message");
                        clientReceivedMessageEvent.Set();
                    }
                });

            //start server after client just to prove that it works
            server.Start(connectionStateChangedAction: (connection, fromState, toState) =>
                {
                    Debug.WriteLine($"Server state changed to {toState}");
                },
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var sr = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    if (await sr.ReadLineAsync() == "MESSAGE FROM CLIENT")
                    {
                        Debug.WriteLine($"Server received message");
                        serverReceivedMessageEvent.Set();
                    }
                });

            AssertEx.IsTrue(() => server.State == ConnectionState.Connected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Connected);

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

            (await serverReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();
            (await clientReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();

            server.Stop();
            server.Start();

            AssertEx.IsTrue(() => server.State == ConnectionState.Connected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Connected);

            await server.SendDataAsync(Encoding.UTF8.GetBytes("MESSAGE FROM SERVER\n"));
            await client.SendDataAsync(Encoding.UTF8.GetBytes("MESSAGE FROM CLIENT\n"));

            (await serverReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();
            (await clientReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();

            server.Stop();
            client.Stop();

            AssertEx.IsTrue(() => server.State == ConnectionState.Disconnected);
            AssertEx.IsTrue(() => client.State == ConnectionState.Disconnected);
        }

        //Buffered stream is no more supported for named pipe
        //[TestMethod]
        //public async Task NamedPipeConnectionPointPointShouldSendAndReceiveMessagesUsingBufferedStream()
        //{
        //    using var server = NamedPipeConnectionFactory.CreateServer("NamedPipeConnectionPointPointShouldSendAndReceiveMessagesUsingBufferedStream", new ServerConnectionSettings(useBufferedStream: true));
        //    using var client = NamedPipeConnectionFactory.CreateClient("NamedPipeConnectionPointPointShouldSendAndReceiveMessagesUsingBufferedStream", new ClientConnectionSettings(useBufferedStream: true));

        //    var serverReceivedMessageEvent = new AsyncAutoResetEvent(false);
        //    var clientReceivedMessageEvent = new AsyncAutoResetEvent(false);

        //    client.Start(
        //        connectionStateChangedAction: (connection, fromState, toState) =>
        //        {
        //            Debug.WriteLine($"Client state changed to {toState}");
        //        },
        //        receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
        //        {
        //            //NOTE: Do not use reader like StreamReader that already use a internal buffer!
        //            Debug.WriteLine($"Client received message");
        //            var buffer = new byte[19];
        //            await stream.ReadAsync(buffer, 0, buffer.Length);
        //            if (Encoding.UTF8.GetString(buffer) == "MESSAGE FROM SERVER")
        //                clientReceivedMessageEvent.Set();
        //        });

        //    //start server after client just to prove that it works
        //    server.Start(
        //        connectionStateChangedAction: (connection, fromState, toState) =>
        //        {
        //            Debug.WriteLine($"Server state changed to {toState}");
        //        },
        //        receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
        //        {
        //            //NOTE: Do not use reader like StreamReader that already use a internal buffer!
        //            Debug.WriteLine($"Server received message");
        //            var buffer = new byte[19];
        //            await stream.ReadAsync(buffer, 0, buffer.Length);
        //            if (Encoding.UTF8.GetString(buffer) == "MESSAGE FROM CLIENT")
        //                serverReceivedMessageEvent.Set();
        //        });

        //    await server.WaitForStateAsync(ConnectionState.Connected);
        //    await client.WaitForStateAsync(ConnectionState.Connected);

        //    await server.SendAsync(async (stream, cancellationToken) =>
        //    {
        //        await stream.WriteAsync(Encoding.UTF8.GetBytes("MESSAGE FROM SERVER"));
        //    });
        //    await client.SendAsync(async (stream, cancellationToken) =>
        //    {
        //        await stream.WriteAsync(Encoding.UTF8.GetBytes("MESSAGE FROM CLIENT"));
        //    });

        //    (await serverReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();
        //    (await clientReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();

        //    server.Stop();
        //    server.Start();

        //    await server.WaitForStateAsync(ConnectionState.Connected);
        //    await client.WaitForStateAsync(ConnectionState.Connected);

        //    await server.SendDataAsync(Encoding.UTF8.GetBytes("MESSAGE FROM SERVER"));
        //    await client.SendDataAsync(Encoding.UTF8.GetBytes("MESSAGE FROM CLIENT"));

        //    (await serverReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();
        //    (await clientReceivedMessageEvent.WaitAsync(10000)).ShouldBeTrue();

        //    server.Stop();
        //    client.Stop();

        //    await server.WaitForStateAsync(ConnectionState.Disconnected);
        //    await client.WaitForStateAsync(ConnectionState.Disconnected);
        //}

        [TestMethod]
        public async Task NamedPipeConnectionListenerShouldAcceptNewConnection()
        {
            using var multiPeerServer = NamedPipeConnectionFactory.CreateMultiPeerServer(
                "NamedPipeConnectionListenerShouldAcceptNewConnection");

            using var client1 = NamedPipeConnectionFactory.CreateClient(
                "NamedPipeConnectionListenerShouldAcceptNewConnection");

            using var client2 = NamedPipeConnectionFactory.CreateClient(
                "NamedPipeConnectionListenerShouldAcceptNewConnection");

            var receivedClient1BackFromServerEvent = new AsyncAutoResetEvent(false);
            var receivedClient2BackFromServerEvent = new AsyncAutoResetEvent(false);

            multiPeerServer.Start((listener, c) =>
            {
                c.Start(
                    receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                    {
                        using var memoryOwner = MemoryPool<byte>.Shared.Rent(17);
                        var buffer = memoryOwner.Memory.Slice(0, 17);
                        await stream.ReadAsync(buffer, cancellationToken);
                        var pingString = Encoding.UTF8.GetString(buffer.Span);
                        await connection.SendDataAsync(
                            new Memory<byte>(Encoding.UTF8.GetBytes($"SERVER RECEIVED: {pingString}")));
                    },
                    connectionStateChangedAction: (c, fromState, toState) =>
                    {
                        //if (toState == ConnectionState.Connected) serverConnectedEvent.Set();
                    });
            });

            client1.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent(34);
                    var buffer = memoryOwner.Memory.Slice(0, 34);
                    await stream.ReadAsync(buffer, cancellationToken);
                    if (Encoding.UTF8.GetString(buffer.Span) == "SERVER RECEIVED: PING FROM CLIENT1")
                        receivedClient1BackFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) =>
                {
                    //if (toState == ConnectionState.Connected) client1ConnectedEvent.Set(); 
                }
                );

            client2.Start(
                receivedActionStreamAsync: async (connection, stream, cancellationToken) =>
                {
                    using var memoryOwner = MemoryPool<byte>.Shared.Rent(34);
                    var buffer = memoryOwner.Memory.Slice(0, 34);
                    await stream.ReadAsync(buffer, cancellationToken);
                    if (Encoding.UTF8.GetString(buffer.Span) == "SERVER RECEIVED: PING FROM CLIENT2")
                        receivedClient2BackFromServerEvent.Set();
                },
                connectionStateChangedAction: (c, fromState, toState) =>
                {
                    //if (toState == ConnectionState.Connected) client2ConnectedEvent.Set(); 
                }
                );


            //WaitHandle.WaitAll(new[] { client1ConnectedEvent, client2ConnectedEvent, serverConnectedEvent }, 10000).ShouldBeTrue();
            await client1.WaitForStateAsync(ConnectionState.Connected);
            await client2.WaitForStateAsync(ConnectionState.Connected);

            await client1.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("PING FROM CLIENT1")));
            await client2.SendDataAsync(new Memory<byte>(Encoding.UTF8.GetBytes("PING FROM CLIENT2")));

            (await receivedClient1BackFromServerEvent.WaitAsync(10000)).ShouldBeTrue();
            (await receivedClient2BackFromServerEvent.WaitAsync(10000)).ShouldBeTrue();

            multiPeerServer.Stop();
        }

        [TestMethod]
        public void TcpConnectionShouldBeFullDuplex()
        {
            using var server = TcpConnectionFactory.CreateMultiPeerServer(15025);
            using var client = TcpConnectionFactory.CreateClient(IPAddress.Loopback, 15025);

            server.Start((listener, connection) =>
            {
                connection.Start(async (connection, stream, cancellationToken) =>
                {
                    var buffer = new byte[10];
                    stream.Read(buffer, 0, 10);
                    await connection.SendAsync((outStream, outCancellationToken) =>
                    {
                        outStream.Write(buffer, 0, 10);
                        return Task.CompletedTask;
                    });
                });
            });

            int receivedMessageBackCount = 0;
            client.Start((connection, stream, cancellationToken) =>
            {
                stream.Read(new byte[10], 0, 10);
                receivedMessageBackCount++;
                return Task.CompletedTask;
            });

            client.WaitForState(ConnectionState.Connected);

            for (int i = 0; i < 1000; i++)
            {
                client.SendAsync((stream, cancellationToken) =>
                {
                    stream.Write(new byte[10], 0, 10);
                    return Task.CompletedTask;
                });
            }

            AssertEx.IsTrue(() => receivedMessageBackCount == 1000);
        }

        [TestMethod]
        public void NamedPipeConnectionShouldBeFullDuplex()
        {
            using var server = NamedPipeConnectionFactory.CreateMultiPeerServer("NamedPipeConnectionShouldBeFullDuplex");
            using var client = NamedPipeConnectionFactory.CreateClient("NamedPipeConnectionShouldBeFullDuplex");

            server.Start((listener, connection) =>
            {
                connection.Start(async (connection, stream, cancellationToken) =>
                {
                    var buffer = new byte[10];
                    stream.Read(buffer, 0, 10);
                    await connection.SendAsync((outStream, outCancellationToken) =>
                    {
                        outStream.Write(buffer, 0, 10);
                        return Task.CompletedTask;
                    });
                });
            });

            int receivedMessageBackCount = 0;
            client.Start((connection, stream, cancellationToken) =>
            {
                stream.Read(new byte[10], 0, 10);
                receivedMessageBackCount++;
                return Task.CompletedTask;
            });

            client.WaitForState(ConnectionState.Connected);

            for (int i = 0; i < 1000; i++)
            {
                client.SendAsync((stream, cancellationToken) =>
                {
                    stream.Write(new byte[10], 0, 10);
                    return Task.CompletedTask;
                });
            }

            AssertEx.IsTrue(() => receivedMessageBackCount == 1000);
        }

        [TestMethod]
        public async Task NamedPipePointToPointConnectionShouldBeFullDuplex()
        {
            using var server = NamedPipeConnectionFactory.CreateServer("PIPE");
            using var client = NamedPipeConnectionFactory.CreateClient("PIPE");

            server.Start(async (connection, stream, cancellationToken) =>
            {
                var buffer = new byte[10];
                await stream.ReadAsync(buffer, 0, 10, cancellationToken);
                await connection.SendAsync(async (outStream, outCancellationToken) =>
                {
                    await outStream.WriteAsync(buffer, 0, 10, outCancellationToken);
                });
            });

            int receivedMessageBackCount = 0;
            client.Start(async (connection, stream, cancellationToken) =>
            {
                await stream.ReadAsync(new byte[10], 0, 10);
                receivedMessageBackCount++;
                System.Diagnostics.Debug.WriteLine(receivedMessageBackCount);
            });

            await client.WaitForStateAsync(ConnectionState.Connected);

            for (int i = 0; i < 100; i++)
            {
                await client.SendAsync(async (stream, cancellationToken) =>
                {
                    await stream.WriteAsync(new byte[10], 0, 10);                    
                });
            }

            AssertEx.IsTrue(() => receivedMessageBackCount == 100);
        }
    }
}
