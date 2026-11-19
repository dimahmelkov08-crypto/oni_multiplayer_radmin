using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Events;
using MultiplayerMod.Core.Extensions;
using MultiplayerMod.Core.Paths;
using MultiplayerMod.Core.Scheduling;
using MultiplayerMod.Game.World;
using MultiplayerMod.ModRuntime.Context;
using MultiplayerMod.ModRuntime.StaticCompatibility;
using MultiplayerMod.Multiplayer.Commands.Speed;
using MultiplayerMod.Multiplayer.Commands.World;
using MultiplayerMod.Multiplayer.CoreOperations.PlayersManagement.Commands;
using MultiplayerMod.Multiplayer.Players;
using MultiplayerMod.Multiplayer.Players.Events;
using MultiplayerMod.Multiplayer.UI.Overlays;
using MultiplayerMod.Network;

namespace MultiplayerMod.Multiplayer.World;

[Dependency, UsedImplicitly]
public class WorldManager {

    private const int ResumeTimeoutMs = 60000;

    private readonly IMultiplayerServer server;
    private readonly MultiplayerGame multiplayer;
    private readonly EventDispatcher events;
    private readonly UnityTaskScheduler scheduler;
    private readonly ExecutionLevelManager executionLevelManager;


    public WorldManager(
        IMultiplayerServer server,
        MultiplayerGame multiplayer,
        EventDispatcher events,
        UnityTaskScheduler scheduler,
        ExecutionLevelManager executionLevelManager
    ) {
        this.server = server;
        this.multiplayer = multiplayer;
        this.events = events;
        this.scheduler = scheduler;
        this.executionLevelManager = executionLevelManager;
    }

    public void Sync(bool showOverlay = true, string? savePath = null) {
        if (showOverlay)
            SetupStatusOverlay();

        var resume = !SpeedControlScreen.Instance.IsPaused;
        server.Send(new PauseGame());
        multiplayer.Objects.SynchronizeWithTracker();
        multiplayer.Players.ForEach(it => server.Send(new ChangePlayerStateCommand(it.Id, PlayerState.Loading)));
        server.Send(new ChangePlayerStateCommand(multiplayer.Players.Current.Id, PlayerState.Ready));
        server.Send(new NotifyWorldSavePreparing(), MultiplayerCommandOptions.SkipHost);
        server.Send(new LoadWorld(WorldName, GetWorldSave(savePath)), MultiplayerCommandOptions.SkipHost);

        if (!resume)
            return;

        // The game stays paused until every player is Ready (see ResumeGame).
        server.Send(new ResumeGame());
        ScheduleStuckSyncRecovery();
    }

    // If a client never becomes Ready (dropped while loading), don't leave the host paused forever
    private void ScheduleStuckSyncRecovery() {
        Task.Delay(ResumeTimeoutMs).ContinueWith(
            _ => scheduler.Run(() => {
                try {
                    if (multiplayer.Players.Ready)
                        return;
                    if (SpeedControlScreen.Instance != null && SpeedControlScreen.Instance.IsPaused)
                        SpeedControlScreen.Instance.Unpause();
                    MultiplayerStatusOverlay.Close();
                } catch (Exception) {
                    // never let recovery crash the game
                }
            }),
            TaskScheduler.Default
        );
    }

    private void SetupStatusOverlay() {
        MultiplayerStatusOverlay.Show("Ожидание игроков...");
        events.Subscribe<PlayerStateChangedEvent>(
            (_, subscription) => {
                var players = multiplayer.Players;
                if (players.Ready) {
                    MultiplayerStatusOverlay.Close();
                    subscription.Cancel();
                }
                var readyPlayersCount = players.Count(it => it.State == PlayerState.Ready);
                var playerList = string.Join("\n", players.Select(it => $"{it.Profile.PlayerName}: {it.State}"));
                var statusText = $"Ожидание игроков (готово {readyPlayersCount}/{players.Count})...\n{playerList}";
                MultiplayerStatusOverlay.Text = statusText;
            }
        );
    }

    public void RequestWorldLoad(string name, byte[] data) {
        MultiplayerStatusOverlay.Show($"Загрузка {name}...");
        events.Subscribe<PlayersReadyEvent>(
            (_, subscription) => {
                MultiplayerStatusOverlay.Close();
                subscription.Cancel();
            }
        );
        scheduler.Run(() => LoadWorldSave(name, data));
    }

    private void LoadWorldSave(string name, byte[] data) {
        var savePath = SaveLoader.GetCloudSavesDefault()
            ? SaveLoader.GetCloudSavePrefix()
            : SaveLoader.GetSavePrefixAndCreateFolder();

        var path = SecurePath.Combine(savePath, name, $"{name}.sav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // FileMode.Create truncates: a shorter save must not keep the tail of the previous (longer) one
        using (var writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write)))
            writer.Write(data);

        executionLevelManager.BaseLevel = ExecutionLevel.Multiplayer;
        ModFootprintPatch.AllowForHostWorldLoad();
        MultiplayerMod.Multiplayer.World.Debug.WorldDebugSnapshotRunner.ResetErrors();
        LoadScreen.DoLoad(path);
    }

    private static string WorldName => Path.GetFileNameWithoutExtension(SaveLoader.GetActiveSaveFilePath());

    // Sends the given save file (the autosave made at the end of the cycle). If it is not available,
    // makes a fresh save right now and sends that instead.
    private static byte[] GetWorldSave(string? savePath) {
        if (!string.IsNullOrEmpty(savePath)) {
            var data = TryReadFile(savePath!);
            if (data != null)
                return data;
        }

        var path = SaveLoader.GetActiveSaveFilePath();
        Execution.RunUsingLevel(ExecutionLevel.Multiplayer, () => SaveLoader.Instance.Save(path));
        return File.ReadAllBytes(path);
    }

    private static byte[]? TryReadFile(string path) {
        for (var attempt = 0; attempt < 5; attempt++) {
            try {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var data = new byte[stream.Length];
                var read = 0;
                while (read < data.Length) {
                    var count = stream.Read(data, read, data.Length - read);
                    if (count == 0) break;
                    read += count;
                }
                if (read == data.Length && data.Length > 0)
                    return data;
            } catch (Exception) {
                // the file may still be locked by the game: retry
            }
            System.Threading.Thread.Sleep(100);
        }
        return null;
    }

}
