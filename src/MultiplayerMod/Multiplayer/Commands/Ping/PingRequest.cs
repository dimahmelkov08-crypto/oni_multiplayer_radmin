using System;
using MultiplayerMod.ModRuntime.StaticCompatibility;
using MultiplayerMod.Network;

namespace MultiplayerMod.Multiplayer.Commands.Ping;

[Serializable]
[MultiplayerCommand(Type = MultiplayerCommandType.System, ExecuteOnServer = true)]
public class PingRequest : MultiplayerCommand {

    private long sentTimestamp;

    public PingRequest(long sentTimestamp) {
        this.sentTimestamp = sentTimestamp;
    }

    public override void Execute(MultiplayerCommandContext context) {
        if (context.ClientId == null)
            return;
        Dependencies.Get<IMultiplayerServer>().Send(context.ClientId, new PongResponse(sentTimestamp));
    }

}
