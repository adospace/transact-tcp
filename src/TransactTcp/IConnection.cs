using System;
using System.Threading.Tasks;

namespace TransactTcp
{
    public interface IConnection : IDisposable
    {
        void Start();

        void Stop();

        ConnectionState State { get; }

        Task SendDataAsync(byte[] data);
    }
}
