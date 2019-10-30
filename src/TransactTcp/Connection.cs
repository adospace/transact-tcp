using ServiceActor;
using Stateless;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal abstract class Connection : IConnection
    {
        private Action<IConnection, byte[]> _receivedAction;
        private Func<IConnection, byte[], CancellationToken, Task> _receivedActionAsync;
        private Func<IConnection, NetworkBufferedReadStream, CancellationToken, Task> _receivedActionStreamAsync;
        private Action<IConnection, ConnectionState, ConnectionState> _connectionStateChangedAction;

        protected readonly ConnectionSettings _connectionSettings;
        private CancellationTokenSource _connectCancellationTokenSource;
        private CancellationTokenSource _receiveLoopCancellationTokenSource;
        private CancellationTokenSource _sendKeepAliveLoopCancellationTokenSource;
        protected readonly StateMachine<ConnectionState, ConnectionTrigger> _connectionStateMachine;
        protected Stream _connectedStream;
        private AutoResetEvent _sendKeepAliveResetEvent;
        private Task _receiveLoopTask;
        private Task _sendKeepAliveLoopTask;
        private AutoResetEvent _receiveLoopTaskIsRunningEvent;
        private AutoResetEvent _sendKeepAliveTaskIsRunningEvent;

        private static readonly byte[] _keepAlivePacket = new byte[] { 0x0, 0x0, 0x0, 0x0 };

        protected Connection(
            ConnectionSettings connectionSettings = null)
        {
            _connectionSettings = connectionSettings ?? new ConnectionSettings();
            _connectionStateMachine = new StateMachine<ConnectionState, ConnectionTrigger>(ConnectionState.Disconnected);

            _connectionStateMachine.OnTransitioned((transition) =>
            {
                try
                {
                    _connectionStateChangedAction?.Invoke(this, transition.Source, transition.Destination);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"({GetType()}){Environment.NewLine}{ex}");
                }
            });

            ConfigureStateMachine();
        }

        protected virtual void ConfigureStateMachine()
        {
            _connectionStateMachine.Configure(ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.Connect, ConnectionState.Connecting)
                .OnEntryFrom(ConnectionTrigger.Disconnect, () =>
                {
                    OnDisconnect();
                });

            _connectionStateMachine.Configure(ConnectionState.Connecting)
                .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
                .Permit(ConnectionTrigger.LinkError, ConnectionState.LinkError)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .OnEntry(() => Task.Run(OnConnectAsyncCore))
                .OnExit(() => _connectCancellationTokenSource?.Cancel());

            _connectionStateMachine.Configure(ConnectionState.Connected)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.LinkError, ConnectionState.LinkError)
                .OnEntry(() =>
                {
                    using (_receiveLoopTaskIsRunningEvent = new AutoResetEvent(false))
                    using (_sendKeepAliveTaskIsRunningEvent = new AutoResetEvent(false))
                    {
                        _receiveLoopTask = Task.Run(ReceiveLoopAsync);
                        _sendKeepAliveLoopTask = Task.Run(() => SendKeepAliveLoopAsync());

                        if (!_receiveLoopTaskIsRunningEvent.WaitOne())
                            throw new InvalidOperationException();

                        if (!_sendKeepAliveTaskIsRunningEvent.WaitOne())
                            throw new InvalidOperationException();
                    }

                    _receiveLoopTaskIsRunningEvent = null;
                    _sendKeepAliveTaskIsRunningEvent = null;
                })
                .OnExit(() =>
                {
                    _receiveLoopCancellationTokenSource?.Cancel();
                    _sendKeepAliveLoopCancellationTokenSource?.Cancel();
                    _sendKeepAliveResetEvent?.Set();
                });

            _connectionStateMachine.Configure(ConnectionState.LinkError)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
                .OnEntryFrom(ConnectionTrigger.LinkError, () =>
                {
                    OnDisconnect();
                    if (_connectionSettings.AutoReconnect)
                        Task.Run(OnConnectAsyncCore);
                });
        }

        private void SendKeepAliveLoopAsync()
        {
            try
            {
                using (_sendKeepAliveResetEvent = new AutoResetEvent(false))
                using (_sendKeepAliveLoopCancellationTokenSource = new CancellationTokenSource()) 
                {
                    _sendKeepAliveTaskIsRunningEvent.Set();

                    if (_connectedStream.CanTimeout)
                        _connectedStream.ReadTimeout = _connectionSettings.KeepAliveMilliseconds * 2;

                    while (true)
                    {
                        if (!_sendKeepAliveResetEvent.WaitOne(_connectionSettings.KeepAliveMilliseconds))
                        {
                            _sendKeepAliveLoopCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            ServiceRef.Call(this, async () =>
                            {
                                if (_connectionStateMachine.State == ConnectionState.Connected && IsStreamConnected)
                                {
                                    await _connectedStream.WriteAsync(_keepAlivePacket, 0, _keepAlivePacket.Length, _sendKeepAliveLoopCancellationTokenSource.Token);
                                }
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
#if DEBUG
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
#else
            catch (Exception)
            {
#endif
                _sendKeepAliveLoopCancellationTokenSource = null;
                _sendKeepAliveResetEvent = null;
                
                ServiceRef.Call(this, () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connected)
                        _connectionStateMachine.Fire(ConnectionTrigger.LinkError);
                });
            }
            finally
            {
                _sendKeepAliveLoopCancellationTokenSource = null;
                _sendKeepAliveResetEvent = null;
            }
        }

        private readonly byte[] _messageSizeBuffer = new byte[4];

        private async Task ReceiveLoopAsync()
        {
#pragma warning disable IDE0068 // Use recommended dispose pattern
            var refToThis = ServiceRef.Create<IConnection>(this);
#pragma warning restore IDE0068 // Use recommended dispose pattern
            try
            {
                using (_receiveLoopCancellationTokenSource = new CancellationTokenSource())
                {
                    _receiveLoopTaskIsRunningEvent.Set();

                    var cancellationToken = _receiveLoopCancellationTokenSource.Token;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await _connectedStream.ReadBufferedAsync(_messageSizeBuffer, cancellationToken);

                        var messageLength = BitConverter.ToInt32(_messageSizeBuffer, 0);
                        if (messageLength == 0)
                            continue;

                        cancellationToken.ThrowIfCancellationRequested();

                        if (_receivedActionStreamAsync != null)
                        {
                            var bufferedStream = new NetworkBufferedReadStream(_connectedStream, messageLength);
                            await _receivedActionStreamAsync(refToThis, bufferedStream, cancellationToken);
                            await bufferedStream.FlushAsync();
                        }
                        else
                        {
                            var messageBuffer = new byte[messageLength];
                            
                            await _connectedStream.ReadBufferedAsync(messageBuffer, cancellationToken);

                            if (_receivedActionAsync != null)
                                await _receivedActionAsync(refToThis, messageBuffer, cancellationToken);
                            else //if (_receivedAction != null)
                                _receivedAction(refToThis, messageBuffer);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
#if DEBUG
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{GetType()}{Environment.NewLine}{ex}");
#else
            catch (Exception)
            {
#endif                
                _receiveLoopCancellationTokenSource = null;

                ServiceRef.Call(this, () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connected)
                        _connectionStateMachine.Fire(ConnectionTrigger.LinkError);
                });
            }
            finally
            {
                _receiveLoopCancellationTokenSource = null;
            }
        }

        protected virtual void OnDisconnect()
        {
            if (_receiveLoopTask != null)
            {
                Task.WaitAll(_receiveLoopTask, _sendKeepAliveLoopTask);
                System.Diagnostics.Debug.Assert(_receiveLoopCancellationTokenSource == null);
                System.Diagnostics.Debug.Assert(_sendKeepAliveLoopCancellationTokenSource == null);
                _receiveLoopTask.Dispose();
                _sendKeepAliveLoopTask.Dispose();
                _receiveLoopTask = null;
                _sendKeepAliveLoopTask = null;
            }

            _connectedStream?.Close();
            _connectedStream = null;
        }

        protected abstract bool IsStreamConnected { get; }

        private async Task OnConnectAsyncCore()
        {
            try
            {
                using (_connectCancellationTokenSource = new CancellationTokenSource())
                {
                    await OnConnectAsync(_connectCancellationTokenSource.Token);

                    _connectCancellationTokenSource.Token.ThrowIfCancellationRequested();
                }

                _connectCancellationTokenSource = null;

                ServiceRef.Call(this, () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connecting ||
                        _connectionStateMachine.State == ConnectionState.LinkError)
                    {
                        _connectionStateMachine.Fire(ConnectionTrigger.Connected);
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
#if DEBUG
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{GetType()}{Environment.NewLine}{ex}");
#else
            catch (Exception)
            {
#endif                
                _connectCancellationTokenSource = null;

                ServiceRef.Call(this, () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connecting)
                        _connectionStateMachine.Fire(ConnectionTrigger.LinkError);
                });
            }
            finally
            {
                _connectCancellationTokenSource = null;
            }
        }

        protected abstract Task OnConnectAsync(CancellationToken cancellationToken);

        public ConnectionState State { get => _connectionStateMachine.State; }
        public async Task SendDataAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_connectionStateMachine.State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                var lenInBytes = BitConverter.GetBytes(data.Length);

                try
                {
                    await _connectedStream?.WriteAsync(lenInBytes, 0, 4, cancellationToken);
                    await _connectedStream?.WriteAsync(data, 0, data.Length, cancellationToken);
                }
                catch (Exception)
                {
                    _connectionStateMachine.Fire(ConnectionTrigger.LinkError);
                }
            }
        }

#if NETSTANDARD2_1
        public async Task SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (_connectionStateMachine.State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                using var memoryBufferOwner = MemoryPool<byte>.Shared.Rent(4);

                if (!BitConverter.TryWriteBytes(memoryBufferOwner.Memory.Span, data.Length))
                {
                    throw new InvalidOperationException();
                }                

                try
                {

                    await _connectedStream?.WriteAsync(memoryBufferOwner.Memory.Slice(0, 4), cancellationToken).AsTask();
                    await _connectedStream?.WriteAsync(data, cancellationToken).AsTask();
                }
                catch (Exception)
                {
                    _connectionStateMachine.Fire(ConnectionTrigger.LinkError);
                }
            }
        }
#endif

        public virtual void Start(Action<IConnection, byte[]> receivedAction = null,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync = null,
            Func<IConnection, Stream, CancellationToken, Task> receivedActionStreamAsync = null,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null)
        {
            _receivedAction = receivedAction ?? _receivedAction;
            _receivedActionAsync = receivedActionAsync ?? _receivedActionAsync;
            _receivedActionStreamAsync = receivedActionStreamAsync ?? _receivedActionStreamAsync;

            if (new [] { _receivedAction != null, _receivedActionAsync != null, _receivedActionStreamAsync != null }.Count(_=>_) > 1)
            {
                throw new InvalidOperationException("No more than one received action can be specified");
            }

            _connectionStateChangedAction = connectionStateChangedAction ?? _connectionStateChangedAction;
            _connectionStateMachine.Fire(ConnectionTrigger.Connect);
        }

        public virtual void Stop()
        {
            if (_connectionStateMachine.State != ConnectionState.Disconnected)
                _connectionStateMachine.Fire(ConnectionTrigger.Disconnect);
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Stop();
                    _connectCancellationTokenSource?.Dispose();
                    _sendKeepAliveLoopCancellationTokenSource?.Dispose();
                    _receiveLoopCancellationTokenSource?.Dispose();
                    _sendKeepAliveResetEvent?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Connection()
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
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
