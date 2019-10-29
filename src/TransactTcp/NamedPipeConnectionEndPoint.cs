using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    public class NamedPipeConnectionEndPoint
    {
        public NamedPipeConnectionEndPoint(string localNamedPipeName = null, string remoteEndPointName = null, string remoteEndPointHost = ".", ConnectionSettings connectionSettings = null)
        {
            LocalEndPointName = localNamedPipeName;
            RemoteEndPointName = remoteEndPointName;
            RemoteEndPointHost = remoteEndPointHost;
            ConnectionSettings = connectionSettings;
        }

        public string LocalEndPointName { get; }
        public string RemoteEndPointName { get; }
        public string RemoteEndPointHost { get; }
        public ConnectionSettings ConnectionSettings { get; }
    }
}
