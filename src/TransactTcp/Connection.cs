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
        //protected readonly StateMachine<ConnectionState, ConnectionTrigger> _connectionStateMachine;

        protected Stream _connectedStream;
        private Stream _sendingStream;
        private Stream _receivingStream;

        private AsyncAutoResetEvent _sendKeepAliveResetEvent;
        private Task _receiveLoopTask;
        private Task _sendKeepAliveLoopTask;
        private AsyncAutoResetEvent _receiveLoopTaskIsRunningEvent;
        private AsyncAutoResetEvent _sendKeepAliveTaskIsRunningEvent;

        private static readonly byte[] _keepAlivePacket = new byte[] { 0x0, 0x0, 0x0, 0x0 };

        private readonly bool _messageFramingEnabled = true;

        protected Connection(
            bool messageFramingEnabled,
            ConnectionSettings connectionSettings = null)
        {
            _messageFramingEnabled = messageFramingEnabled;
            _connectionSettings = connectionSettings ?? new ConnectionSettings();
            //            _connectionStateMachine = new StateMachine<ConnectionState, ConnectionTrigger>(ConnectionState.Disconnected);

            //            _connectionStateMachine.OnTransitionedAsync((transition) =>
            //            {
            //                try
            //                {
            //                    _connectionStateChangedAction?.Invoke(this, transition.Source, transition.Destination);
            //                }
            //#if DEBUG
            //                catch (Exception ex)
            //                {
            //                    System.Diagnostics.Debug.WriteLine($"({GetType()}){Environment.NewLine}{ex}");
            //#else
            //                catch (Exception)
            //                {
            //#endif
            //                }

            //                return Task.CompletedTask;
            //            });

            //ConfigureStateMachine();
        }

        public ConnectionState State { get; private set; }

        private async Task RunConnection()
        {
            _receiveLoopTaskIsRunningEvent = new AsyncAutoResetEvent();
            _sendKeepAliveTaskIsRunningEvent = new AsyncAutoResetEvent();

            _receiveLoopTask = Task.Run(()
                => ReceiveLoopAsync((_receiveLoopCancellationTokenSource = new CancellationTokenSource()).Token));

            if (_connectionSettings.KeepAliveMilliseconds > 0)
            {
                _sendKeepAliveLoopTask = Task.Run(() => SendKeepAliveLoopAsync(
                    _sendKeepAliveResetEvent = new AsyncAutoResetEvent(false),
                    (_sendKeepAliveLoopCancellationTokenSource = new CancellationTokenSource()).Token));
            }

            await _receiveLoopTaskIsRunningEvent.WaitAsync().ConfigureAwait(false);

            if (_connectionSettings.KeepAliveMilliseconds > 0)
            {
                await _sendKeepAliveTaskIsRunningEvent.WaitAsync().ConfigureAwait(false);
            }

            _receiveLoopTaskIsRunningEvent = null;
            _sendKeepAliveTaskIsRunningEvent = null;
        }

        protected virtual void SetState(ConnectionTrigger connectionTrigger)
        {
            var currentState = State;
            var newState = State;

            System.Diagnostics.Debug.WriteLine($"{GetType()} SetState({currentState}) -> {connectionTrigger}");

            switch (currentState)
            {
                case ConnectionState.Connecting:
                    {
                        if (connectionTrigger == ConnectionTrigger.Disconnect)
                        {
                            _connectCancellationTokenSource?.Cancel();
                            OnDisconnect();
                            newState = ConnectionState.Disconnected;
                        }
                        else if (connectionTrigger == ConnectionTrigger.LinkError)
                        {
                            _connectCancellationTokenSource?.Cancel();
                            OnDisconnect();
                            newState = ConnectionState.LinkError;
                        }
                        else if (connectionTrigger == ConnectionTrigger.Connected)
                        {
                            newState = ConnectionState.Connected;
                        }
                        else throw new InvalidOperationException();
                    }
                    break;
                case ConnectionState.Disconnected:
                    {
                        if (connectionTrigger == ConnectionTrigger.Connect)
                        {
                            BeginConnection();
                            newState = ConnectionState.Connecting;
                        }
                        else throw new InvalidOperationException();
                    }
                    break;
                case ConnectionState.Connected:
                    {
                        if (connectionTrigger == ConnectionTrigger.LinkError)
                        {
                            _receiveLoopCancellationTokenSource?.Cancel();
                            _sendKeepAliveLoopCancellationTokenSource?.Cancel();
                            _sendKeepAliveResetEvent?.Set();
                            OnDisconnect();
                            newState = ConnectionState.LinkError;
                        }
                        else if (connectionTrigger == ConnectionTrigger.Disconnect)
                        {
                            _connectCancellationTokenSource?.Cancel();
                            _receiveLoopCancellationTokenSource?.Cancel();
                            _sendKeepAliveLoopCancellationTokenSource?.Cancel();
                            _sendKeepAliveResetEvent?.Set();
                            OnDisconnect();
                            newState = ConnectionState.Disconnected;
                        }
                    }
                    break;
                case ConnectionState.LinkError:
                    {
                        if (connectionTrigger == ConnectionTrigger.Disconnect)
                        {
                            _connectCancellationTokenSource?.Cancel();
                            _receiveLoopCancellationTokenSource?.Cancel();
                            _sendKeepAliveLoopCancellationTokenSource?.Cancel();
                            _sendKeepAliveResetEvent?.Set();
                            newState = ConnectionState.Disconnected;
                        }
                        else if (connectionTrigger == ConnectionTrigger.Connected)
                        {
                            newState = ConnectionState.Connected;
                        }

                    }
                    break;
            }

            if (newState == currentState)
                return;

            State = newState;

            System.Diagnostics.Debug.WriteLine($"{GetType()} SetState({currentState}) -> {connectionTrigger} -> {newState}");

            _connectionStateChangedAction?.Invoke(ServiceRef.Create<IConnection>(this), currentState, newState);

        }

        //protected virtual async Task SetStateAsync(ConnectionTrigger connectionTrigger)
        //{
        //    var currentState = State;
        //    var newState = State;

        //    System.Diagnostics.Debug.WriteLine($"{GetType()} SetStateAsync({currentState}) -> {connectionTrigger}");

        //    switch (currentState)
        //    {
        //        case ConnectionState.Connecting:
        //            {
        //                if (connectionTrigger == ConnectionTrigger.Connected)
        //                {
        //                    await RunConnection();
        //                    newState = ConnectionState.Connected;
        //                }
        //                else throw new InvalidOperationException();
        //            }
        //            break;
        //        case ConnectionState.Disconnected:
        //            {
        //                throw new InvalidOperationException();
        //            }
        //        case ConnectionState.Connected:
        //            {
        //                throw new InvalidOperationException();
        //            }
        //        case ConnectionState.LinkError:
        //            {
        //                if (connectionTrigger == ConnectionTrigger.Connected)
        //                {
        //                    await RunConnection();
        //                    newState = ConnectionState.Connected;
        //                }
        //            }
        //            break;
        //    }

        //    if (newState == currentState)
        //        return;

        //    State = newState;

        //    System.Diagnostics.Debug.WriteLine($"{GetType()} SetState({currentState}) -> {connectionTrigger} -> {newState}");

        //    _connectionStateChangedAction?.Invoke(ServiceRef.Create<IConnection>(this), currentState, newState);
        //}

        //protected virtual void ConfigureStateMachine()
        //{
        //    _connectionStateMachine.Configure(ConnectionState.Disconnected)
        //        .Permit(ConnectionTrigger.Connect, ConnectionState.Connecting)
        //        .OnEntryFromAsync(ConnectionTrigger.Disconnect, async () =>
        //        {
        //            await OnDisconnect().ConfigureAwait(false);
        //        });

        //    _connectionStateMachine.Configure(ConnectionState.Connecting)
        //        .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
        //        .Permit(ConnectionTrigger.LinkError, ConnectionState.LinkError)
        //        .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
        //        .OnEntryAsync(async () => await ConnectAsyncCore().ConfigureAwait(false))
        //        .OnExitAsync(() =>
        //        {
        //            _connectCancellationTokenSource?.Cancel();
        //            return Task.CompletedTask;
        //        })
        //        .OnExit(() => _connectCancellationTokenSource?.Cancel());

        //    _connectionStateMachine.Configure(ConnectionState.Connected)
        //        .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
        //        .Permit(ConnectionTrigger.LinkError, ConnectionState.LinkError)
        //        .OnEntryAsync(async () =>
        //        {
        //            _receiveLoopTaskIsRunningEvent = new AsyncAutoResetEvent();
        //            _sendKeepAliveTaskIsRunningEvent = new AsyncAutoResetEvent();

        //            _receiveLoopTask = Task.Run(ReceiveLoopAsync);
        //            if (_connectionSettings.KeepAliveMilliseconds > 0)
        //            {
        //                _sendKeepAliveLoopTask = Task.Run(() => SendKeepAliveLoopAsync());
        //            }

        //            await _receiveLoopTaskIsRunningEvent.WaitAsync().ConfigureAwait(false);

        //            if (_connectionSettings.KeepAliveMilliseconds > 0)
        //            {
        //                await _sendKeepAliveTaskIsRunningEvent.WaitAsync().ConfigureAwait(false);
        //            }

        //            _receiveLoopTaskIsRunningEvent = null;
        //            _sendKeepAliveTaskIsRunningEvent = null;
        //        })
        //        .OnExitAsync(() =>
        //        {
        //            _receiveLoopCancellationTokenSource?.Cancel();
        //            _sendKeepAliveLoopCancellationTokenSource?.Cancel();
        //            _sendKeepAliveResetEvent?.Set();
        //            return Task.CompletedTask;
        //        });

        //    _connectionStateMachine.Configure(ConnectionState.LinkError)
        //        .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
        //        .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
        //        .Permit(ConnectionTrigger.Connect, ConnectionState.Connecting)
        //        .OnEntryFromAsync(ConnectionTrigger.LinkError, async () =>
        //        {
        //            await OnDisconnect().ConfigureAwait(false);
        //        });
        //}

        private async Task SendKeepAliveLoopAsync(AsyncAutoResetEvent sendKeepAliveResetEvent, CancellationToken cancellationToken)
        {
            try
            {
                //_sendKeepAliveResetEvent = new AsyncAutoResetEvent(false);

                //if (_sendKeepAliveLoopCancellationTokenSource != null)
                //{
                //    _sendKeepAliveLoopCancellationTokenSource.Dispose();
                //    _sendKeepAliveLoopCancellationTokenSource = null;
                //}

                //_sendKeepAliveLoopCancellationTokenSource = new CancellationTokenSource();

                _sendKeepAliveTaskIsRunningEvent.Set();

                if (_connectedStream.CanTimeout)
                    _connectedStream.ReadTimeout = _connectionSettings.KeepAliveMilliseconds * 2;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!await sendKeepAliveResetEvent.WaitAsync(_connectionSettings.KeepAliveMilliseconds, cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await ServiceRef.CallAndWaitAsync(this, async () =>
                        {
                            if (State == ConnectionState.Connected && IsStreamConnected)
                            {
                                await _sendingStream.WriteAsync(_keepAlivePacket, 0, _keepAlivePacket.Length, cancellationToken).ConfigureAwait(false);
                            }
                        }).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"{GetType()} SendKeepAliveLoop Cancelled");
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

                ServiceRef.Call(this, () =>
                {
                    if (State == ConnectionState.Connected)
                        //await _connectionStateMachine.FireAsync(ConnectionTrigger.LinkError).ConfigureAwait(false);
                        SetState(ConnectionTrigger.LinkError);

                });
            }
            finally
            {
                //_sendKeepAliveLoopCancellationTokenSource = null;
                //_sendKeepAliveResetEvent = null;
            }
        }

        private readonly byte[] _messageSizeBuffer = new byte[4];

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
#pragma warning disable IDE0068 // Use recommended dispose pattern
            var refToThis = ServiceRef.Create<IConnection>(this);
