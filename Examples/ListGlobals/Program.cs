using WaylandNET.Client;

using var conn = new WaylandClientConnection();
var registry = conn.Display.GetRegistry();
registry.Global += (_, name, iface, version) =>
{
    Console.WriteLine($"Global {name}: {iface} version {version}");
};
conn.Display.Sync().Done += (callback, data) => conn.Quit();
conn.MessageLoop();
