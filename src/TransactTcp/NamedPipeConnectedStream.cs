using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class NamedPipeConnectedStream : Stream
    {
        private readonly string _nodename;
        private readonly string _inPipeName;
        private readonly Stream _inStream;
        private readonly string _outPipeName;
        private readonly Stream _outStream;

        public NamedPipeConnectedStream(string nodename, string inPipeName,
            Stream inStream, string outPipeName, Stream outStream)
        {
            _nodename = nodename;
            _inPipeName = inPipeName;
            _inStream = inStream ?? throw new ArgumentNullException(nameof(inStream));
            _outPipeName = outPipeName;
            _outStream = outStream ?? throw new ArgumentNullException(nameof(outStream));
        }

        public override string ToString()
        {
            return $"Read from {_inPipeName} | Write to {_outPipeName}";
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            _outStream.Flush();
            //((PipeStream)_outStream).WaitForPipeDrain();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            //((PipeStream)_outStream).WaitForPipeDrain();
            return _outStream.FlushAsync(cancellationToken);
        }

        public override bool CanTimeout => _inStream.CanTimeout && _outStream.CanTimeout;

        public override int ReadTimeout { get => _inStream.ReadTimeout; set => _inStream.ReadTimeout = value; }

        public override int WriteTimeout { get => _outStream.WriteTimeout; set => _outStream.WriteTimeout = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            System.Diagnostics.Debug.WriteLine($"{_nodename} reading buffer from {_inPipeName}");
            return _inStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"{_nodename} reading buffer async from {_inPipeName}");
            return _inStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            System.Diagnostics.Debug.WriteLine($"{_nodename} reading byte from {_inPipeName}");
            return _inStream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            System.Diagnostics.Debug.WriteLine($"{_nodename} writing buffer to {_outPipeName}");
            _outStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"{_nodename} writing buffer async to {_outPipeName}");
            return _outStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            System.Diagnostics.Debug.WriteLine($"{_nodename} writing byte to {_outPipeName}");
            _outStream.WriteByte(value);
        }

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
