using HarmonyLib;
using UnityEngine;

namespace MultiplayerMod.Game.World;

/// <summary>Remembers the last autosave the game has written (path and real time).</summary>
[HarmonyPatch(typeof(SaveLoader))]
public static class AutoSaveEvents {

    public static string? LastPath { get; private set; }
    public static float LastTime { get; private set; } = -1000f;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SaveLoader.Save), typeof(string), typeof(bool), typeof(bool))]
    // ReSharper disable once UnusedMember.Global
    public static void SavePostfix(string filename, bool isAutoSave) {
        if (!isAutoSave)
            return;

        LastPath = filename;
        LastTime = Time.realtimeSinceStartup;
    }

}
