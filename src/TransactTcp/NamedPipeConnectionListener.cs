﻿using ServiceActor;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public class NamedPipeConnectionListener : IConnectionListener
    {
        private readonly string _localEndPointName;
        private readonly ConnectionListenerSettings _settings;
        private CancellationTokenSource _listeningLoopCancellationTokenSource;
        private Task _listeningTask;
        private Action<IConnectionListener, IConnection> _connectionCreatedAction;

        internal NamedPipeConnectionListener(
           string localEndPointName, ConnectionListenerSettings settings = null)
        {
            _localEndPointName = localEndPointName ?? throw new ArgumentNullException("localEndPointName");
            _settings = settings ?? new ConnectionListenerSettings();
            
            if ((_settings.NewConnectionSettings?.UseBufferedStream).GetValueOrDefault())
                throw new NotSupportedException();

            if (string.IsNullOrWhiteSpace(localEndPointName))
            {
                throw new ArgumentException("Invalid pipename", nameof(localEndPointName));
            }
        }

        public bool Listening { get => _listeningTask != null; }

        public void Start(Action<IConnectionListener, IConnection> connectionCreatedAction)
        {
            _connectionCreatedAction = connectionCreatedAction ?? throw new ArgumentNullException(nameof(connectionCreatedAction));

            if (Listening)
            {
                throw new InvalidOperationException();
            }

            _listeningTask = Task.Run(() => ListeningLoopCore((_listeningLoopCancellationTokenSource = new CancellationTokenSource()).Token));
        }

        private async Task ListeningLoopCore(CancellationToken cancellationToken)
        {
            NamedPipeServerStream pipeServerIn = null;
            NamedPipeServerStream pipeServerOut = null;

            try
            {
                while (true)
                {
                    try
                    {
                        pipeServerIn =
                            new NamedPipeServerStream(_localEndPointName + "_IN_TO_SERVER", 
                            PipeDirection.In, 
                            NamedPipeServerStream.MaxAllowedServerInstances, 
                            PipeTransmissionMode.Byte, 
                            PipeOptions.Asynchronous);

                        pipeServerOut =
                            new NamedPipeServerStream(_localEndPointName + "_OUT_FROM_SERVER", 
                            PipeDirection.Out, 
                            NamedPipeServerStream.MaxAllowedServerInstances, 
                            PipeTransmissionMode.Byte, 
                            PipeOptions.Asynchronous);

                        await pipeServerIn.WaitForConnectionAsync(cancellationToken);
                        await pipeServerOut.WaitForConnectionAsync(cancellationToken);

                        _connectionCreatedAction.Invoke(this,
                            ServiceRef.Create<IConnection>(new NamedPipeServerPeerConnection(
                                pipeServerIn, pipeServerOut, _settings.NewConnectionSettings)));
                    }
#if DEBUG
                    catch (InvalidOperationException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"{GetType()}{Environment.NewLine}{ex}");
#else
                    catch (InvalidOperationException)
                    {
#endif
                        cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    }
                    finally
                    {
                    }

                    cancellationToken.ThrowIfCancellationRequested();
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
                pipeServerIn?.Close();
                pipeServerOut?.Close();
                //_listeningLoopCancellationTokenSource = null;
            }
        }

        public void Stop()
        {
            _listeningLoopCancellationTokenSource?.Cancel();
            //_listeningTask?.Wait();
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
