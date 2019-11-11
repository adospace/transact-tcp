using Nito.AsyncEx;
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
        private Func<IConnection, Stream, CancellationToken, Task> _receivedActionStreamAsync;
        private Action<IConnection, ConnectionState, ConnectionState> _connectionStateChangedAction;

        protected readonly ConnectionSettings _connectionSettings;
        private CancellationTokenSource _connectCancellationTokenSource;
        private CancellationTokenSource _receiveLoopCancellationTokenSource;
        private CancellationTokenSource _sendKeepAliveLoopCancellationTokenSource;
        protected readonly StateMachine<ConnectionState, ConnectionTrigger> _connectionStateMachine;

        protected Stream _connectedStream;
        private Stream _sendingStream;
        private Stream _receivingStream;

        private AsyncAutoResetEvent _sendKeepAliveResetEvent;
        private Task _receiveLoopTask;
        private Task _sendKeepAliveLoopTask;
        private AsyncAutoResetEvent _receiveLoopTaskIsRunningEvent;
        private AsyncAutoResetEvent _sendKeepAliveTaskIsRunningEvent;

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
#if DEBUG
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"({GetType()}){Environment.NewLine}{ex}");
#else
                catch (Exception)
                {
#endif
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
                .OnEntry(() => BeginConnection())
                .OnExit(() => _connectCancellationTokenSource?.Cancel());

            _connectionStateMachine.Configure(ConnectionState.Connected)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.LinkError, ConnectionState.LinkError)
                .OnEntryAsync(async () =>
                {
                    _receiveLoopTaskIsRunningEvent = new AsyncAutoResetEvent();
                    _sendKeepAliveTaskIsRunningEvent = new AsyncAutoResetEvent();

                    _receiveLoopTask = Task.Run(ReceiveLoopAsync);
                    if (_connectionSettings.KeepAliveMilliseconds > 0)
                    {
                        _sendKeepAliveLoopTask = Task.Run(() => SendKeepAliveLoopAsync());
                    }

                    await _receiveLoopTaskIsRunningEvent.WaitAsync();

                    if (_connectionSettings.KeepAliveMilliseconds > 0)
                    {
                        await _sendKeepAliveTaskIsRunningEvent.WaitAsync();
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
                .Permit(ConnectionTrigger.Connect, ConnectionState.Connecting)
                .OnEntryFrom(ConnectionTrigger.LinkError, () =>
                {
                    OnDisconnect();
                });
        }

        private async Task SendKeepAliveLoopAsync()
        {
            try
            {
                _sendKeepAliveResetEvent = new AsyncAutoResetEvent(false);

                if (_sendKeepAliveLoopCancellationTokenSource != null)
                {
                    _sendKeepAliveLoopCancellationTokenSource.Dispose();
                    _sendKeepAliveLoopCancellationTokenSource = null;
                }

                _sendKeepAliveLoopCancellationTokenSource = new CancellationTokenSource();

                _sendKeepAliveTaskIsRunningEvent.Set();

                if (_connectedStream.CanTimeout)
                    _connectedStream.ReadTimeout = _connectionSettings.KeepAliveMilliseconds * 2;

                while (true)
                {
                    if (!await _sendKeepAliveResetEvent.WaitAsync(_connectionSettings.KeepAliveMilliseconds))
                    {
                        _sendKeepAliveLoopCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        await ServiceRef.CallAndWaitAsync(this, async () =>
                        {
                            if (_connectionStateMachine.State == ConnectionState.Connected && IsStreamConnected)
                            {
                                await _sendingStream.WriteAsync(_keepAlivePacket, 0, _keepAlivePacket.Length, _sendKeepAliveLoopCancellationTokenSource.Token);
                            }
                        });
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
                //_sendKeepAliveLoopCancellationTokenSource = null;
                //_sendKeepAliveResetEvent = null;

                ServiceRef.Call(this, async () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connected)
                        await _connectionStateMachine.FireAsync(ConnectionTrigger.LinkError);
                });
            }
            finally
            {
                //_sendKeepAliveLoopCancellationTokenSource = null;
                //_sendKeepAliveResetEvent = null;
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
                if (_receiveLoopCancellationTokenSource != null)
                {
                    _receiveLoopCancellationTokenSource.Dispose();
                    _receiveLoopCancellationTokenSource = null;
                }

                _receiveLoopCancellationTokenSource = new CancellationTokenSource();
                _receiveLoopTaskIsRunningEvent.Set();

                var cancellationToken = _receiveLoopCancellationTokenSource.Token;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await _receivingStream.ReadBufferedAsync(_messageSizeBuffer, cancellationToken);

                    var messageLength = BitConverter.ToInt32(_messageSizeBuffer, 0);
                    if (messageLength == 0)
                        continue;

                    cancellationToken.ThrowIfCancellationRequested();

                    if (messageLength == -1)
                    {
                        if (_receivedActionStreamAsync != null)
                        {
                            await ServiceRef.CallAndWaitAsync(this, async () =>
                                await _receivedActionStreamAsync(refToThis, _receivingStream, cancellationToken));
                        }
                        else
                        {
                            throw new InvalidOperationException("ReceiveActionStreamAsync must be specified to handle variable length messages");
                        }
                    }
                    else
                    {
                        if (_receivedActionStreamAsync != null)
                        {
#pragma warning disable IDE0067 // Dispose objects before losing scope
                            var bufferedStream = new NetworkBufferedReadStream(_receivingStream, messageLength);
#pragma warning restore IDE0067 // Dispose objects before losing scope
                            await ServiceRef.CallAndWaitAsync(this, async () =>
                                await _receivedActionStreamAsync(refToThis, bufferedStream, cancellationToken));
                            await bufferedStream.FlushAsync();
                        }
                        else
                        {
                            var messageBuffer = new byte[messageLength];

                            await _receivingStream.ReadBufferedAsync(messageBuffer, cancellationToken);

                            if (_receivedActionAsync != null)
                                await ServiceRef.CallAndWaitAsync(this, async () =>
                                    await _receivedActionAsync(refToThis, messageBuffer, cancellationToken));
                            else //if (_receivedAction != null)
                                ServiceRef.CallAndWait(this, () => _receivedAction(refToThis, messageBuffer));
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
                //_receiveLoopCancellationTokenSource = null;

                ServiceRef.Call(this, async () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connected)
                        await _connectionStateMachine.FireAsync(ConnectionTrigger.LinkError);
                });
            }
            finally
            {
                //_receiveLoopCancellationTokenSource = null;
            }
        }

        protected virtual void OnDisconnect()
        {
            if (_receiveLoopTask != null)
            {
                if (_sendKeepAliveLoopTask == null)
                    _receiveLoopTask.Wait();
                else
                    Task.WaitAll(_receiveLoopTask, _sendKeepAliveLoopTask);
                //System.Diagnostics.Debug.Assert(_receiveLoopCancellationTokenSource == null);
                //System.Diagnostics.Debug.Assert(_sendKeepAliveLoopCancellationTokenSource == null);
                _receiveLoopTask.Dispose();
                _sendKeepAliveLoopTask?.Dispose();
                _receiveLoopTask = null;
                _sendKeepAliveLoopTask = null;
            }

            _connectedStream?.Close();
            _connectedStream = null;
            _sendingStream = null;
        }

        protected abstract bool IsStreamConnected { get; }

        protected void BeginConnection()
            => Task.Run(OnConnectAsyncCore);

        private async Task OnConnectAsyncCore()
        {
            try
            {
                if (_connectCancellationTokenSource != null)
                {
                    _connectCancellationTokenSource.Dispose();
                    _connectCancellationTokenSource = null;
                }

                _connectCancellationTokenSource = new CancellationTokenSource();

                await OnConnectAsync(_connectCancellationTokenSource.Token);

                _connectCancellationTokenSource.Token.ThrowIfCancellationRequested();

                System.Diagnostics.Debug.Assert(_connectedStream != null, "Connected stream must be not null here!");

                if (_connectionSettings.UseBufferedStream)
                {
                    _sendingStream = new NetworkWriteStream(new BufferedStream(_connectedStream));
                    _receivingStream = new NetworkReadStream(new BufferedStream(_connectedStream));
                }
                else
                {
                    _sendingStream = new NetworkWriteStream(_connectedStream);
                    _receivingStream = new NetworkReadStream(_connectedStream);
                }

                ServiceRef.Call(this, async () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connecting ||
                        _connectionStateMachine.State == ConnectionState.LinkError)
                    {
                        await _connectionStateMachine.FireAsync(ConnectionTrigger.Connected);
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
                ServiceRef.Call(this, async () =>
                {
                    if (_connectionStateMachine.State == ConnectionState.Connecting)
                        await _connectionStateMachine.FireAsync(ConnectionTrigger.LinkError);
                });
            }
            finally
            {
            }
        }

        protected abstract Task OnConnectAsync(CancellationToken cancellationToken);

        public ConnectionState State { get => _connectionStateMachine.State; }

        public async Task SendDataAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (_connectionStateMachine.State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                var lenInBytes = BitConverter.GetBytes(data.Length);

                try
                {
                    //if (_connectionSettings.UseBufferedStream)
                    //{
                    //    _sendingStream?.Write(lenInBytes, 0, 4);
                    //    _sendingStream?.Write(data, 0, data.Length);
                    //    await _sendingStream?.FlushAsync(cancellationToken);
                    //}
                    //else
                    //{
                    await _sendingStream?.WriteAsync(lenInBytes, 0, 4, cancellationToken);
                    await _sendingStream?.WriteAsync(data, 0, data.Length, cancellationToken);
                    await _sendingStream?.FlushAsync(cancellationToken);
                    //}
                }
                catch (Exception)
                {
                    await _connectionStateMachine.FireAsync(ConnectionTrigger.LinkError);
                }
            }
        }

        private static readonly byte[] _nullLength = BitConverter.GetBytes(-1);

#if NETSTANDARD2_1
        public async Task SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (_connectionStateMachine.State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                try
                {
                    //if (_connectionSettings.UseBufferedStream)
                    //{
                    //    var lenInBytes = BitConverter.GetBytes(data.Length);
                    //    _sendingStream?.Write(lenInBytes, 0, 4);
                    //    _sendingStream?.Write(data.ToArray());
                    //    await _sendingStream?.FlushAsync();
                    //}
                    //else
                    //{
                    using var memoryBufferOwner = MemoryPool<byte>.Shared.Rent(4);

                    if (!BitConverter.TryWriteBytes(memoryBufferOwner.Memory.Span, data.Length))
                    {
                        throw new InvalidOperationException();
                    }

                    await _sendingStream?.WriteAsync(memoryBufferOwner.Memory.Slice(0, 4), cancellationToken).AsTask();
                    await _sendingStream?.WriteAsync(data, cancellationToken).AsTask();
                    await _sendingStream?.FlushAsync();
                    //}
                }
#if DEBUG
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"{GetType()}{Environment.NewLine}{ex}");
#else
                catch (Exception)
                {
#endif
                    await _connectionStateMachine.FireAsync(ConnectionTrigger.LinkError);
                }
            }
        }
