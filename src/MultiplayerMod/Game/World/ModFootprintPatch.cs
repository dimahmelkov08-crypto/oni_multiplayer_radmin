using HarmonyLib;
using UnityEngine;

namespace MultiplayerMod.Game.World;

/// <summary>
/// The host's save carries the host's mod footprint (mod list/versions). A client loading the world received from
/// the host would otherwise get the game's "mods differ" dialog. While a host world is being loaded, accept it.
/// </summary>
[HarmonyPatch(typeof(KMod.Manager), "MatchFootprint")]
public static class ModFootprintPatch {

    private const float windowSeconds = 120f;
    private static float acceptUntil;

    public static void AllowForHostWorldLoad() => acceptUntil = Time.realtimeSinceStartup + windowSeconds;

    [HarmonyPrefix]
    // ReSharper disable once UnusedMember.Global
    public static bool Prefix(ref bool __result) {
        if (Time.realtimeSinceStartup > acceptUntil)
            return true;
        __result = true;
        return false;
    }

}
