using System;
using System.Collections.Generic;
using System.Text;

namespace TransactTcp
{
    internal enum ConnectionTrigger
    {
        Connect,

        Disconnect,
        Connected,
        LinkError
    }
}