#endif

        public async Task SendAsync(Func<Stream, CancellationToken, Task> sendFunction, CancellationToken cancellationToken)
        {
            if (sendFunction == null)
            {
                throw new ArgumentNullException(nameof(sendFunction));
            }

            if (_connectionStateMachine.State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                try
                {
                    //if (_connectionSettings.UseBufferedStream)
                    //{
                    _sendingStream?.Write(_nullLength, 0, 4);
                    //    await sendFunction(_sendingStream, cancellationToken);
                    //    await _sendingStream.FlushAsync();
                    //}
                    //else
                    //{
                    //await _sendingStream?.WriteAsync(_nullLength, 0, 4, cancellationToken);
                    await sendFunction(_sendingStream, cancellationToken);
                    await _sendingStream?.FlushAsync();
                    //}
                }
#if DEBUG
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
#else
                catch (Exception)
                {
#endif
                    await _connectionStateMachine.FireAsync(ConnectionTrigger.LinkError);
                }
            }
        }

        public virtual void Start(Action<IConnection, byte[]> receivedAction = null,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync = null,
            Func<IConnection, Stream, CancellationToken, Task> receivedActionStreamAsync = null,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null)
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
                    _connectCancellationTokenSource = null;
                    _sendKeepAliveLoopCancellationTokenSource?.Dispose();
                    _sendKeepAliveLoopCancellationTokenSource = null;
                    _receiveLoopCancellationTokenSource?.Dispose();
                    _receiveLoopCancellationTokenSource = null;
                    //_sendKeepAliveResetEvent?.Dispose();
                    _sendKeepAliveResetEvent = null;
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
