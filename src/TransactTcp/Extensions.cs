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
        public static async Task ReadBufferedAsync(this NetworkStream networkStream, byte[] buffer, CancellationToken cancellationToken)
        {
            if (networkStream is null)
            {
                throw new ArgumentNullException(nameof(networkStream));
            }

            int offset = 0;
            while (offset < buffer.Length)
            {
                var bytesRead = await networkStream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken);
                if (bytesRead == 0)
                {
                    throw new InvalidOperationException("Unable to read bytes from network stream");
                }

                offset += bytesRead;
            }
        }
    }
}
