using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WaylandNET.Client;
using WaylandNET.Client.Protocol;

public class Window
{
    private const ushort width = 640;
    private const ushort height = 400;
    private const int size = width * height * 4;

    public static void Main()
    {
        var win = new Window();
        win.Run();
    }

    public unsafe void Run()
    {
        conn = new WaylandClientConnection();
        try
        {
            if ((memfd = memfd_create("wayland", MFD_CLOEXEC | MFD_ALLOW_SEALING)).IsInvalid)
            {
                throw new IOException(Marshal.GetLastPInvokeErrorMessage());
            }
            if (ftruncate(memfd, size) == -1)
            {
                throw new IOException(Marshal.GetLastPInvokeErrorMessage());
            }
            if (fcntl(memfd, F_ADD_SEALS, F_SEAL_GROW | F_SEAL_SHRINK | F_SEAL_SEAL) == -1)
            {
                throw new IOException(Marshal.GetLastPInvokeErrorMessage());
            }
            if ((bits = mmap(0, size, PROT_READ | PROT_WRITE, MAP_SHARED, memfd, 0)) == -1)
            {
                throw new IOException(Marshal.GetLastPInvokeErrorMessage());
            }
            using (var image = Assembly.GetExecutingAssembly().GetManifestResourceStream("Window.Cat.img"))
            {
                image.ReadExactly(new Span<byte>((void*)bits, size));
            }

            repeatTimer = conn.CreateTimer();
            repeatTimer.Elapsed += RepeatTimerEvent;

            var registry = conn.Display.GetRegistry();
            registry.Global += GlobalEvent;

            conn.Display.Sync().Done += InitEvent;
            conn.MessageLoop();
        }
        finally
        {
            conn.Dispose();
            memfd.Dispose();
            munmap(bits, size);
            bits = -1;
        }
    }

    private void GlobalEvent(WlRegistry registry, uint name, string iface, uint version)
    {
        if (iface == "wl_shm")
        {
            shm = registry.Bind<WlShm>(name, iface, version);
        }
        else if (iface == "wl_seat")
        {
            seat = registry.Bind<WlSeat>(name, iface, version);
        }
        else if (iface == "wl_compositor")
        {
            compositor = registry.Bind<WlCompositor>(name, iface, version);
        }
        else if (iface == "xdg_wm_base")
        {
            xdg_wm_base = registry.Bind<XdgWmBase>(name, iface, version);
        }
        else if (iface == "zxdg_decoration_manager_v1")
        {
            zxdg_manager = registry.Bind<ZxdgDecorationManagerV1>(name, iface, version);
        }
    }

    private void InitEvent(WlCallback callback, uint data)
    {
        var pool = shm.CreatePool(memfd, size);
        buffer = pool.CreateBuffer(0, width, height, 4 * width, WlShm.FormatEnum.Xrgb8888);
        buffer.Release += ReleaseEvent;
        pool.Destroy();

        seat.Capabilities += CapabilitiesEvent;
        xdg_wm_base.Ping += PingEvent;

        surface = compositor.CreateSurface();
        xdg_surface = xdg_wm_base.GetXdgSurface(surface);
        xdg_surface.Configure += ConfigureEvent;

        xdg_toplevel = xdg_surface.GetToplevel();
        xdg_toplevel.SetTitle("Hello Wayland !");
        xdg_toplevel.SetMinSize(width, height);
        xdg_toplevel.SetMaxSize(width, height);
        xdg_toplevel.Close += CloseEvent;

        zxdg_decoration = zxdg_manager?.GetToplevelDecoration(xdg_toplevel);

        surface.Commit();
    }

    private void CapabilitiesEvent(WlSeat seat, WlSeat.Capability capabilities)
    {
        if (capabilities.HasFlag(WlSeat.Capability.Keyboard))
        {
            if (keyboard == null)
            {
                keyboard = seat.GetKeyboard();
                keyboard.Keymap += KeymapEvent;
                keyboard.RepeatInfo += RepeatInfoEvent;
                keyboard.Key += KeyEvent;
            }
        }
        else
        {
            if (keyboard != null)
            {
                keyboard.Keymap -= KeymapEvent;
                keyboard.RepeatInfo -= RepeatInfoEvent;
                keyboard.Key -= KeyEvent;
                keyboard = null;
            }
        }
    }

