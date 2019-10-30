using ServiceActor;
using Stateless;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class RedundantConnection : IConnection, IDisposable
    {
        private Action<IConnection, byte[]> _receivedAction;
        private Func<IConnection, byte[], CancellationToken, Task> _receivedActionAsync;
        private Func<IConnection, Stream, CancellationToken, Task> _receivedActionStreamAsync;
        private Action<IConnection, ConnectionState, ConnectionState> _connectionStateChangedAction;
        protected readonly StateMachine<ConnectionState, ConnectionTrigger> _connectionStateMachine;

        private int _receivedMessageCounter;
        private int _sentMessageCounter;

        public RedundantConnection(IConnection[] connections)
        {
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));

            if (_connections.Length == 0)
            {
                throw new InvalidOperationException("At least one connection is required for a redundant channel");
            }

            _connectionStateMachine = new StateMachine<ConnectionState, ConnectionTrigger>(ConnectionState.Disconnected);

            _connectionStateMachine.OnTransitioned((transition) =>
                _connectionStateChangedAction?.Invoke(this, transition.Source, transition.Destination));

            _connectionStateMachine.Configure(ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.Connect, ConnectionState.Connecting)
                .OnEntryFrom(ConnectionTrigger.Disconnect, () =>
                {
                    //System.Diagnostics.Debug.WriteLine($"{GetType()}: ConnectionState.Disconnected OnEntryFrom(ConnectionTrigger.Disconnect)");
                    StopChildConnections();
                })
                ;

            _connectionStateMachine.Configure(ConnectionState.Connecting)
                .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
                .Permit(ConnectionTrigger.LinkError, ConnectionState.LinkError)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .OnEntry(() =>
                {
                    //System.Diagnostics.Debug.WriteLine($"{GetType()}: ConnectionState.Connecting OnEntry()");
                    StartChildConnections();
                })
                ;

            _connectionStateMachine.Configure(ConnectionState.Connected)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.LinkError, ConnectionState.LinkError)
                .PermitReentry(ConnectionTrigger.Connected);
                ;

            _connectionStateMachine.Configure(ConnectionState.LinkError)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
                .OnEntry(() =>
                {
                    //System.Diagnostics.Debug.WriteLine($"{GetType()}: ConnectionState.LinkError OnEntry");
                    _receivedMessageCounter = _sentMessageCounter = 0;
                    RestartChildConnections();
                });
                ;

        }

        private void RestartChildConnections()
        {
            foreach (var childConnection in _connections)
            {
                childConnection.Restart();
            }
        }

        private void StartChildConnections()
        {
            foreach (var childConnection in _connections)
            {
                childConnection.Start(

                    receivedActionStreamAsync: 
                        (connection, networkStream, cancellationToken) 
                            => ServiceRef.CallAndWaitAsync(this, async () => await OnReceivedDataFromChildChannel(connection, networkStream, cancellationToken)),

                    connectionStateChangedAction: 
                        (connection, fromState, toState) 
                            => ServiceRef.Call(this, () => OnChildChannelConnectionStateChanged(connection, fromState, toState)));
            }
        }

        private void OnChildChannelConnectionStateChanged(IConnection connection, ConnectionState fromState, ConnectionState toState)
        {
            System.Diagnostics.Debug.WriteLine($"{connection.GetType()} connection state changed from {fromState} to {toState}");

            if (toState == ConnectionState.Connected)
                //even if only one connection is connected I'm connected
                _connectionStateMachine.Fire(ConnectionTrigger.Connected);
            else if (toState == ConnectionState.LinkError)
                //check if all connections are down
                if (_connections.Except(new[] { connection }).All(_ => _.State == ConnectionState.LinkError))
                    _connectionStateMachine.Fire(ConnectionTrigger.LinkError);
        }

        private async Task OnReceivedDataFromChildChannel(IConnection connection, Stream networkStream, CancellationToken cancellationToken)
        {
            int messageCounter = BitConverter.ToInt32(await networkStream.ReadBytesAsync(4), 0);

            if (messageCounter == _receivedMessageCounter + 1)
            {
                //ok new message in sync arrived
                _receivedMessageCounter++;

#pragma warning disable IDE0068 // Use recommended dispose pattern
                var thisRef = ServiceRef.Create<IConnection>(this);
#pragma warning restore IDE0068 // Use recommended dispose pattern

                if (_receivedActionStreamAsync != null)
                {
                    await _receivedActionStreamAsync(thisRef, networkStream, cancellationToken);
                    await networkStream.FlushAsync();
                }
                else
                {
                    var messageData = await networkStream.ReadBytesAsync((int)networkStream.Length - 4/* messageCounter size */);

                    if (_receivedActionAsync != null)
                        await _receivedActionAsync(thisRef, messageData, cancellationToken);
                    else 
                        _receivedAction?.Invoke(thisRef, messageData);
                }
            }
            else if (messageCounter <= _receivedMessageCounter)
            {
                //ok it's an old message from slow or reconnected channel
                //nothing to do
            }
            else //if (messageCounter > _receivedMessageCounter)
            {
                //not good: I'm no more in sync with other peer
                //a reconnection is required
                _connectionStateMachine.Fire(ConnectionTrigger.LinkError);
            }
        }

        private void StopChildConnections()
        {
            foreach (var childConnection in _connections)
            {
                childConnection.Stop();
            }
        }

        public ConnectionState State { get => _connectionStateMachine.State; }

        public async Task SendDataAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_connectionStateMachine.State == ConnectionState.Connected)
            {
                _sentMessageCounter++;

                foreach (var childConnection in _connections)
                {
                    var buffer = new byte[4 + data.Length];
                    BitConverter.GetBytes(_sentMessageCounter).CopyTo(buffer, 0);
                    data.CopyTo(buffer, 4);
                    await childConnection.SendDataAsync(buffer, cancellationToken);
                }
            }
        }

