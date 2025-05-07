using System;
using System.Threading.Tasks;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Defines the interface for a class that can handle specific game packets.
    /// Packet registration typically happens via PacketHandlerAttribute on methods.
    /// </summary>
    public interface IGamePacketHandler
    {
        // No specific methods required by the interface itself,
        // as registration relies on attributes within implementing classes.
        // This interface serves mainly as a marker and for dependency injection grouping.
    }
}