    private void KeymapEvent(WlKeyboard keyboard, WlKeyboard.KeymapFormat format, SafeFileHandle fd, uint size)
    {
        using (var stream = new FileStream(fd, FileAccess.Read))
        {
        }
    }

    private void RepeatInfoEvent(WlKeyboard keyboard, int rate, int delay)
    {
        repeatRate = rate;
        repeatDelay = delay;
    }

    private void KeyEvent(WlKeyboard keyboard, uint serial, uint time, uint key, WlKeyboard.KeyState state)
    {
        switch (state)
        {
            case WlKeyboard.KeyState.Pressed:
                repeatKey = key;
                if (repeatRate > 0)
                {
                    repeatTimer.SetTimeout(repeatDelay);
                    repeatTimer.Start();
                }
                KeyPressed(key);
                break;

            case WlKeyboard.KeyState.Released:
                if (key == repeatKey)
                {
                    repeatTimer.Stop();
                }
                KeyReleased(key);
                break;

            case WlKeyboard.KeyState.Repeated:
                KeyPressed(key);
                break;
        }
    }

    private void ConfigureEvent(XdgSurface xdg_surface, uint serial)
    {
        xdg_surface.AckConfigure(serial);
        if (!xdg_configured)
        {
            surface.Attach(buffer, 0, 0);
            surface.Commit();
            xdg_configured = true;
        }
    }

    private void RepeatTimerEvent(IClientTimer timer)
    {
        if (!timer.Running)
        {
            timer.SetTimeout(1000 / repeatRate, true);
            timer.Start();
        }
        KeyPressed(repeatKey);
    }

    private void CloseEvent(XdgToplevel xdg_toplevel)
    {
        zxdg_decoration?.Destroy();
        xdg_toplevel.Destroy();
        xdg_surface.Destroy();
        surface.Destroy();
        xdg_configured = false;
    }

    private void ReleaseEvent(WlBuffer buffer)
    {
        buffer.Destroy();
        conn.Quit();
    }

    private void KeyPressed(uint key)
    {
        Console.WriteLine("key pressed: " + key);
    }

    private void KeyReleased(uint key)
    {
        Console.WriteLine("key released: " + key);
    }

    private WaylandClientConnection conn;

    private WlShm shm;
    private WlSeat seat;
    private WlCompositor compositor;
    private XdgWmBase xdg_wm_base;
    private ZxdgDecorationManagerV1 zxdg_manager;

    private WlSurface surface;
    private XdgSurface xdg_surface;
    private XdgToplevel xdg_toplevel;
    private ZxdgToplevelDecorationV1 zxdg_decoration;
    private bool xdg_configured;

    private WlBuffer buffer;
    private SafeFileHandle memfd;
    private IntPtr bits = -1;

    private WlKeyboard keyboard;
    private int repeatRate;
    private int repeatDelay;
    private uint repeatKey;
    private IClientTimer repeatTimer;

    private static void PingEvent(XdgWmBase xdg_wm_base, uint serial) => xdg_wm_base.Pong(serial);

    [DllImport("libc", SetLastError = true)]
    private static extern SafeFileHandle memfd_create(string name, uint flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int ftruncate(SafeHandle fd, long length);

    [DllImport("libc", SetLastError = true)]
    private static extern int fcntl(SafeHandle fd, int cmd, int arg);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr mmap(IntPtr addr, IntPtr length, int prot, int flags, SafeHandle fd, long offset);

    [DllImport("libc", SetLastError = true)]
    private static extern int munmap(IntPtr addr, IntPtr length);

    private const int MFD_CLOEXEC = 0x0001;
    private const int MFD_ALLOW_SEALING = 0x0002;
    private const int F_ADD_SEALS = 1033;
    private const int F_SEAL_GROW = 0x0004;
    private const int F_SEAL_SHRINK = 0x0002;
    private const int F_SEAL_SEAL = 0x0001;
    private const int PROT_READ = 1;
    private const int PROT_WRITE = 2;
    private const int MAP_SHARED = 1;
}
