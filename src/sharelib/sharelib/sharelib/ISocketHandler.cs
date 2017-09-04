using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace sharelib
{
    public interface ISocketHandler
    {
        Task<bool> ProcessData(SocketConnection conn);
    }
}
