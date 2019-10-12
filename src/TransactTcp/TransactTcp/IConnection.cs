using System;
using System.Threading.Tasks;

namespace TransactTcp
{
    public interface IConnection
    {
        void Start();

        void Stop();

        ConnectionState State { get; }

        Task SendDataAsync(byte[] data);
    }
}
