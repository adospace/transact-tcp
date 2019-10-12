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
            : base(endPoint, receivedAction, null, connectionStateChangedAction, connectionSettings)
        {
            _localEndPoint = localEndPoint;
        }

        public ClientConnection(
            IPEndPoint endPoint,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync,
            IPEndPoint localEndPoint = null,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null)
            : base(endPoint, null, receivedActionAsync, connectionStateChangedAction, connectionSettings)
        {
            _localEndPoint = localEndPoint;
        }


        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _tcpClient = _localEndPoint == null ? new TcpClient() : new TcpClient(_localEndPoint);
            var cancellationCompletionSource = new TaskCompletionSource<bool>();

            while (true)
            {
                try
                { 
                    cancellationToken.ThrowIfCancellationRequested();

                    var connectTask = _tcpClient.ConnectAsync(_endPoint.Address, _endPoint.Port);

                    using (cancellationToken.Register(() => cancellationCompletionSource.TrySetResult(true)))
                    {
                        if (connectTask != await Task.WhenAny(connectTask, cancellationCompletionSource.Task))
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }
                    }

                    break;
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                { 
                
                }
            }
        }
    }
}
