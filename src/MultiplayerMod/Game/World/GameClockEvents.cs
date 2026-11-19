using HarmonyLib;

namespace MultiplayerMod.Game.World;

[HarmonyPatch(typeof(GameClock))]
public static class GameClockEvents {

    private static int lastCycle = -1;

    /// <summary>Raised (on the machine where the clock runs) when the colony cycle number changes.</summary>
    public static event System.Action<int>? CycleChanged;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameClock.Render1000ms))]
    // ReSharper disable once UnusedMember.Global
    public static void Render1000msPostfix(GameClock __instance) {
        var cycle = __instance.GetCycle();
        if (cycle == lastCycle)
            return;
        lastCycle = cycle;
        CycleChanged?.Invoke(cycle);
    }

}
