using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class ClientConnection : Connection
    {
        private readonly IPEndPoint _localEndPoint;

        public ClientConnection(
            IPEndPoint endPoint,
            Action<IConnection, byte[]> receivedAction, 
            IPEndPoint localEndPoint = null, 
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null) 
            : base(endPoint, receivedAction, null, null, connectionStateChangedAction, connectionSettings)
        {
            _localEndPoint = localEndPoint;
        }

        public ClientConnection(
            IPEndPoint endPoint,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync,
            IPEndPoint localEndPoint = null,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null)
            : base(endPoint, null, receivedActionAsync, null, connectionStateChangedAction, connectionSettings)
        {
            _localEndPoint = localEndPoint;
        }

        public ClientConnection(
            IPEndPoint endPoint,
            Func<IConnection, NetworkBufferedReadStream, CancellationToken, Task> receivedActionStreamAsync,
            IPEndPoint localEndPoint = null,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null)
            : base(endPoint, null, null, receivedActionStreamAsync, connectionStateChangedAction, connectionSettings)
        {
            _localEndPoint = localEndPoint;
        }

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _tcpClient = _localEndPoint == null ? new TcpClient() : new TcpClient(_localEndPoint);
            var cancellationCompletionSource = new TaskCompletionSource<bool>();

            using (var reconnectionDelayEvent = new AutoResetEvent(false))
            {
                while (true)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var connectTask = _tcpClient.ConnectAsync(_endPoint.Address, _endPoint.Port);

                        using (cancellationToken.Register(() =>
                        {
                            reconnectionDelayEvent.Set();
                            cancellationCompletionSource.TrySetResult(true);
                        }))
                        {
                            if (connectTask != await Task.WhenAny(connectTask, cancellationCompletionSource.Task))
                            {
                                throw new OperationCanceledException(cancellationToken);
                            }
                        }

                        if (!connectTask.IsFaulted)
                            break;

                        if (reconnectionDelayEvent.WaitOne(_connectionSettings.ReconnectionDelay))
                            break;
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
        }
    }
}
