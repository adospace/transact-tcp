﻿using System;
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

            await _pipeServer.WaitForConnectionAsync(cancellationToken);

            _connectedStream = _pipeServer;
        }
    }

}
