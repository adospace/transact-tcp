using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransactTcp
{
    public static class AsyncAutoResetEventExtensions
    {
        public static Task<bool> WaitAsync(this AsyncAutoResetEvent autoResetEvent, int timeout, CancellationToken cancellationToken = default) => Task.FromResult(
                0 == (Task.WaitAny(autoResetEvent.WaitAsync(cancellationToken), Task.Delay(timeout, cancellationToken))));
    }
}
