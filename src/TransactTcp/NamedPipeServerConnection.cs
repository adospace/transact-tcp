using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    internal class NamedPipeServerConnection : Connection
    {
        private NamedPipeServerStream _pipeServer;
        private readonly string _localEndPointName;

        public NamedPipeServerConnection(
           NamedPipeConnectionEndPoint connectionEndPoint)
            : base(connectionEndPoint?.ConnectionSettings)
        {
            _localEndPointName = connectionEndPoint.LocalEndPointName ?? throw new ArgumentNullException("connectionEndPoint.LocalEndPointName");
        }

        protected override bool IsStreamConnected => (_pipeServer?.IsConnected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationToken cancellationToken)
        {
            _pipeServer =
                new NamedPipeServerStream(_localEndPointName, PipeDirection.InOut, 1);

            try
            {
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
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

            }

            _connectedStream = _pipeServer;
        }
    }

}
