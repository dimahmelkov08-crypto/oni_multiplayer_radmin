using System;
using HarmonyLib;
using MultiplayerMod.Core.Logging;
using TMPro;
using UnityEngine;

namespace MultiplayerMod.Game.UI;

/// <summary>Persistent version line at the bottom of the screen.</summary>
[HarmonyPatch(typeof(MainMenu), "OnSpawn")]
public static class CreditLabelPatch {

    // ReSharper disable once UnusedMember.Global
    public static void Postfix() => CreditLabel.Ensure();

}

public static class CreditLabel {

    private const string Text = "Версия 1.9AAB";

    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger(typeof(CreditLabel));
    private static GameObject? root;

    public static void Ensure() {
        try {
            if (root != null) return;

            root = new GameObject("MultiplayerCreditLabel");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            var textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform) textObject.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 4);
            rect.sizeDelta = new Vector2(0, 24);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = Localization.FontAsset;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 1f, 1f, 0.75f);
            text.raycastTarget = false;
            text.text = Text;
        } catch (Exception exception) {
            log.Warning($"Version label failed: {exception.Message}");
        }
    }

}
