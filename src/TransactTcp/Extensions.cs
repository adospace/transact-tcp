using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public static class Extensions
    {
        internal static Task ReadBufferedAsync(this NetworkStream networkStream, byte[] buffer, CancellationToken cancellationToken) 
            => ReadBufferedAsync(networkStream, buffer, 0, buffer.Length, cancellationToken);

        internal static async Task ReadBufferedAsync(this NetworkStream networkStream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (networkStream is null)
            {
                throw new ArgumentNullException(nameof(networkStream));
            }

            while (count > 0)
            {
                var bytesRead = await networkStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Unable to read bytes from network stream");
                }

                offset += bytesRead;
                count -= bytesRead;
            }
        }

        internal static void ReadBuffered(this NetworkStream networkStream, byte[] buffer, int offset, int count)
        {
            if (networkStream is null)
            {
                throw new ArgumentNullException(nameof(networkStream));
            }

            while (count > 0)
            {
                var bytesRead = networkStream.Read(buffer, offset, count);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Unable to read bytes from network stream");
                }

                offset += bytesRead;
                count -= bytesRead;
            }
        }

        public static void Start(this IConnection connection, Action<IConnection, byte[]> receivedAction)
            => connection.Start(receivedAction: receivedAction);

        public static void Start(this IConnection connection, Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync)
            => connection.Start(receivedActionAsync: receivedActionAsync);

        public static void Start(this IConnection connection, Func<IConnection, NetworkBufferedReadStream, CancellationToken, Task> receivedActionStreamAsync)
            => connection.Start(receivedActionStreamAsync: receivedActionStreamAsync);
    }
}
