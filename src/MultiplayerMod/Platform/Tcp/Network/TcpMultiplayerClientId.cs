using System;
using MultiplayerMod.Network;

namespace MultiplayerMod.Platform.Tcp.Network;

[Serializable]
public record TcpMultiplayerClientId(Guid Id) : IMultiplayerClientId {

    /// <summary>Identity of this game instance. Sent to the host during the handshake.</summary>
    public static readonly TcpMultiplayerClientId Local = new(Guid.NewGuid());

    public bool Equals(IMultiplayerClientId other) => other is TcpMultiplayerClientId player && player.Id == Id;

}
