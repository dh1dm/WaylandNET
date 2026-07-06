/// Copyright © 2018 Simon Ser
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a
/// copy of this software and associated documentation files (the "Software"),
/// to deal in the Software without restriction, including without limitation
/// the rights to use, copy, modify, merge, publish, distribute, sublicense,
/// and/or sell copies of the Software, and to permit persons to whom the
/// Software is furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice (including the next
/// paragraph) shall be included in all copies or substantial portions of the
/// Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL
/// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
/// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
/// DEALINGS IN THE SOFTWARE.
using System;
using Microsoft.Win32.SafeHandles;
using WaylandNET;
using WaylandNET.Client;
namespace WaylandNET.Client.Protocol
{
    /// zxdg_decoration_manager_v1 version 2
    /// <summary>
    /// window decoration manager
    /// <para>
    /// This interface allows a compositor to announce support for server-side
    /// decorations.
    /// 
    /// A window decoration is a set of window controls as deemed appropriate by
    /// the party managing them, such as user interface components used to move,
    /// resize and change a window's state.
    /// 
    /// A client can use this protocol to request being decorated by a supporting
    /// compositor.
    /// 
    /// If compositor and client do not negotiate the use of a server-side
    /// decoration using this protocol, clients continue to self-decorate as they
    /// see fit.
    /// 
    /// Warning! The protocol described in this file is experimental and
    /// backward incompatible changes may be made. Backward compatible changes
    /// may be added together with the corresponding interface version bump.
    /// Backward incompatible changes are done by bumping the version number in
    /// the protocol and interface names and resetting the interface version.
    /// Once the protocol is to be declared stable, the 'z' prefix and the
    /// version number in the protocol and interface names are removed and the
    /// interface version number is reset.
    /// </para>
    /// </summary>
    public sealed class ZxdgDecorationManagerV1 : WaylandClientObject
    {
        public ZxdgDecorationManagerV1(uint id, uint version, WaylandClientConnection connection) : base("zxdg_decoration_manager_v1", id, version, connection)
        {
        }
        public enum RequestOpcode : ushort
        {
            Destroy,
            GetToplevelDecoration,
        }
        public enum EventOpcode : ushort
        {
        }
        public override void Handle(ushort opcode, params object[] arguments)
        {
            switch ((EventOpcode)opcode)
            {
                default:
                    throw new ArgumentOutOfRangeException("unknown event");
            }
        }
        public override WaylandType[] Arguments(ushort opcode)
        {
            switch ((EventOpcode)opcode)
            {
                default:
                    throw new ArgumentOutOfRangeException("unknown event");
            }
        }
        /// <summary>
        /// destroy the decoration manager object
        /// <para>
        /// Destroy the decoration manager. This doesn't destroy objects created
        /// with the manager.
        /// </para>
        /// </summary>
        public void Destroy()
        {
            Marshal((ushort)RequestOpcode.Destroy);
            Die();
        }
        /// <summary>
        /// create a new toplevel decoration object
        /// <para>
        /// Create a new decoration object associated with the given toplevel.
        /// 
        /// For objects of version 1, creating an xdg_toplevel_decoration from an
        /// xdg_toplevel which has a buffer attached or committed is a client
        /// error, and any attempts by a client to attach or manipulate a buffer
        /// prior to the first xdg_toplevel_decoration.configure event must also be
        /// treated as errors.
        /// 
        /// For objects of version 2 or newer, creating an xdg_toplevel_decoration
        /// from an xdg_toplevel which has a buffer attached or committed is
        /// allowed. The initial decoration mode of the surface if a buffer is
        /// already attached depends on whether a xdg_toplevel_decoration object
        /// has been associated with the surface or not prior to this request.
        /// 
        /// If an xdg_toplevel_decoration was associated with the surface, then
        /// destroyed without a surface commit, the previous decoration mode is
        /// retained.
        /// 
        /// If no xdg_toplevel_decoration was associated with the surface prior to
        /// this request, or if a surface commit has been performed after a previous
        /// xdg_toplevel_decoration object associated with the surface was
        /// destroyed, the decoration mode is assumed to be client-side.
        /// </para>
        /// </summary>
        public ZxdgToplevelDecorationV1 GetToplevelDecoration(XdgToplevel toplevel)
        {
            uint id = Connection.AllocateId();
            Marshal((ushort)RequestOpcode.GetToplevelDecoration, id, toplevel.Id);
            Connection[id] = new ZxdgToplevelDecorationV1(id, Version, ClientConnection);
            return (ZxdgToplevelDecorationV1)Connection[id];
        }
    }
    /// zxdg_toplevel_decoration_v1 version 2
    /// <summary>
    /// decoration object for a toplevel surface
    /// <para>
    /// The decoration object allows the compositor to toggle server-side window
    /// decorations for a toplevel surface. The client can request to switch to
    /// another mode.
    /// 
    /// The xdg_toplevel_decoration object must be destroyed before its
    /// xdg_toplevel.
    /// </para>
    /// </summary>
    public sealed class ZxdgToplevelDecorationV1 : WaylandClientObject
    {
        public ZxdgToplevelDecorationV1(uint id, uint version, WaylandClientConnection connection) : base("zxdg_toplevel_decoration_v1", id, version, connection)
        {
        }
        public enum RequestOpcode : ushort
        {
            Destroy,
            SetMode,
            UnsetMode,
        }
        public enum EventOpcode : ushort
        {
            Configure,
        }
        /// <summary>
        /// notify a decoration mode change
        /// <para>
        /// The configure event configures the effective decoration mode. The
        /// configured state should not be applied immediately. Clients must send an
        /// ack_configure in response to this event. See xdg_surface.configure and
        /// xdg_surface.ack_configure for details.
        /// 
        /// A configure event can be sent at any time. The specified mode must be
        /// obeyed by the client.
        /// </para>
        /// </summary>
        /// <param name="mode">the decoration mode</param>
        public delegate void ConfigureHandler(ZxdgToplevelDecorationV1 zxdgToplevelDecorationV1, Mode mode);
        public event ConfigureHandler Configure;
        public override void Handle(ushort opcode, params object[] arguments)
        {
            switch ((EventOpcode)opcode)
            {
                case EventOpcode.Configure:
                    {
                        var mode = (Mode)(uint)arguments[0];
                        Configure?.Invoke(this, mode);
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException("unknown event");
            }
        }
        public override WaylandType[] Arguments(ushort opcode)
        {
            switch ((EventOpcode)opcode)
            {
                case EventOpcode.Configure:
                    return new WaylandType[]
                    {
                        WaylandType.UInt,
                    };
                default:
                    throw new ArgumentOutOfRangeException("unknown event");
            }
        }
        public enum Error : int
        {
            UnconfiguredBuffer = 0,
            AlreadyConstructed = 1,
            Orphaned = 2,
            InvalidMode = 3,
        }
        /// <summary>
        /// window decoration modes
        /// <para>
        /// These values describe window decoration modes.
        /// </para>
        /// </summary>
        public enum Mode : int
        {
            ClientSide = 1,
            ServerSide = 2,
        }
        /// <summary>
        /// destroy the decoration object
        /// <para>
        /// Switch back to a mode without any server-side decorations at the next
        /// commit, unless a new xdg_toplevel_decoration is created for the surface
        /// first.
        /// </para>
        /// </summary>
        public void Destroy()
        {
            Marshal((ushort)RequestOpcode.Destroy);
            Die();
        }
        /// <summary>
        /// set the decoration mode
        /// <para>
        /// Set the toplevel surface decoration mode. This informs the compositor
        /// that the client prefers the provided decoration mode.
        /// 
        /// After requesting a decoration mode, the compositor will respond by
        /// emitting an xdg_surface.configure event. The client should then update
        /// its content, drawing it without decorations if the received mode is
        /// server-side decorations. The client must also acknowledge the configure
        /// when committing the new content (see xdg_surface.ack_configure).
        /// 
        /// The compositor can decide not to use the client's mode and enforce a
        /// different mode instead.
        /// 
        /// Clients whose decoration mode depend on the xdg_toplevel state may send
        /// a set_mode request in response to an xdg_surface.configure event and wait
        /// for the next xdg_surface.configure event to prevent unwanted state.
        /// Such clients are responsible for preventing configure loops and must
        /// make sure not to send multiple successive set_mode requests with the
        /// same decoration mode.
        /// 
        /// If an invalid mode is supplied by the client, the invalid_mode protocol
        /// error is raised by the compositor.
        /// </para>
        /// </summary>
        /// <param name="mode">the decoration mode</param>
        public void SetMode(Mode mode)
        {
            Marshal((ushort)RequestOpcode.SetMode, (uint)mode);
        }
        /// <summary>
        /// unset the decoration mode
        /// <para>
        /// Unset the toplevel surface decoration mode. This informs the compositor
        /// that the client doesn't prefer a particular decoration mode.
        /// 
        /// This request has the same semantics as set_mode.
        /// </para>
        /// </summary>
        public void UnsetMode()
        {
            Marshal((ushort)RequestOpcode.UnsetMode);
        }
    }
}
