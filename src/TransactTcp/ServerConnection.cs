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
            ConnectionSettings connectionSettings = null) 
            : base(endPoint, connectionSettings)
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
