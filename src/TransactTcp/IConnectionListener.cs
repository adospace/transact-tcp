using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TransactTcp
{
    public interface IConnectionListener : IDisposable
    {
        void Start(
            Action<IConnectionListener, IConnection> connectionCreated);

        void Stop();

        bool Listening { get; }
    }
}
