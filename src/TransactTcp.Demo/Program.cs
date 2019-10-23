using System;
using System.Net;
using System.Text;

namespace TransactTcp.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null ||
                args.Length != 1)
            {
                Console.WriteLine("Please run dotnet Transact.Demo.dll -server|-client");
                return;
            }

            if (args[0] == "-server")
                RunServer();
            else if (args[0] == "-client")
                RunClient();
            else
            {
                Console.WriteLine("Please run dotnet Transact.Demo.dll -server|-client");
            }
        }

        private static void RunServer()
        {
            using var server = ConnectionFactory.CreateServer(15000);

            server.Start(
                receivedAction: (connection, data) => Console.WriteLine($"Message from client: {Encoding.UTF8.GetString(data)}"),
                connectionStateChangedAction: (connection, fromState, toState) => Console.WriteLine($"Server connection state changed from {fromState} to {toState}"));

            RunConnection(server);
        }

        private static void RunClient()
        {
            using var client = ConnectionFactory.CreateClient(IPAddress.Loopback, 15000);

            client.Start(
                receivedAction: (connection, data) => Console.WriteLine($"Message from server: {Encoding.UTF8.GetString(data)}"),
                connectionStateChangedAction: (connection, fromState, toState) => Console.WriteLine($"Client connection state changed from {fromState} to {toState}"));

            RunConnection(client);
        }

        private static void RunConnection(IConnection connection)
        {
            while (true)
            {
                var message = Console.ReadLine();
                if (message == null)
                    break;

                connection.SendDataAsync(Encoding.UTF8.GetBytes(message)).Wait();
            }        
        }
    }
}
