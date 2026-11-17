using MultiplayerMod.Network;

namespace MultiplayerMod.Platform.Tcp.Network;

public record TcpEndpoint(string Host, int Port) : IMultiplayerEndpoint;
