using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class NamedPipeConnectedStream : Stream
    {
        private readonly Stream _inStream;
        private readonly Stream _outStream;

        public NamedPipeConnectedStream(Stream inStream, Stream outStream)
        {
            _inStream = inStream ?? throw new ArgumentNullException(nameof(inStream));
            _outStream = outStream ?? throw new ArgumentNullException(nameof(outStream));
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => _outStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inStream.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inStream.ReadAsync(buffer, offset, count, cancellationToken);

        public override int ReadByte() =>
            _inStream.ReadByte();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => 
            _outStream.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _outStream.WriteAsync(buffer, offset, count, cancellationToken);

        public override void WriteByte(byte value) => 
            _outStream.WriteByte(value);

#if NETSTANDARD2_1
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inStream.ReadAsync(buffer, cancellationToken);

        public override int Read(Span<byte> buffer) =>
            _inStream.Read(buffer);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _outStream.WriteAsync(buffer, cancellationToken);

        public override void Write(ReadOnlySpan<byte> buffer) => 
            _outStream.Write(buffer);
#endif
        public override void Close()
        {
            _inStream.Close();
            _outStream.Close();
            base.Close();
        }
    }
}
