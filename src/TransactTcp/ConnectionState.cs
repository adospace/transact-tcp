using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    public enum ConnectionState
    {
        Disconnected,

        Connecting,

        Connected,
                
        LinkError
    }
}
