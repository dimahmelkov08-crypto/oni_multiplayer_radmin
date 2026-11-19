using System;
using MultiplayerMod.Multiplayer.Ping;

namespace MultiplayerMod.Multiplayer.Commands.Ping;

[Serializable]
[MultiplayerCommand(Type = MultiplayerCommandType.System)]
public class PongResponse : MultiplayerCommand {

    private long sentTimestamp;

    public PongResponse(long sentTimestamp) {
        this.sentTimestamp = sentTimestamp;
    }

    public override void Execute(MultiplayerCommandContext context) => PingState.Report(sentTimestamp);

}
