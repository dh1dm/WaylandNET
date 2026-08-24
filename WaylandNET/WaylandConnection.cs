using System;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace WaylandNET
{
    public abstract class WaylandConnection : IDisposable
    {
        public WaylandConnection(Socket socket, WaylandObjectMap objectMap)
        {
            this.socket = socket;
            this.objectMap = objectMap;
        }

        public void Dispose() => socket.Dispose();

        public WaylandObject this[uint id]
        {
            get => objectMap[id];
            set => objectMap[id] = value;
        }

        public uint AllocateId() => objectMap.AllocateId();
        public void DeallocateId(uint id) => objectMap.DeallocateId(id);

        public void SendRequest(uint id, ushort opcode, params object[] arguments)
        {
            ushort size = 8;
            foreach (object argument in arguments)
            {
                switch (argument)
                {
                    case int i:
                    case uint u:
                    case double d:
                        size += 4;
                        break;
                    case string s:
                        size += 4;
                        if (s != null)
                            size += (ushort)((Encoding.UTF8.GetByteCount(s) + 4) & ~3);
                        break;
                    case byte[] a:
                        size += 4;
                        if (a != null)
                            size += (ushort)((a.Length + 3) & ~3);
                        break;
                    case SafeHandle h:
                        break;
                }
            }
            sendQueue.Write(id);
            sendQueue.Write(opcode | (size << 16));
            foreach (object argument in arguments)
            {
                switch (argument)
                {
                    case int i:
                        sendQueue.Write(i);
                        break;
                    case uint u:
                        sendQueue.Write(u);
                        break;
                    case double d:
                        sendQueue.Write((int)(d * 256.0));
                        break;
                    case string s:
                        sendQueue.Write(s);
                        break;
                    case byte[] a:
                        sendQueue.Write(a);
                        break;
                    case SafeHandle h:
                        sendQueue.Write(h);
                        break;
                }
            }
        }

        public void MessageLoop()
        {
            SafeHandle sockfd = socket.SafeHandle;
            SysNative.Result result;
            SysNative.PollFd pfd;
            quit = false;
            while (true)
            {
                int timeout = quit ? 0 : GetIdleTimeout();
                pfd.events = sendQueue.Flush(sockfd) ? PollEvents.POLLIN :
                    PollEvents.POLLIN | PollEvents.POLLOUT;
                pfd.revents = 0;
                bool success = false;
                sockfd.DangerousAddRef(ref success); // throws if disposed
                pfd.fd = (int)sockfd.DangerousGetHandle();
                try
                {
                    result = SysNative.Poll(ref pfd, timeout);
                }
                finally
                {
                    sockfd.DangerousRelease();
                }
                if (result.retval < 0)
                {
                    throw new IOException(Marshal.GetPInvokeErrorMessage(result.errno));
                }
                if (result.retval == 0) // timeout
                {
                    if (quit)
                    {
                        break;
                    }
                    continue;
                }
                if ((pfd.revents & (PollEvents.POLLERR | PollEvents.POLLHUP | PollEvents.POLLNVAL)) != 0)
                {
                    throw new SocketException((int)SocketError.NotConnected);
                }
                if ((pfd.revents & PollEvents.POLLIN) != 0)
                {
                    recvQueue.Receive(sockfd);
                    ParseReceived();
                }
            }
        }

        public void Quit() => quit = true;

        protected virtual int GetIdleTimeout() => -1;

        private void ParseReceived()
        {
            while (true)
            {
                if (recvObject == null)
                {
                    if (recvQueue.Count < 8)
                    {
                        break;
                    }
                    recvObject = objectMap[recvQueue.ReadUInt32()];
                    uint header = recvQueue.ReadUInt32();
                    recvOpcode = (ushort)header;
                    recvLength = (ushort)(header >> 16);
                }
                if (recvQueue.Count < (recvLength - 8))
                {
                    break;
                }
                WaylandType[] argumentTypes = recvObject.Arguments(recvOpcode);
                object[] arguments = new object[argumentTypes.Length];
                for (int i = 0; i < argumentTypes.Length; i++)
                {
                    switch (argumentTypes[i])
                    {
                        case WaylandType.Int:
                            arguments[i] = recvQueue.ReadInt32();
                            break;
                        case WaylandType.UInt:
                            arguments[i] = recvQueue.ReadUInt32();
                            break;
                        case WaylandType.Fixed:
                            arguments[i] = recvQueue.ReadInt32() / 256.0d;
                            break;
                        case WaylandType.Object:
                            arguments[i] = objectMap[recvQueue.ReadUInt32()];
                            break;
                        case WaylandType.NewId:
                            arguments[i] = recvQueue.ReadUInt32();
                            break;
                        case WaylandType.String:
                            arguments[i] = recvQueue.ReadString();
                            break;
                        case WaylandType.Array:
                            arguments[i] = recvQueue.ReadBytes();
                            break;
                        case WaylandType.Handle:
                            arguments[i] = recvQueue.ReadHandle();
                            break;
                    }
                }
                if (recvObject.IsAlive)
                {
                    recvObject.Handle(recvOpcode, arguments);
                }
                recvObject = null;
            }
        }

        private readonly Socket socket;
        private readonly WaylandObjectMap objectMap;
        private readonly SendQueue sendQueue = new SendQueue();
        private readonly RecvQueue recvQueue = new RecvQueue();

        private bool quit;
        private ushort recvLength;
        private ushort recvOpcode;
        private WaylandObject recvObject;
    }
}
