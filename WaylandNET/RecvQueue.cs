using System;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WaylandNET
{
    public sealed class RecvQueue
    {
        private const int BUF_SIZE = 4096;
        private const int FDS_SIZE = 128;
        private const int CMSG_SIZE = 128;

        private readonly byte[] buf = new byte[BUF_SIZE];
        private readonly SafeFileHandle[] fds = new SafeFileHandle[FDS_SIZE];

        private int buf_head;
        private int buf_tail;
        private int buf_cnt;
        private int fds_head;
        private int fds_tail;
        private int fds_cnt;

        public int Count => buf_cnt;

        public uint ReadUInt32() => (uint)ReadInt32();

        public int ReadInt32()
        {
            if (buf_cnt < 4)
            {
                throw new InvalidDataException();
            }
            buf_cnt -= 4;
            int idx = buf_tail;
            ReadOnlySpan<byte> buf = this.buf;
            int result = buf[idx];
            idx = (idx + 1) & (BUF_SIZE - 1);
            result |= buf[idx] << 8;
            idx = (idx + 1) & (BUF_SIZE - 1);
            result |= buf[idx] << 16;
            idx = (idx + 1) & (BUF_SIZE - 1);
            result |= buf[idx] << 24;
            idx = (idx + 1) & (BUF_SIZE - 1);
            buf_tail = idx;
            return result;
        }

        public string ReadString()
        {
            int len = ReadInt32();
            if (len == 0) return null;

            Span<byte> bytes = new byte[len - 1];
            len = (len + 3) & ~3;

            if (buf_cnt < len)
            {
                throw new InvalidDataException();
            }
            buf_cnt -= len;

            int idx = buf_tail;
            ReadOnlySpan<byte> buf = this.buf;
            
            int first = Math.Min(BUF_SIZE - idx, bytes.Length);
            int second = bytes.Length - first;
            buf.Slice(idx, first).CopyTo(bytes.Slice(0, first));
            buf.Slice(0, second).CopyTo(bytes.Slice(first, second));

            buf_tail = (idx + len) & (BUF_SIZE - 1);

            return Encoding.UTF8.GetString(bytes);
        }

        public byte[] ReadBytes()
        {
            int len = ReadInt32();
            if (len == 0) return null;

            byte[] bytes = new byte[len];
            len = (len + 3) & ~3;

            if (buf_cnt < len)
            {
                throw new InvalidDataException();
            }
            buf_cnt -= len;

            int idx = buf_tail;
            ReadOnlySpan<byte> buf = this.buf;

            int first = Math.Min(BUF_SIZE - idx, bytes.Length);
            int second = bytes.Length - first;
            buf.Slice(idx, first).CopyTo(bytes.AsSpan(0, first));
            buf.Slice(0, second).CopyTo(bytes.AsSpan(first, second));

            buf_tail = (idx + len) & (BUF_SIZE - 1);

            return bytes;
        }

        public SafeFileHandle ReadHandle()
        {
            if (fds_cnt == 0)
            {
                throw new InvalidDataException();
            }
            fds_cnt--;
            int idx = fds_tail;
            fds_tail = (idx + 1) & (FDS_SIZE - 1);
            SafeFileHandle fd = fds[idx];
            fds[idx] = null;
            return fd;
        }

        public unsafe void Receive(SafeHandle sockfd)
        {
            byte* cmsg = stackalloc byte[CMSG_SIZE];

            SysNative.IoVector* iov = stackalloc SysNative.IoVector[2];
            int iov_len = 0;

            int space = BUF_SIZE - buf_cnt;
            int first = Math.Min(BUF_SIZE - buf_head, space);
            int second = space - first;

            fixed (byte* buf = this.buf)
            {
                if (first > 0)
                {
                    iov[0].iov_base = buf + buf_head;
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
                    msg_iov = iov,
                    msg_iovlen = iov_len,
                    msg_control = cmsg,
                    msg_controllen = CMSG_SIZE
                };

                SysNative.Result result = SysNative.RecvMsg(sockfd, ref msg);
                if (result.retval < 0)
                {
                    if (result.errno == 11) // EAGAIN
                    {
                        return;
                    }
                    throw new IOException(Marshal.GetPInvokeErrorMessage(result.errno));
                }
                parse_cmsg(cmsg, (int)msg.msg_controllen);
                buf_head = (buf_head + result.retval) & (BUF_SIZE - 1);
                buf_cnt += result.retval;
            }
        }

        private unsafe void parse_cmsg(byte* cmsg, int clen)
        {
            while (clen >= sizeof(SysNative.CMsgHeader))
            {
                SysNative.CMsgHeader* header = (SysNative.CMsgHeader*)cmsg;

                int cmsg_len = (int)header->cmsg_len;
                if ((cmsg_len < sizeof(SysNative.CMsgHeader)) || (cmsg_len > clen))
                {
                    break;
                }

                if ((header->cmsg_level == 1) && (header->cmsg_type == 1))
                {
                    int cmsg_fds_cnt = (cmsg_len - sizeof(SysNative.CMsgHeader)) / 4;
                    if ((fds_cnt + cmsg_fds_cnt) > FDS_SIZE)
                    {
                        throw new InternalBufferOverflowException();
                    }
                    fds_cnt += cmsg_fds_cnt;

                    ReadOnlySpan<int> cmsg_fds = new ReadOnlySpan<int>(
                        cmsg + sizeof(SysNative.CMsgHeader), cmsg_fds_cnt);

                    int idx = fds_head;
                    foreach (int fd in cmsg_fds)
                    {
                        fds[idx] = new SafeFileHandle(fd, true);
                        idx = (idx + 1) & (FDS_SIZE - 1);
                    }
                    fds_head = idx;
                }

                cmsg_len = (cmsg_len + (IntPtr.Size - 1)) & ~(IntPtr.Size - 1);
                clen -= cmsg_len;
                cmsg += cmsg_len;
            }
        }
    }
}