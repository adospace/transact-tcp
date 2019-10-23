﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public interface IConnection : IDisposable
    {
        void Start(Action<IConnection, byte[]> receivedAction = null,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync = null,
            Func<IConnection, NetworkBufferedReadStream, CancellationToken, Task> receivedActionStreamAsync = null,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null);

        void Stop();

        ConnectionState State { get; }

        Task SendDataAsync(byte[] data);
    }
}
