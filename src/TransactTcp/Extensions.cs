using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public static class Extensions
    {
        internal static Task ReadBufferedAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken) 
            => ReadBufferedAsync(stream, buffer, 0, buffer.Length, cancellationToken);

        internal static async Task ReadBufferedAsync(this Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            while (count > 0)
            {
                var bytesRead = await stream.ReadAsync(buffer, offset, count, cancellationToken);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Unable to read bytes from network stream");
                }

                offset += bytesRead;
                count -= bytesRead;
            }
        }

        internal static void ReadBuffered(this Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            while (count > 0)
            {
                var bytesRead = stream.Read(buffer, offset, count);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Unable to read bytes from network stream");
                }

                offset += bytesRead;
                count -= bytesRead;
            }
        }

        internal static async Task<byte[]> ReadBytesAsync(this Stream stream, int count)
        {
            var bytes = new byte[count];
            await stream.ReadAsync(bytes, 0, count);
            return bytes;
        }

        public static void Start(this IConnection connection, Action<IConnection, byte[]> receivedAction)
            => connection.Start(receivedAction: receivedAction);

        public static void Start(this IConnection connection, Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync)
            => connection.Start(receivedActionAsync: receivedActionAsync);

        public static void Start(this IConnection connection, Func<IConnection, NetworkBufferedReadStream, CancellationToken, Task> receivedActionStreamAsync)
            => connection.Start(receivedActionStreamAsync: receivedActionStreamAsync);

        public static void Restart(this IConnection connection)
        {
            connection.Stop();
            connection.Start();        
        }
    }
}
