using System;
using MultiplayerMod.Multiplayer.Commands;
using MultiplayerMod.Network;

namespace MultiplayerMod.Platform.Tcp.Network.Messaging;

[Serializable]
public class NetworkMessage : INetworkMessage {

    public IMultiplayerCommand Command { get; }
    public MultiplayerCommandOptions Options { get; }

    public NetworkMessage(IMultiplayerCommand command, MultiplayerCommandOptions options) {
        Command = command;
        Options = options;
    }

}
