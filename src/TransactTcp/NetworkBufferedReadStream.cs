using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public class NetworkBufferedReadStream : Stream
    {
        private readonly Stream _innerStream;

        internal NetworkBufferedReadStream(Stream stream, long messageLength)
        {
            _innerStream = stream;

            Length = messageLength;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length { get; }

        private long _position;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_position < Length)
            {
                await _innerStream.ReadBufferedAsync(new byte[Length - _position], cancellationToken);
            }

            _position = Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _innerStream.ReadBuffered(buffer, offset, count);
            _position += count;
            return count;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _innerStream.ReadBufferedAsync(buffer, offset, count, cancellationToken);
            _position += count;
            return count;
        }
#if NETSTANDARD2_1
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await _innerStream.ReadBufferedAsync(buffer, cancellationToken);
            _position += buffer.Length;
            return buffer.Length;
        }
#endif
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
