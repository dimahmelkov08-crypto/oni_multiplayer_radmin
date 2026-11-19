using System;
using System.Diagnostics;
using HarmonyLib;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.ModRuntime.StaticCompatibility;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Multiplayer.Commands.Ping;
using MultiplayerMod.Multiplayer.Ping;
using MultiplayerMod.Multiplayer.World.Debug;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.Tcp.Network;
using UnityEngine;

namespace MultiplayerMod.Game.UI;

[HarmonyPatch(typeof(MainMenu), "OnSpawn")]
public static class CreditLabelPatch {

    // ReSharper disable once UnusedMember.Global
    public static void Postfix() => CreditLabel.Ensure();

}

/// <summary>
/// Persistent on-screen HUD drawn with IMGUI (independent of the game's fonts and canvases):
/// author and version (bottom center), ping to the host (bottom right),
/// connection-lost and desync warnings (top center).
/// </summary>
public static class CreditLabel {

    internal const string Version = "IM_4ORTTICK  |  версия 2.0";

    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger(typeof(CreditLabel));
    private static GameObject? root;

    public static void Ensure() {
        try {
            if (root != null) return;

            root = new GameObject("MultiplayerHud");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.AddComponent<MultiplayerHud>();
            log.Info("HUD created");
        } catch (Exception exception) {
            log.Warning($"HUD failed: {exception.Message}");
        }
    }

}

public class MultiplayerHud : MonoBehaviour {

    private const float NoReplySeconds = 5f;
    private const float HelloWaitSeconds = 6f;

    private string pingText = "";
    private Color pingColor = Color.white;
    private string warningText = "";

    private float nextPing;
    private float nextRefresh;
    private float connectedSince = -1f;

    private GUIStyle? style;

    private void Update() {
        try {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        } catch (Exception) {
            // HUD must never affect the game
        }
    }

    private void Refresh() {
        var client = Dependencies.Get<IMultiplayerClient>();
        var connected = client.State == MultiplayerClientState.Connected;
        var isClient = Dependencies.Get<MultiplayerGame>().Mode == MultiplayerMode.Client;

        pingText = "";
        warningText = "";

        if (!connected) {
            connectedSince = -1f;
            PingState.Reset();
            if (TcpMultiplayerClient.ConnectionLost)
                warningText = "СОЕДИНЕНИЕ С ХОСТОМ ПОТЕРЯНО! Игра больше не синхронизируется. Выйдите и подключитесь заново";
            return;
        }

        if (connectedSince < 0f)
            connectedSince = Time.unscaledTime;

        var errors = WorldDebugSnapshotRunner.ErrorsCount;
        if (errors > 0)
            warningText = $"РАССИНХРОН! Отличий: {errors}. Мир выровняется при ресинке в конце цикла";

        if (!isClient)
            return;

        if (!PingState.HostSupportsPing) {
            if (Time.unscaledTime - connectedSince > HelloWaitSeconds) {
                pingColor = new Color(1f, 0.85f, 0.3f);
                pingText = "Пинг недоступен: у хоста старая версия мода";
            }
            return;
        }

        if (Time.unscaledTime >= nextPing) {
            nextPing = Time.unscaledTime + 1f;
            client.Send(new PingRequest(Stopwatch.GetTimestamp()));
        }

        if (Time.realtimeSinceStartup - PingState.LastReplyTime > NoReplySeconds) {
            pingColor = new Color(1f, 0.3f, 0.3f);
            pingText = "Пинг: нет ответа";
            return;
        }

        var ms = PingState.LastMs;
        pingColor = ms < 80 ? new Color(0.4f, 1f, 0.4f)
            : ms < 160 ? new Color(1f, 0.85f, 0.3f)
            : new Color(1f, 0.3f, 0.3f);
        pingText = $"Пинг: {ms:0} мс";
    }

    private void OnGUI() {
        try {
            if (Event.current.type != EventType.Repaint) return;

            style ??= new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false };
            var size = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 62f));
            style.fontSize = size;
            var height = size * 1.6f;

            style.alignment = TextAnchor.MiddleCenter;
            Draw(new Rect(0, Screen.height - height - 2, Screen.width, height), CreditLabel.Version,
                new Color(1f, 1f, 1f, 0.8f));

            if (pingText.Length > 0) {
                style.alignment = TextAnchor.MiddleRight;
                Draw(new Rect(0, Screen.height - height * 2 - 2, Screen.width - 14, height), pingText, pingColor);
            }

            if (warningText.Length > 0) {
                style.fontSize = Mathf.RoundToInt(size * 1.5f);
                style.alignment = TextAnchor.MiddleCenter;
                Draw(new Rect(0, 8, Screen.width, size * 2.6f), warningText, new Color(1f, 0.25f, 0.25f));
            }
        } catch (Exception) {
            // HUD must never affect the game
        }
    }

    private void Draw(Rect rect, string text, Color color) {
        var shadow = new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height);
        style!.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(shadow, text, style);
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }

}
