using System;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace WaylandNET
{
    public sealed class SendQueue
    {
        private const int BUF_SIZE = 4096;
        private const int FDS_SIZE = 28;
        private const int CMSG_SIZE = 16 + FDS_SIZE * 4;

        private readonly byte[] buf = new byte[BUF_SIZE];
        private readonly SafeHandle[] fds = new SafeHandle[FDS_SIZE];

        private int buf_head;
        private int buf_tail;
        private int buf_cnt;
        private int fds_cnt;

        public void Write(int val) => Write((uint)val);

        public void Write(uint val)
        {
            if ((buf_cnt + 4) > BUF_SIZE)
            {
                throw new InternalBufferOverflowException();
            }
            buf_cnt += 4;
            int idx = buf_head;
            Span<byte> buf = this.buf;
            buf[idx] = (byte)val;
            idx = (idx + 1) & (BUF_SIZE - 1);
            buf[idx] = (byte)(val >> 8);
            idx = (idx + 1) & (BUF_SIZE - 1);
            buf[idx] = (byte)(val >> 16);
            idx = (idx + 1) & (BUF_SIZE - 1);
            buf[idx] = (byte)(val >> 24);
            idx = (idx + 1) & (BUF_SIZE - 1);
            buf_head = idx;
        }

        public void Write(string str)
        {
            if (str == null)
            {
                Write((uint)0);
                return;
            }

            ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(str);
            int len = bytes.Length + 1;
            Write(len);
            len = (len + 3) & ~3;
            if ((buf_cnt + len) > BUF_SIZE)
            {
                throw new InternalBufferOverflowException();
            }
            buf_cnt += len;

            int idx = buf_head;
            Span<byte> buf = this.buf;

            int first = Math.Min(BUF_SIZE - idx, bytes.Length);
            int second = bytes.Length - first;
            bytes.Slice(0, first).CopyTo(buf.Slice(idx, first));
            bytes.Slice(first, second).CopyTo(buf.Slice(0, second));

            len -= bytes.Length;
            idx = (idx + bytes.Length) & (BUF_SIZE - 1);
            while (len-- > 0)
            {
                buf[idx] = 0;
                idx = (idx + 1) & (BUF_SIZE - 1);
            }
            buf_head = idx;
        }

        public void Write(ReadOnlySpan<byte> bytes)
        {
            int len = bytes.Length;
            Write(len);
            if (len == 0) return;

            len = (len + 3) & ~3;
            if ((buf_cnt + len) > BUF_SIZE)
            {
                throw new InternalBufferOverflowException();
            }
            buf_cnt += len;

            int idx = buf_head;
            Span<byte> buf = this.buf;

            int first = Math.Min(BUF_SIZE - idx, bytes.Length);
            int second = bytes.Length - first;
            bytes.Slice(0, first).CopyTo(buf.Slice(idx, first));
            bytes.Slice(first, second).CopyTo(buf.Slice(0, second));

            len -= bytes.Length;
            idx = (idx + bytes.Length) & (BUF_SIZE - 1);
            while (len-- > 0)
            {
                buf[idx] = 0;
                idx = (idx + 1) & (BUF_SIZE - 1);
            }
            buf_head = idx;
        }

        public void Write(SafeHandle fd)
        {
            if (fds_cnt >= FDS_SIZE)
            {
                throw new InternalBufferOverflowException();
            }
            fds[fds_cnt++] = fd;
        }

        public unsafe bool Flush(SafeHandle sockfd)
        {
            if ((buf_cnt == 0) && (fds_cnt == 0))
            {
                return true;
            }

            byte* cmsg = stackalloc byte[CMSG_SIZE];
            int cmsg_len = 0;

            if (fds_cnt > 0)
            {
                cmsg_len = sizeof(SysNative.CMsgHeader) + fds_cnt * 4;
                SysNative.CMsgHeader* header = (SysNative.CMsgHeader*)cmsg;
                header->cmsg_len   = cmsg_len;
                header->cmsg_level = 1;  // SOL_SOCKET
                header->cmsg_type  = 1;  // SCM_RIGHTS
            }

            Span<SafeHandle> fds = new Span<SafeHandle>(this.fds, 0, fds_cnt);
            int* cmsg_fds = (int*)(cmsg + sizeof(SysNative.CMsgHeader));

            SysNative.IoVector* iov = stackalloc SysNative.IoVector[2];
            int iov_len = 0;

            int first = Math.Min(BUF_SIZE - buf_tail, buf_cnt);
            int second = buf_cnt - first;

            fixed (byte* buf = this.buf)
            {
                if (first > 0)
                {
                    iov[0].iov_base = buf + buf_tail;
                    iov[0].iov_len = first;
                    iov_len = 1;
                    if (second > 0)
                    {
                        iov[1].iov_base = buf;
                        iov[1].iov_len = second;
                        iov_len = 2;
                    }
                }

                SysNative.MsgHeader msg = new SysNative.MsgHeader
                {
                    msg_iov        = iov,
                    msg_iovlen     = iov_len,
                    msg_control    = cmsg,
                    msg_controllen = cmsg_len
                };

                int fds_taken = 0;
                SysNative.Result result;
                try
                {
                    foreach (SafeHandle fd in fds)
                    {
                        bool success = false;
                        fd.DangerousAddRef(ref success); // throws if disposed
                        cmsg_fds[fds_taken++] = (int)fd.DangerousGetHandle();
                    }
                    result = SysNative.SendMsg(sockfd, ref msg);
                }
                finally
                {
                    foreach (SafeHandle fd in fds.Slice(0, fds_taken))
                    {
                        fd.DangerousRelease();
                    }
                }
                if (result.retval < 0)
                {
                    if (result.errno == 11) // EAGAIN
                    {
                        return false;
                    }
                    throw new IOException(Marshal.GetPInvokeErrorMessage(result.errno));
                }
                fds.Clear();
                fds_cnt = 0;
                buf_tail = (buf_tail + result.retval) & (BUF_SIZE - 1);
                buf_cnt -= result.retval;
            }

            return (buf_cnt == 0);
        }
    }
}
