using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;
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

            using var server = ConnectionFactory.CreateServer(15000);

            using var client = ConnectionFactory.CreateClient(IPAddress.Loopback, 15000);

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

            using var server = ConnectionFactory.CreateServer(15000);

            using var client = ConnectionFactory.CreateClient(
                IPAddress.Loopback,
                15000);

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
        public async Task MessagesShouldPassedThruRedundantChannelWhenNotAllChildConnectionsAreSlowOrDown()
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
                    Listen = "127.0.0.1:15000",
                    Upstream = "127.0.0.1:15001"
                };

                await client.AddAsync(interface1Proxy);

                var interface2Proxy = new Proxy()
                {
                    Name = "interface2Proxy",
                    Enabled = true,
                    Listen = "127.0.0.1:16000",
                    Upstream = "127.0.0.1:16001"
                };

                await client.AddAsync(interface2Proxy);

                using var serverConnection = ConnectionFactory.CreateRedundantServer(new[] { new IPEndPoint(IPAddress.Parse("127.0.0.1"), 15001), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16001) });
                using var clientConnection = ConnectionFactory.CreateRedundantClient(new[] { new IPEndPoint(IPAddress.Parse("127.0.0.1"), 15000), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16000) });

                using var serverConnectedEvent = new AutoResetEvent(false);
                using var clientConnectedEvent = new AutoResetEvent(false);

                int counterOfMessagesArrivedAtServer = 0;
                serverConnection.Start(
                    receivedAction: (c, data) => 
                    {
                        BitConverter.ToInt32(data).ShouldBe(counterOfMessagesArrivedAtServer);
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
                        BitConverter.ToInt32(data).ShouldBe(counterOfMessagesArrivedAtClient);
                        counterOfMessagesArrivedAtClient++;
                    },
                    connectionStateChangedAction: (c, fromState, toState) => 
                    {
                        if (toState == ConnectionState.Connected)
                            clientConnectedEvent.Set();
                    });

                WaitHandle.WaitAll(new[] { serverConnectedEvent, clientConnectedEvent }, 1000).ShouldBeTrue();

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

                await Task.Delay(1000);

                interface1Proxy.Enabled = true;
                await client.UpdateAsync(interface1Proxy);

                interface2Proxy.Enabled = false;
                await client.UpdateAsync(interface2Proxy);

                await Task.Delay(1000);

                interface2Proxy.Enabled = true;
                await client.UpdateAsync(interface2Proxy);

                //var latencyProxy = new LatencyToxic()
                //{ 
                //    Name = "latencyInterface2",
                //    Stream = ToxicDirection.DownStream,
                //    Toxicity = 1.0,
                //};
                //latencyProxy.Attributes.Jitter = 100;
                //latencyProxy.Attributes.Latency = 300;

                //await interface1Proxy.AddAsync(latencyProxy);

                //await Task.Delay(1000);

                cancellationTokenSource.Cancel();

            }
            finally
            {
                toxyproxyServerProcess.Kill();
            }
        }
    }
}
