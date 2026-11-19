using HarmonyLib;

namespace MultiplayerMod.Game.World;

/// <summary>Raised after the game has written an autosave (path of the written file).</summary>
[HarmonyPatch(typeof(SaveLoader))]
public static class AutoSaveEvents {

    public static event System.Action<string>? AutoSaved;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SaveLoader.Save), typeof(string), typeof(bool), typeof(bool))]
    // ReSharper disable once UnusedMember.Global
    public static void SavePostfix(string filename, bool isAutoSave) {
        if (isAutoSave)
            AutoSaved?.Invoke(filename);
    }

}