#pragma warning restore IDE0068 // Use recommended dispose pattern
            try
            {
                //if (_receiveLoopCancellationTokenSource != null)
                //{
                //    _receiveLoopCancellationTokenSource.Dispose();
                //    _receiveLoopCancellationTokenSource = null;
                //}

                //_receiveLoopCancellationTokenSource = new CancellationTokenSource();
                _receiveLoopTaskIsRunningEvent.Set();

                //var cancellationToken = _receiveLoopCancellationTokenSource.Token;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int messageLength = -1;
                    if (_messageFramingEnabled)
                    {
                        await _receivingStream.ReadBufferedAsync(_messageSizeBuffer, cancellationToken).ConfigureAwait(false);

                        messageLength = BitConverter.ToInt32(_messageSizeBuffer, 0);
                        if (messageLength == 0)
                            continue;

                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (messageLength == -1)
                    {
                        if (_receivedActionStreamAsync != null)
                        {
                            //await ServiceRef.CallAndWaitAsync(this, () =>
                            //    _receivedActionStreamAsync(refToThis, _receivingStream, cancellationToken)).ConfigureAwait(false);
                            await _receivedActionStreamAsync(refToThis, _receivingStream, cancellationToken);

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
                                await _receivedActionStreamAsync(refToThis, bufferedStream, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                            await bufferedStream.FlushAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            var messageBuffer = new byte[messageLength];

                            await _receivingStream.ReadBufferedAsync(messageBuffer, cancellationToken).ConfigureAwait(false);

                            if (_receivedActionAsync != null)
                                await ServiceRef.CallAndWaitAsync(this, async () =>
                                    await _receivedActionAsync(refToThis, messageBuffer, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                            else //if (_receivedAction != null)
                                ServiceRef.CallAndWait(this, () => _receivedAction(refToThis, messageBuffer));
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"{GetType()} ReceiveLoopAsync Cancelled");
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

                ServiceRef.Call(this, () =>
                {
                    if (State == ConnectionState.Connected)
                        SetState(ConnectionTrigger.LinkError);
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
                //if (_sendKeepAliveLoopTask == null)
                //    await _receiveLoopTask.ConfigureAwait(false);
                //else
                //    await Task.WhenAll(_receiveLoopTask, _sendKeepAliveLoopTask).ConfigureAwait(false);
                //System.Diagnostics.Debug.Assert(_receiveLoopCancellationTokenSource == null);
                //System.Diagnostics.Debug.Assert(_sendKeepAliveLoopCancellationTokenSource == null);
                //_receiveLoopTask?.Dispose();
                //_sendKeepAliveLoopTask?.Dispose();
                _receiveLoopTask = null;
                _sendKeepAliveLoopTask = null;
            }

            _connectedStream?.Close();
            _connectedStream = null;
            _sendingStream = null;
        }

        protected abstract bool IsStreamConnected { get; }

        protected void BeginConnection()
            => Task.Run(()=>ConnectAsyncCore((_connectCancellationTokenSource = new CancellationTokenSource()).Token));

        protected async Task ConnectAsyncCore(CancellationToken cancellationToken)
        {
            try
            {
                await OnConnectAsync(cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                System.Diagnostics.Debug.Assert(_connectedStream != null, "Connected stream must be not null here!");

                if (_connectionSettings.UseBufferedStream)
                {
                    //_sendingStream = new NetworkWriteStream(new BufferedStream(_connectedStream));
                    //_receivingStream = new NetworkReadStream(new BufferedStream(_connectedStream));
                    _sendingStream = new BufferedStream(_connectedStream);
                    _receivingStream = new BufferedStream(_connectedStream);
                }
                else
                {
                    //_sendingStream = new NetworkWriteStream(_connectedStream);
                    //_receivingStream = new NetworkReadStream(_connectedStream);
                    _sendingStream = _connectedStream;
                    _receivingStream = _connectedStream;
                }

                ServiceRef.Call(this, async () =>
                {
                    if (State == ConnectionState.Connecting ||
                        State == ConnectionState.LinkError)
                    {
                        //await SetStateAsync(ConnectionTrigger.Connected).ConfigureAwait(false);
                        SetState(ConnectionTrigger.Connected);

                        await RunConnection();
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
                ServiceRef.Call(this, () =>
                {
                    if (State == ConnectionState.Connecting)
                        SetState(ConnectionTrigger.LinkError);
                });
            }
            finally
            {
            }
        }

        protected abstract Task OnConnectAsync(CancellationToken cancellationToken);

        //public ConnectionState State { get => _connectionStateMachine.State; }

        public async Task SendDataAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                var lenInBytes = BitConverter.GetBytes(data.Length);

                try
                {
                    if (_messageFramingEnabled)
                    {
                        await _sendingStream.WriteAsync(lenInBytes, 0, 4, cancellationToken).ConfigureAwait(false);
                    }

                    await _sendingStream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                    await _sendingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    //}
                }
                catch (Exception)
                {
                    SetState(ConnectionTrigger.LinkError);
                }
            }
        }

        private static readonly byte[] _nullLength = BitConverter.GetBytes(-1);

#if NETSTANDARD2_1
        public async Task SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                try
                {
                    
                    using var memoryBufferOwner = MemoryPool<byte>.Shared.Rent(4);

                    if (!BitConverter.TryWriteBytes(memoryBufferOwner.Memory.Span, data.Length))
                    {
                        throw new InvalidOperationException();
                    }

                    if (_messageFramingEnabled)
                    {
                        await _sendingStream.WriteAsync(memoryBufferOwner.Memory.Slice(0, 4), cancellationToken).AsTask().ConfigureAwait(false);
                    }

                    await _sendingStream.WriteAsync(data, cancellationToken).AsTask().ConfigureAwait(false);
                    await _sendingStream.FlushAsync().ConfigureAwait(false);
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
                    SetState(ConnectionTrigger.LinkError);
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

            if (State == ConnectionState.Connected && IsStreamConnected)
            {
                _sendKeepAliveResetEvent?.Set();

                try
                {
                    if (_messageFramingEnabled)
                    {
                        await _sendingStream.WriteAsync(_nullLength, 0, 4).ConfigureAwait(false);
                    }

                    await sendFunction(_sendingStream, cancellationToken).ConfigureAwait(false);
                    //await _sendingStream.FlushAsync().ConfigureAwait(false);
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
                    SetState(ConnectionTrigger.LinkError);
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
            SetState(ConnectionTrigger.Connect);
        }

        public virtual void Stop()
        {
            if (State != ConnectionState.Disconnected)
                SetState(ConnectionTrigger.Disconnect);
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
