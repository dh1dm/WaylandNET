using System;
using System.Runtime.InteropServices;

namespace WaylandNET
{
    internal static class SysNative
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct PollFd
        {
            public int fd;
            public PollEvents events;
            public PollEvents revents;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct IoVector
        {
            public void*  iov_base;
            public IntPtr iov_len;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CMsgHeader
        {
            public IntPtr cmsg_len;
            public int    cmsg_level;
            public int    cmsg_type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MsgHeader
        {
            public void*     msg_name;
            public uint      msg_namelen;
            public IoVector* msg_iov;
            public IntPtr    msg_iovlen;
            public void*     msg_control;
            public IntPtr    msg_controllen;
            public int       msg_flags;
        }

        public struct Result
        {
            public Result(int retval, int errno)
            {
                this.retval = retval;
                this.errno = errno;
            }
            public readonly int retval;
            public readonly int errno;
        }

        public static Result Poll(ref PollFd pfd, int timeout)
        {
            int retval = poll(ref pfd, 1, timeout);
            if (retval >= 0)
            {
                return new Result(retval, 0);
            }
            return new Result(-1, Marshal.GetLastPInvokeError());
        }

        public static Result SendMsg(SafeHandle sockfd, ref MsgHeader msg)
        {
            int retval = sendmsg(sockfd, ref msg, MSG_DONTWAIT | MSG_NOSIGNAL);
            if (retval >= 0)
            {
                return new Result(retval, 0);
            }
            return new Result(-1, Marshal.GetLastPInvokeError());
        }

        public static Result RecvMsg(SafeHandle sockfd, ref MsgHeader msg)
        {
            int retval = recvmsg(sockfd, ref msg, MSG_DONTWAIT | MSG_CMSG_CLOEXEC);
            if (retval >= 0)
            {
                return new Result(retval, 0);
            }
            return new Result(-1, Marshal.GetLastPInvokeError());
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int poll(ref PollFd fds, int nfds, int timeout);

        [DllImport("libc", SetLastError = true)]
        private static extern int sendmsg(SafeHandle sockfd, ref MsgHeader msg, uint flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int recvmsg(SafeHandle sockfd, ref MsgHeader msg, uint flags);

        private const int MSG_DONTWAIT = 0x40;
        private const int MSG_NOSIGNAL = 0x4000;
        private const int MSG_CMSG_CLOEXEC = 0x40000000;
    }
}
