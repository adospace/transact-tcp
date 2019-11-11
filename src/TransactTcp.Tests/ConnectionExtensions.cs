using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp.Tests
{
    public static class ConnectionExtensions
    {
        public static void WaitForState(this IConnection connection, ConnectionState state, int timeout = 10000)
        {
            while (connection.State != state && timeout > 0)
            {
                Thread.Sleep(10);
                timeout -= 10;
            }

            if (connection.State != state)
            {
                throw new AssertFailedException($"Expected {state}, actual {connection.State}");
            }
        }

        public static async Task WaitForStateAsync(this IConnection connection, ConnectionState state, int timeout = 10000)
        {
            while (connection.State != state && timeout > 0)
            {
                await Task.Delay(10);
                timeout -= 10;
            }

            if (connection.State != state)
            {
                throw new AssertFailedException($"Expected {state}, actual {connection.State}");
            }
        }
    }
}
