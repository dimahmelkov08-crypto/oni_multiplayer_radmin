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

        subscriptions = new EventSubscriptions {
            eventDispatcher.Subscribe<DebugSnapshotAvailableEvent>(
                e => server.Send(new SyncWorldDebugSnapshot(e.Snapshot))
            )
        };
    }

    private int pendingToken;

    // End of the cycle: take the autosave the game has just written and sync exactly that file.
    // The game's own autosave may land slightly before or after we notice the new cycle, so look back a few seconds.
    // If no autosave shows up, sync a fresh save instead.
    private void OnCycleChanged(int cycle) {
        if (cycle - lastSyncCycle < SyncIntervalCycles)
            return;

        lastSyncCycle = cycle;
        WaitForAutoSave(++pendingToken, UnityEngine.Time.realtimeSinceStartup, 0);
    }

    private void WaitForAutoSave(int token, float changedAt, int attempt) {
        Task.Delay(attempt == 0 ? 2000 : 4000).ContinueWith(
            _ => scheduler.Run(() => {
                if (token != pendingToken)
                    return;

                var path = FindFreshAutoSave(changedAt);
                if (path != null) {
                    RunSync(path);
                    return;
                }

                if (attempt == 0) {
                    WaitForAutoSave(token, changedAt, 1);
                    return;
                }

                log.Info("No fresh autosave found at the end of the cycle, syncing a fresh save");
                RunSync(null);
            }),
            TaskScheduler.Default
        );
    }

    private static string? FindFreshAutoSave(float changedAt) {
        var path = AutoSaveEvents.LastPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return null;
        return AutoSaveEvents.LastTime >= changedAt - 5f ? path : null;
    }

    private void RunSync(string? savePath) {
        try {
            // Nobody to sync with, or previous sync is still in progress
            if (multiplayer.Players.Count < 2 || !multiplayer.Players.Ready)
                return;

            log.Info($"Periodic world sync: {(savePath != null ? "autosave " + savePath : "fresh save")}");
            worldManager.Sync(showOverlay: true, savePath);
        } catch (Exception e) {
            // A failed periodic sync must never crash or freeze the host's game
            log.Error($"Periodic world sync failed: {e}");
        }
    }

    private void OnStopMultiplayer(StopMultiplayerEvent @event) {
        if (@event.Multiplayer.Mode != MultiplayerMode.Host)
            return;

        GameClockEvents.CycleChanged -= OnCycleChanged;
        pendingToken++;
        subscriptions.Cancel();
    }

}
