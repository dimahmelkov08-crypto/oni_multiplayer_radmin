using MultiplayerMod.Core.Events;

namespace MultiplayerMod.Multiplayer.CoreOperations.Events;

public record GameStartedEvent(MultiplayerGame Multiplayer) : IDispatchableEvent;
