﻿using ServiceActor;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    [KeepAsyncContext(false)]
    public interface IConnection : IDisposable
    {
        [BlockCaller]
        void Start(Action<IConnection, byte[]> receivedAction = null,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync = null,
            Func<IConnection, Stream, CancellationToken, Task> receivedActionStreamAsync = null,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null);

        [BlockCaller]
        void Stop();
        
        [AllowConcurrentAccess]
        ConnectionState State { get; }

        Task SendDataAsync(byte[] data, CancellationToken cancellationToken);

        Task SendAsync(Func<Stream, CancellationToken, Task> sendFunction, CancellationToken cancellationToken);

#if NETSTANDARD2_1
        [BlockCaller]
        Task SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
#endif
    }
}
