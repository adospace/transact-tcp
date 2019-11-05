using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class TcpServerConnection : Connection
    {
        protected TcpClient _tcpToClient;
        private readonly IPEndPoint _localEndPoint;

        public TcpServerConnection(
           TcpConnectionEndPoint connectionEndPoint) 
            : base(connectionEndPoint?.ConnectionSettings)
        {
            _localEndPoint = connectionEndPoint.LocalEndPoint ?? throw new ArgumentNullException("connectionEndPoint.LocalEndPoint");
        }

        protected override bool IsStreamConnected => (_tcpToClient?.Connected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationTokenSource cancellationTokenSource)
        {
            var cancellationToken = cancellationTokenSource.Token;
            var tcpListener = new TcpListener(_localEndPoint);

            tcpListener.Start(1);

            using (cancellationToken.Register(() => tcpListener.Stop()))
            {
                try
                {
                    _tcpToClient = await tcpListener.AcceptTcpClientAsync();
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                finally
                {
                    tcpListener.Stop();
                }
            }

            _connectedStream = await CreateConnectedStreamAsync(_tcpToClient, cancellationToken);
        }

        protected virtual Task<Stream> CreateConnectedStreamAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            return Task.FromResult<Stream>(tcpClient.GetStream());
        }

        protected override void OnDisconnect()
        {
            base.OnDisconnect();

            _tcpToClient?.Close();
            _tcpToClient = null;
        }

    }
}
