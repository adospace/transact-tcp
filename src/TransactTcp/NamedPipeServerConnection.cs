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
            : base(connectionEndPoint?.ConnectionSettings ?? new ConnectionSettings(keepAliveMilliseconds: 0 /*by default named pipe does't require keep alive messages*/))
        {
            _localEndPointName = connectionEndPoint.LocalEndPointName ?? throw new ArgumentNullException("connectionEndPoint.LocalEndPointName");
        }

        protected override bool IsStreamConnected => (_pipeServer?.IsConnected).GetValueOrDefault();

        protected override async Task OnConnectAsync(CancellationTokenSource cancellationTokenSource)
        {
            var cancellationToken = cancellationTokenSource.Token;

            _pipeServer =
                new NamedPipeServerStream(_localEndPointName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            await _pipeServer.WaitForConnectionAsync(cancellationToken);

            _connectedStream = _pipeServer;
        }
    }

}
