using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class NetworkReadStream : Stream
    {
        private readonly Stream _innerStream;

        internal NetworkReadStream(Stream stream)
        {
            _innerStream = stream;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override bool CanTimeout => _innerStream.CanTimeout;

        public override int ReadTimeout { get => _innerStream.ReadTimeout; set => _innerStream.ReadTimeout = value; }

        public override int WriteTimeout { get => _innerStream.WriteTimeout; set => _innerStream.WriteTimeout = value; }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) 
            => _innerStream.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) 
            => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

#if NETSTANDARD2_1
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            => _innerStream.ReadAsync(buffer, cancellationToken);
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
