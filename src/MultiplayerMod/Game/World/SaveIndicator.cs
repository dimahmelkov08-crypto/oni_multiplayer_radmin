using System;
using HarmonyLib;
using MultiplayerMod.Core.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerMod.Game.World;

/// <summary>
/// Small "saving" badge in the top-right corner. Shown while SaveLoader.Save runs and for a moment afterwards.
/// Never throws: a broken indicator must not affect saving or the game.
/// </summary>
[HarmonyPatch(typeof(SaveLoader), nameof(SaveLoader.Save), typeof(string), typeof(bool), typeof(bool))]
public static class SaveIndicatorPatch {

    // ReSharper disable once UnusedMember.Global
    public static void Prefix() => SaveIndicator.Show(true);

    // ReSharper disable once UnusedMember.Global
    public static void Postfix() => SaveIndicator.Show(false);

    // ReSharper disable once UnusedMember.Global
    public static Exception? Finalizer(Exception? __exception) {
        if (__exception != null) SaveIndicator.Hide();
        return __exception;
    }

}

public class SaveIndicator : MonoBehaviour {

    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<SaveIndicator>();
    private const float DoneDisplaySeconds = 2.5f;

    private static SaveIndicator? instance;

    private GameObject panel = null!;
    private TextMeshProUGUI label = null!;
    private float hideAt;

    public static void Show(bool saving) {
        try {
            Ensure();
            if (instance == null) return;
            instance.label.text = saving ? "↓ Сохранение..." : "✓ Игра сохранена";
            instance.hideAt = saving ? float.MaxValue : Time.unscaledTime + DoneDisplaySeconds;
            instance.panel.SetActive(true);
            if (saving) Canvas.ForceUpdateCanvases();
        } catch (Exception exception) {
            log.Warning($"Save indicator failed: {exception.Message}");
        }
    }

    public static void Hide() {
        try {
            if (instance != null) instance.panel.SetActive(false);
        } catch (Exception) {
            // ignored
        }
    }

    private static void Ensure() {
        if (instance != null) return;

        var root = new GameObject("MultiplayerSaveIndicator");
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var self = root.AddComponent<SaveIndicator>();

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var rect = (RectTransform) panel.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-20, -60);
        rect.sizeDelta = new Vector2(240, 36);
        var background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);
        var textRect = (RectTransform) textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = Localization.FontAsset;
        text.fontSize = 18;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        self.panel = panel;
        self.label = text;
        panel.SetActive(false);
        instance = self;
    }

    private void Update() {
        if (panel.activeSelf && Time.unscaledTime >= hideAt) panel.SetActive(false);
    }

}