#if NETSTANDARD2_1
        public async Task SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (_connectionStateMachine.State == ConnectionState.Connected)
            {
                _sentMessageCounter++;

                foreach (var childConnection in _connections)
                {
                    int bufferLength = 4 + data.Length;
                    using var memoryBufferOwner = MemoryPool<byte>.Shared.Rent(bufferLength);

                    if (!BitConverter.TryWriteBytes(memoryBufferOwner.Memory.Slice(0, 4).Span, _sentMessageCounter))
                    {
                        throw new InvalidOperationException();
                    }

                    data.CopyTo(memoryBufferOwner.Memory.Slice(4));
                    
                    await childConnection.SendDataAsync(memoryBufferOwner.Memory, cancellationToken);
                }
            }
        }
#endif

        public void Start(Action<IConnection, byte[]> receivedAction = null, Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync = null, Func<IConnection, Stream, CancellationToken, Task> receivedActionStreamAsync = null, Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null)
        {
            _receivedAction = receivedAction ?? _receivedAction;
            _receivedActionAsync = receivedActionAsync ?? _receivedActionAsync;
            _receivedActionStreamAsync = receivedActionStreamAsync ?? _receivedActionStreamAsync;

            if (new[] { _receivedAction != null, _receivedActionAsync != null, _receivedActionStreamAsync != null }.Count(_ => _) > 1)
            {
                throw new InvalidOperationException("No more than one received action can be specified");
            }

            _connectionStateChangedAction = connectionStateChangedAction ?? _connectionStateChangedAction;
            _connectionStateMachine.Fire(ConnectionTrigger.Connect);
        }

        public void Stop()
        {
            if (_connectionStateMachine.State != ConnectionState.Disconnected)
                _connectionStateMachine.Fire(ConnectionTrigger.Disconnect);
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls
        private readonly IConnection[] _connections;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RedundantConnection()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
