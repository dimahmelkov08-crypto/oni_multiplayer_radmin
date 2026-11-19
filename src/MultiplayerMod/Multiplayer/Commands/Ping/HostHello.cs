using System;
using MultiplayerMod.Multiplayer.Ping;

namespace MultiplayerMod.Multiplayer.Commands.Ping;

/// <summary>
/// Sent by a host running a mod version that understands PingRequest. A client never pings a host that didn't say hello:
/// an older host can't deserialize the unknown command and would drop the connection.
/// </summary>
[Serializable]
[MultiplayerCommand(Type = MultiplayerCommandType.System)]
public class HostHello : MultiplayerCommand {

    public override void Execute(MultiplayerCommandContext context) => PingState.HostSupportsPing = true;

}
