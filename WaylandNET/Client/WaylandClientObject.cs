namespace WaylandNET.Client
{
    public abstract class WaylandClientObject : WaylandObject
    {
        public WaylandClientConnection ClientConnection { get; private set; }

        protected WaylandClientObject(string @interface, uint id, uint version,
            WaylandClientConnection connection) : base(@interface, id, version, connection)
        {
            ClientConnection = connection;
        }
    }
}
