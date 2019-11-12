using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
//    internal class NetworkWriteStream : Stream
//    {
//        private readonly Stream _innerStream;

//        internal NetworkWriteStream(Stream stream)
//        {
//            _innerStream = stream;
//        }

//        public override bool CanRead => false;

//        public override bool CanSeek => false;

//        public override bool CanWrite => true;

//        public override long Length => throw new NotSupportedException();

//        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

//        public override bool CanTimeout => _innerStream.CanTimeout;

//        public override int ReadTimeout { get => _innerStream.ReadTimeout; set => _innerStream.ReadTimeout = value; }

//        public override int WriteTimeout { get => _innerStream.WriteTimeout; set => _innerStream.WriteTimeout = value; }

//        public override void Flush() 
//            => _innerStream.Flush();

//        public override Task FlushAsync(CancellationToken cancellationToken) 
//            => _innerStream.FlushAsync(cancellationToken);

//        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

//        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

//        public override void SetLength(long value) => throw new NotSupportedException();

//        public override void Write(byte[] buffer, int offset, int count) => 
//            _innerStream.Write(buffer, offset, count);

//        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) 
//            => _innerStream.WriteAsync(buffer, offset, count, cancellationToken);

//#if NETSTANDARD2_1
//        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) 
//            => _innerStream.WriteAsync(buffer, cancellationToken);
//#endif
//    }
}
