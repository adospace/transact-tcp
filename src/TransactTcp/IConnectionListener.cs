using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ServiceActor;

namespace TransactTcp
{
    public interface IConnectionListener : IDisposable
    {
        void Start(
            Action<IConnectionListener, IConnection> connectionCreated);

        [BlockCaller]
        void Stop();

        bool Listening { get; }
    }
}
