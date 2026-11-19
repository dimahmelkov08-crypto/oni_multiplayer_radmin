using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Events;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Game.Chores;
using MultiplayerMod.Game.World;
using MultiplayerMod.Multiplayer.Commands.Chores;
using MultiplayerMod.Multiplayer.Commands.Debug;
using MultiplayerMod.Multiplayer.CoreOperations.Events;
using MultiplayerMod.Multiplayer.World;
using MultiplayerMod.Multiplayer.World.Debug;
using MultiplayerMod.Network;

namespace MultiplayerMod.Multiplayer.CoreOperations.Binders;

[Dependency, UsedImplicitly]
public class HostEventsBinder {

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<HostEventsBinder>();
    private readonly IMultiplayerServer server;
    private readonly WorldManager worldManager;
    private readonly EventDispatcher eventDispatcher;

    // Forced full world resync (host sends the whole colony to clients) once per this many cycles
    private const int SyncIntervalCycles = 5;

    private EventSubscriptions subscriptions = null!;
    private int lastSyncCycle;

    public HostEventsBinder(IMultiplayerServer server, WorldManager worldManager, EventDispatcher eventDispatcher) {
        this.server = server;
        this.worldManager = worldManager;
        this.eventDispatcher = eventDispatcher;

        Bind();
    }

    private void Bind() {
        log.Debug("Binding host events");

        eventDispatcher.Subscribe<GameStartedEvent>(OnGameStarted);
        eventDispatcher.Subscribe<StopMultiplayerEvent>(OnStopMultiplayer);

        ChoreConsumerEvents.FindNextChore += p => server.Send(new FindNextChore(p), MultiplayerCommandOptions.SkipHost);
    }

    private void OnGameStarted(GameStartedEvent @event) {
        if (@event.Multiplayer.Mode != MultiplayerMode.Host)
            return;

        lastSyncCycle = GameClock.Instance.GetCycle();
        GameClockEvents.CycleChanged += OnCycleChanged;

        subscriptions = new EventSubscriptions {
            eventDispatcher.Subscribe<DebugSnapshotAvailableEvent>(
                e => server.Send(new SyncWorldDebugSnapshot(e.Snapshot))
            )
        };
    }

    private void OnCycleChanged(int cycle) {
        if (cycle - lastSyncCycle < SyncIntervalCycles)
            return;

        lastSyncCycle = cycle;
        worldManager.Sync();
    }

    private void OnStopMultiplayer(StopMultiplayerEvent @event) {
        if (@event.Multiplayer.Mode != MultiplayerMode.Host)
            return;

        GameClockEvents.CycleChanged -= OnCycleChanged;
        subscriptions.Cancel();
    }

}
