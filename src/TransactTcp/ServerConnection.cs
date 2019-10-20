using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class ServerConnection : Connection
    {
        public ServerConnection(
            IPEndPoint endPoint, 
            Action<IConnection, byte[]> receivedAction, 
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null) 
            : base(endPoint, receivedAction, null, null, connectionStateChangedAction, connectionSettings)
        {
        }

        public ServerConnection(
            IPEndPoint endPoint,
            Func<IConnection, byte[], CancellationToken, Task> receivedActionAsync,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null)
            : base(endPoint, null, receivedActionAsync, null, connectionStateChangedAction, connectionSettings)
        {
        }

        public ServerConnection(
            IPEndPoint endPoint,
            Func<IConnection, NetworkBufferedReadStream, CancellationToken, Task> receivedActionStreamAsync,
            Action<IConnection, ConnectionState, ConnectionState> connectionStateChangedAction = null,
            ConnectionSettings connectionSettings = null)
            : base(endPoint, null, null, receivedActionStreamAsync, connectionStateChangedAction, connectionSettings)
        {
        }
        
        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            var tcpListener = new TcpListener(_endPoint);
            
            tcpListener.Start(1);

            using (cancellationToken.Register(() => tcpListener.Stop()))
            {
                try
                {
                    _tcpClient = await tcpListener.AcceptTcpClientAsync();
                }
                catch (InvalidOperationException)
                {
                    // Either tcpListener.Start wasn't called (a bug!)
                    // or the CancellationToken was cancelled before
                    // we started accepting (giving an InvalidOperationException),
                    // or the CancellationToken was cancelled after
                    // we started accepting (giving an ObjectDisposedException).
                    //
                    // In the latter two cases we should surface the cancellation
                    // exception, or otherwise rethrow the original exception.
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                finally
                {
                    tcpListener.Stop();
                }
            }
        }

    }
}
