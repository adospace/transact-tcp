using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Text;

namespace TransactTcp.ChatDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null ||
                args.Length != 1)
            {
                Console.WriteLine("Please run TransactTcp.ChatDemo -server|-client");
                return;
            }

            if (args[0] == "-server")
                RunServer();
            else if (args[0] == "-client")
                RunClient();
            else
            {
                Console.WriteLine("Please run Transact.ChatDemo -server|-client");
            }
        }

        private readonly static ConcurrentDictionary<IConnection, IConnection> _connectedClients = new ConcurrentDictionary<IConnection, IConnection> ();

        private static void RunServer()
        {
            Console.WriteLine("Starting server...");

            using var server = ConnectionFactory.CreateMultiPeerServer(15000);

            server.Start(
                connectionCreated: 
                    (listener, newClientConnection) =>
                    {
                        Console.WriteLine($"Client connection accepted");
                        _connectedClients.TryAdd(newClientConnection, newClientConnection);

                        newClientConnection.Start(
                            receivedAction: (connection, data) =>
                            {
                                foreach (var clientConnection in _connectedClients.Keys.Except(new[] { connection }))
                                    clientConnection.SendDataAsync(data).Wait();
                            },
                            connectionStateChangedAction: (connection, fromState, toState)=>
                            { 
                                if (toState == ConnectionState.Connected)
                                    connection.SendDataAsync(Encoding.UTF8.GetBytes("Welcome from server!")).Wait();
                                else if (toState == ConnectionState.LinkError)
                                    _connectedClients.TryRemove(connection, out var _);
                            });
                    });

            Console.ReadLine();

            Console.WriteLine("Closing server...");
        }

        private static void RunClient()
        {
            using var client = ConnectionFactory.CreateClient(IPAddress.Loopback, 15000);

            client.Start(
                receivedAction: (connection, data) => Console.WriteLine($"Message from server: {Encoding.UTF8.GetString(data)}"),
                connectionStateChangedAction: (connection, fromState, toState) => Console.WriteLine($"Client connection state changed from {fromState} to {toState}"));

            while (true)
            {
                var message = Console.ReadLine();
                if (message == null)
                    break;

                client.SendDataAsync(Encoding.UTF8.GetBytes(message)).Wait();
            }
        }
    }
}
