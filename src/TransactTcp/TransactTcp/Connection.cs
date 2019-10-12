using ServiceActor;
using Stateless;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal abstract class Connection : IConnection
    {
        protected IPEndPoint _endPoint;
        protected Action<IConnection, byte[]> _receivedAction;
        private readonly Func<IConnection, byte[], CancellationToken, Task> _receivedActionAsync;
        private readonly ConnectionSettings _connectionSettings;
        private CancellationTokenSource _connectCancellationTokenSource;
        private CancellationTokenSource _receiveLoopCancellationTokenSource;
        private CancellationTokenSource _sendKeepAliveLoopCancellationTokenSource;
        protected readonly StateMachine<ConnectionState, ConnectionTrigger> _connectionStateMachine;
        protected TcpClient _tcpClient;
        private AutoResetEvent _sendKeepAliveResetEvent;
        private Task _receiveLoopTask;
        private Task _sendKeepAliveLoopTask;

        private static readonly byte[] _keepAlivePacket = new byte[] { 0x0, 0x0, 0x0, 0x0 };

        protected Connection(
            IPEndPoint endPoint,
            Action<IConnection, byte[]> receivedAction,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null)
        {
            _endPoint = endPoint;
            _receivedAction = receivedAction ?? throw new ArgumentNullException(nameof(receivedAction));
            _receivedActionAsync = receivedActionAsync;
            _connectionSettings = connectionSettings ?? ConnectionSettings.Default;
            _connectionStateMachine = new StateMachine<ConnectionState, ConnectionTrigger>(ConnectionState.Disconnected);

            _connectionStateMachine.OnTransitioned((transition) => 
                connectionStateChangedAction?.Invoke(this, transition.Source, transition.Destination));

            _connectionStateMachine.Configure(ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.Connect, ConnectionState.Connecting)
                .OnEntryFrom(ConnectionTrigger.Disconnect, OnDisconnect);

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
                    _receiveLoopTask = Task.Run(() => ReceiveLoopAsync());
                    _sendKeepAliveLoopTask = Task.Run(() => SendKeepAliveLoopAsync());
                })
                .OnExit(() =>
                {
                    _receiveLoopCancellationTokenSource?.Cancel();
                    _sendKeepAliveLoopCancellationTokenSource?.Cancel();
                    _sendKeepAliveResetEvent?.Set();
                    Task.WaitAll(_receiveLoopTask, _sendKeepAliveLoopTask);
                });

            _connectionStateMachine.Configure(ConnectionState.LinkError)
                .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
                .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
                .OnEntry(() =>
                {
                    OnDisconnect();
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
                    while (true)
                    {
                        if (!_sendKeepAliveResetEvent.WaitOne(_connectionSettings.KeepAliveMilliseconds))
                        {
                            _sendKeepAliveLoopCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            ServiceRef.Call(this, async () =>
                            {
                                if (_connectionStateMachine.State == ConnectionState.Connected)
                                {
                                    await _tcpClient.GetStream().WriteAsync(_keepAlivePacket, 0, _keepAlivePacket.Length, _sendKeepAliveLoopCancellationTokenSource.Token);
                                }
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
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

        private async void ReceiveLoopAsync()
        {
            try
            {
                using (_receiveLoopCancellationTokenSource = new CancellationTokenSource())
                {
                    var messageSizeBuffer = new byte[4];
                    var cancellationToken = _receiveLoopCancellationTokenSource.Token;
                    while (true)
                    {
                        if (await _tcpClient.GetStream().ReadAsync(messageSizeBuffer, 0, 4, cancellationToken) < 4)
                            throw new TimeoutException();

                        cancellationToken.ThrowIfCancellationRequested();

                        var messageLength = BitConverter.ToInt32(messageSizeBuffer, 0);
                        if (messageLength == 0)
                            continue;

                        var messageBuffer = new byte[messageLength];
                        if (await _tcpClient.GetStream().ReadAsync(messageBuffer, 0, messageLength, cancellationToken) < messageLength)
                            throw new TimeoutException();

                        cancellationToken.ThrowIfCancellationRequested();

                        if (_receivedActionAsync != null)
                            await _receivedActionAsync(ServiceRef.Create<IConnection>(this), messageBuffer, cancellationToken);
                        else
                            _receivedAction(ServiceRef.Create<IConnection>(this), messageBuffer);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
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

        private void OnDisconnect()
        {
            System.Diagnostics.Debug.Assert(_receiveLoopCancellationTokenSource == null);
            _tcpClient?.Close();
            _tcpClient = null;
        }

        private async void OnConnectAsyncCore()
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
                        _tcpClient.ReceiveTimeout = _connectionSettings.KeepAliveMilliseconds * 2;
                        _connectionStateMachine.Fire(ConnectionTrigger.Connected);
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
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

        public  async Task SendDataAsync(byte[] data)
        {
            _sendKeepAliveResetEvent.Set();
            await _tcpClient.GetStream().WriteAsync(data, 0, data.Length);
        }

        public virtual void Start()
        {
            _connectionStateMachine.Fire(ConnectionTrigger.Connect);
        }

        public virtual void Stop()
        {
            _connectionStateMachine.Fire(ConnectionTrigger.Disconnect);
        }
    }
}
