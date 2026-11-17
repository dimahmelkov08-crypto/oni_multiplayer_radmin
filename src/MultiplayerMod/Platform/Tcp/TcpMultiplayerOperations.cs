using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Platform.Tcp.UI;

namespace MultiplayerMod.Platform.Tcp;

[Dependency, UsedImplicitly]
public class TcpMultiplayerOperations : IMultiplayerOperations {

    // Instead of the Steam friends overlay we ask for the host address (IP:port, e.g. a Radmin VPN address).
    public void Join() => UnityObject.CreateStaticWithComponent<JoinDialog>();

}
