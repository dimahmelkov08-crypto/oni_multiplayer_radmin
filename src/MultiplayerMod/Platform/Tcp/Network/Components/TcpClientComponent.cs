using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Unity;

// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace MultiplayerMod.Platform.Tcp.Network.Components;

public class TcpClientComponent : MultiplayerMonoBehaviour {

    [InjectDependency]
    private TcpMultiplayerClient client = null!;

    private void Update() => client.Tick();

}
