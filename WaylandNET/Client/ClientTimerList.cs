using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace WaylandNET.Client
{
    public class ClientTimerList
    {
        public IClientTimer CreateTimer()
        {
            return new TimerImpl(list);
        }

        public int GetTimeout()
        {
            if (list.Count == 0)
            {
                return -1;
            }

            long current = Stopwatch.GetTimestamp();
            LinkedListNode<TimerImpl> node = list.First;
            while (node != null)
            {
                TimerImpl timer = node.Value;
                node = node.Next;
                if ((timer.Deadline - current) < TicksPerMillisecond)
                {
                    timer.FireEvent();
                }
            }

            if (list.Count == 0)
            {
                return -1;
            }

            long next = long.MaxValue;
            foreach (TimerImpl timer in list)
            {
                if (timer.Deadline < next)
                {
                    next = timer.Deadline;
                }
            }

            long timeout = next - current;
            if (timeout <= 0)
            {
                return 0;
            }
            if (timeout < (long.MaxValue / 1000))
            {
                long msec = (timeout * 1000) / Stopwatch.Frequency;
                if (msec < int.MaxValue)
                {
                    return (int)msec;
                }
            }
            return int.MaxValue;
        }

        private class TimerImpl : IClientTimer
        {
            public TimerImpl(LinkedList<TimerImpl> list)
            {
                this.list = list;
                this.node = new LinkedListNode<TimerImpl>(this);
            }

            public void SetTimeout(int timeout, bool periodic)
            {
                if (timeout < 1)
                {
                    throw new ArgumentException(nameof(timeout));
                }
                this.interval = ((long)timeout * Stopwatch.Frequency) / 1000;
                this.periodic = periodic;
                if (node.List != null)
                {
                    deadline = Stopwatch.GetTimestamp() + interval;
                }
            }

            public void Start()
            {
                if (node.List == null)
                {
                    deadline = Stopwatch.GetTimestamp() + interval;
                    list.AddFirst(node);
                }
            }

            public void Stop()
            {
                node.List?.Remove(node);
            }

            public bool Running => node.List != null;

            public event Action<IClientTimer> Elapsed
            {
                add => handler += value;
                remove => handler -= value;
            }

            public void FireEvent()
            {
                if (periodic)
                {
                    deadline += interval;
                }
                else
                {
                    node.List.Remove(node);
                }
                handler?.Invoke(this);
            }

            public long Deadline => deadline;

            private long deadline;
            private long interval;
            private bool periodic;
            private Action<IClientTimer> handler;

            private readonly LinkedList<TimerImpl> list;
            private readonly LinkedListNode<TimerImpl> node;
        }

        private readonly LinkedList<TimerImpl> list = new LinkedList<TimerImpl>();
        private static readonly long TicksPerMillisecond = Stopwatch.Frequency / 1000;
    }
}
