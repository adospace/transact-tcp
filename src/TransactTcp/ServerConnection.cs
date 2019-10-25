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
    internal class ServerConnection : Connection
    {
        protected TcpClient _tcpToClient;
        private IPEndPoint _localEndPoint;

        public ServerConnection(
           ConnectionEndPoint connectionEndPoint) 
            : base(connectionEndPoint?.ConnectionSettings)
        {
            _localEndPoint = connectionEndPoint.LocalEndPoint ?? throw new ArgumentNullException("connectionEndPoint.LocalEndPoint");
        }

        //protected override void ConfigureStateMachine()
        //{
        //    base.ConfigureStateMachine();

        //    _connectionStateMachine.Configure(ConnectionState.Disconnected)
        //        .Permit(ConnectionTrigger.Connect, ConnectionState.Connecting)
        //        ;

        //    _connectionStateMachine.Configure(ConnectionState.LinkError)
        //        .Permit(ConnectionTrigger.Disconnect, ConnectionState.Disconnected)
        //        .Permit(ConnectionTrigger.Connected, ConnectionState.Connected)
        //        .OnEntryFrom(ConnectionTrigger.LinkError, () =>
        //        {
        //            OnDisconnect();
        //            Task.Run(OnConnectAsyncCore);
        //        });
        //}

        protected override bool IsStreamConnected => (_tcpToClient?.Connected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
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
                finally
                {
                    tcpListener.Stop();
                }
            }

            _tcpToClient.ReceiveTimeout = _connectionSettings.KeepAliveMilliseconds * 2;
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
