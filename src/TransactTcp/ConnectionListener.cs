using ServiceActor;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public class ConnectionListener : IConnectionListener
    {
        private readonly IPEndPoint _localEndPoint;
        private readonly ConnectionListenerSettings _settings;
        private CancellationTokenSource _listeningLoopCancellationTokenSource;
        private Task _listeningTask;
        private Action<IConnectionListener, IConnection> _connectionCreatedAction;

        internal ConnectionListener(IPEndPoint localEndPoint, ConnectionListenerSettings settings = null)
        {
            _localEndPoint = localEndPoint ?? throw new ArgumentNullException(nameof(localEndPoint));
            _settings = settings ?? new ConnectionListenerSettings();
        }

        public bool Listening { get => _listeningTask != null; }

        public void Start(Action<IConnectionListener, IConnection> connectionCreatedAction)
        {
            _connectionCreatedAction = connectionCreatedAction ?? throw new ArgumentNullException(nameof(connectionCreatedAction));

            if (Listening)
            {
                throw new InvalidOperationException();
            }

            _listeningTask = Task.Run(ListeningLoopCore);
        }

        private async Task ListeningLoopCore()
        {
            var tcpListener = new TcpListener(_localEndPoint);
            try
            {

                tcpListener.Start(_settings.BackLog);
                _listeningLoopCancellationTokenSource = new CancellationTokenSource();
                {
                    while (true)
                    {
                        using (_listeningLoopCancellationTokenSource.Token.Register(() => tcpListener.Stop()))
                        {
                            try
                            {
                                var tcpToClient = await tcpListener.AcceptTcpClientAsync();

                                _connectionCreatedAction.Invoke(this,
                                    ServiceRef.Create<IConnection>(new ServerPeerConnection(tcpToClient, _settings.NewConnectionSettings)));
                            }
                            catch (InvalidOperationException)
                            {
                                _listeningLoopCancellationTokenSource.Token.ThrowIfCancellationRequested();
                                throw;
                            }
                            finally
                            {
                            }
                        }

                        _listeningLoopCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                //_listeningLoopCancellationTokenSource?.Cancel(); //-> call to Register(() => tcpListener.Stop()))
                //_listeningLoopCancellationTokenSource = null;

                //TODO: handle excpetions like unable to bind to a port etc...
            }
            finally
            {
                tcpListener.Stop();
                //_listeningLoopCancellationTokenSource = null;
            }
        }

        public void Stop()
        {
            _listeningTask?.Wait();
            _listeningLoopCancellationTokenSource?.Cancel();
            _listeningLoopCancellationTokenSource = null;
            _listeningTask = null;
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
                    _listeningLoopCancellationTokenSource?.Dispose();
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
