﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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

            using var server = ConnectionFactory.CreateServer(
                15000,
                (connection, data) => 
                {
                    Assert.AreEqual(messageSize, data.Length);
                    serverReceivedDataEvent.Set(); 
                },
                connectionStateChangedAction: (connection, fromState, toState) =>
                {
                    if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                        serverStateChangedEvent.Set();
                });

            using var client = ConnectionFactory.CreateClient(
                IPAddress.Loopback,
                15000,
                (connection, data) => { },
                connectionStateChangedAction: (connection, fromState, toState) =>
                {
                    if (toState == ConnectionState.Connected || toState == ConnectionState.Disconnected || toState == ConnectionState.LinkError)
                        clientStateChangedEvent.Set();
                });

            client.Start();

            server.Start();
            clientStateChangedEvent.WaitOne(10000).ShouldBeTrue();
            serverStateChangedEvent.WaitOne(10000).ShouldBeTrue();

            await client.SendDataAsync(new byte[messageSize]);

            serverReceivedDataEvent.WaitOne(10000).ShouldBeTrue();

        }
    }
}
