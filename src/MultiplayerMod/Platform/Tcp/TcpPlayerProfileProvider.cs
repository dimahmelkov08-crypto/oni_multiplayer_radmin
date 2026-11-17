using System;
using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Multiplayer.Players;

namespace MultiplayerMod.Platform.Tcp;

[Dependency, UsedImplicitly]
public class TcpPlayerProfileProvider : IPlayerProfileProvider {

    private readonly Lazy<PlayerProfile> profile = new(() => new PlayerProfile(Environment.UserName));

    public PlayerProfile GetPlayerProfile() => profile.Value;

}
