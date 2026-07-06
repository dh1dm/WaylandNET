using System;

namespace WaylandNET.Client
{
    public interface IClientTimer
    {
        void SetTimeout(int timeout, bool periodic = false);
        void Start();
        void Stop();
        bool Running { get; }
        event Action<IClientTimer> Elapsed;
    }
}
