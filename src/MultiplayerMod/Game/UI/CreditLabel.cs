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
using TMPro;
using UnityEngine;

namespace MultiplayerMod.Game.UI;

[HarmonyPatch(typeof(MainMenu), "OnSpawn")]
public static class CreditLabelPatch {

    // ReSharper disable once UnusedMember.Global
    public static void Postfix() => CreditLabel.Ensure();

}

/// <summary>
/// Persistent on-screen HUD: version (bottom center), ping to the host (bottom right)
/// and a desync warning (top center) shown while host and client worlds differ.
/// </summary>
public static class CreditLabel {

    private const string Version = "IM_4ORTTICK  |  версия 2.0";

    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger(typeof(CreditLabel));
    private static GameObject? root;

    public static void Ensure() {
        try {
            if (root != null) return;

            root = new GameObject("MultiplayerHud");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            CreateLabel(
                "Version", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 4), 16,
                TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f)
            ).text = Version;
            var ping = CreateLabel(
                "Ping", new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 4), 16,
                TextAlignmentOptions.Right, Color.white
            );
            var desync = CreateLabel(
                "Desync", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8), 26,
                TextAlignmentOptions.Center, new Color(1f, 0.25f, 0.25f, 1f)
            );

            root.AddComponent<MultiplayerHud>().Init(ping, desync);
        } catch (Exception exception) {
            log.Warning($"HUD failed: {exception.Message}");
        }
    }

    private static TextMeshProUGUI CreateLabel(
        string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, float size,
        TextAlignmentOptions alignment, Color color
    ) {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(root!.transform, false);
        var rect = (RectTransform) textObject.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(0, size + 8);

        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = Localization.FontAsset ?? TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.text = "";
        return text;
    }

}

public class MultiplayerHud : MonoBehaviour {

    private const float NoReplySeconds = 5f;

    private TextMeshProUGUI ping = null!;
    private TextMeshProUGUI desync = null!;
    private float nextPing;
    private float nextRefresh;

    public void Init(TextMeshProUGUI pingLabel, TextMeshProUGUI desyncLabel) {
        ping = pingLabel;
        desync = desyncLabel;
    }

    private void Update() {
        try {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;

            var client = Dependencies.Get<IMultiplayerClient>();
            var connected = client.State == MultiplayerClientState.Connected;
            var isClient = Dependencies.Get<MultiplayerGame>().Mode == MultiplayerMode.Client;

            if (connected && isClient) {
                if (Time.unscaledTime >= nextPing) {
                    nextPing = Time.unscaledTime + 1f;
                    client.Send(new PingRequest(Stopwatch.GetTimestamp()));
                }
                ShowPing();
            } else {
                ping.text = "";
                PingState.Reset();
            }

            var errors = WorldDebugSnapshotRunner.ErrorsCount;
            desync.text = connected && errors > 0
                ? $"РАССИНХРОН! Отличий: {errors}. Мир выровняется при ресинке в конце цикла"
                : "";
        } catch (Exception) {
            // HUD must never affect the game
        }
    }

    private void ShowPing() {
        if (Time.realtimeSinceStartup - PingState.LastReplyTime > NoReplySeconds) {
            ping.color = new Color(1f, 0.3f, 0.3f);
            ping.text = "Пинг: нет ответа";
            return;
        }

        var ms = PingState.LastMs;
        ping.color = ms < 80 ? new Color(0.4f, 1f, 0.4f)
            : ms < 160 ? new Color(1f, 0.85f, 0.3f)
            : new Color(1f, 0.3f, 0.3f);
        ping.text = $"Пинг: {ms:0} мс";
    }

}
