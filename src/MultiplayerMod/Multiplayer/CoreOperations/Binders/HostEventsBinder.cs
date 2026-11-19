using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Events;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Scheduling;
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
    private readonly MultiplayerGame multiplayer;
    private readonly UnityTaskScheduler scheduler;

    // Forced full world resync (host sends the whole colony to clients) once per this many cycles (host keeps playing, no overlay)
    private const int SyncIntervalCycles = 1;

    private EventSubscriptions subscriptions = null!;
    private int lastSyncCycle;

    public HostEventsBinder(
        IMultiplayerServer server,
        WorldManager worldManager,
        EventDispatcher eventDispatcher,
        MultiplayerGame multiplayer,
        UnityTaskScheduler scheduler
    ) {
        this.multiplayer = multiplayer;
        this.scheduler = scheduler;
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
        AutoSaveEvents.AutoSaved += OnAutoSaved;

        subscriptions = new EventSubscriptions {
            eventDispatcher.Subscribe<DebugSnapshotAvailableEvent>(
                e => server.Send(new SyncWorldDebugSnapshot(e.Snapshot))
            )
        };
    }

    private const int AutoSaveWaitMs = 5000;

    private bool syncPending;
    private int pendingToken;

    // End of the cycle: wait for the game's autosave and sync exactly that file.
    // If it doesn't show up, trigger the autosave ourselves; as the last resort sync a fresh save.
    private void OnCycleChanged(int cycle) {
        if (cycle - lastSyncCycle < SyncIntervalCycles)
            return;

        lastSyncCycle = cycle;
        var token = ++pendingToken;
        syncPending = true;

        Task.Delay(AutoSaveWaitMs).ContinueWith(
            _ => scheduler.Run(() => {
                if (!syncPending || token != pendingToken)
                    return;
                try {
                    GameClock.Instance.DoAutoSave(cycle);
                } catch (Exception e) {
                    log.Warning($"Unable to trigger autosave: {e.Message}");
                }

                Task.Delay(AutoSaveWaitMs).ContinueWith(
                    __ => scheduler.Run(() => {
                        if (!syncPending || token != pendingToken)
                            return;
                        syncPending = false;
                        RunSync(null);
                    }),
                    TaskScheduler.Default
                );
            }),
            TaskScheduler.Default
        );
    }

    private void OnAutoSaved(string path) {
        if (!syncPending)
            return;

        syncPending = false;
        // Not inside SaveLoader.Save: sync on the next tick
        scheduler.Run(() => RunSync(path));
    }

    private void RunSync(string? savePath) {
        try {
            // Nobody to sync with, or previous sync is still in progress
            if (multiplayer.Players.Count < 2 || !multiplayer.Players.Ready)
                return;

            worldManager.Sync(showOverlay: false, savePath);
        } catch (Exception e) {
            // A failed periodic sync must never crash or freeze the host's game
            log.Error($"Periodic world sync failed: {e}");
        }
    }

    private void OnStopMultiplayer(StopMultiplayerEvent @event) {
        if (@event.Multiplayer.Mode != MultiplayerMode.Host)
            return;

        GameClockEvents.CycleChanged -= OnCycleChanged;
        AutoSaveEvents.AutoSaved -= OnAutoSaved;
        syncPending = false;
        subscriptions.Cancel();
    }

}
