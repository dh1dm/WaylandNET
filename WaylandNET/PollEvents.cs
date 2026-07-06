using System;

namespace WaylandNET
{
    [Flags]
    internal enum PollEvents : ushort
    {
        POLLIN = 0x0001,
        POLLPRI = 0x0002,
        POLLOUT = 0x0004,
        POLLERR = 0x0008,
        POLLHUP = 0x0010,
        POLLNVAL = 0x0020
    }
}
