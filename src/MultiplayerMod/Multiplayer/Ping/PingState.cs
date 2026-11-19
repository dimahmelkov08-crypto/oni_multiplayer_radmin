using System.Diagnostics;
using UnityEngine;

namespace MultiplayerMod.Multiplayer.Ping;

/// <summary>Latest round-trip time to the host, measured by PingRequest/PongResponse.</summary>
public static class PingState {

    public static float LastMs { get; private set; }
    public static float LastReplyTime { get; private set; } = -1000f;

    /// <summary>True once the host has said hello, i.e. runs a mod version that answers pings.</summary>
    public static bool HostSupportsPing { get; set; }

    public static void Report(long sentTimestamp) {
        LastMs = (float) ((Stopwatch.GetTimestamp() - sentTimestamp) * 1000.0 / Stopwatch.Frequency);
        LastReplyTime = Time.realtimeSinceStartup;
    }

    public static void Reset() {
        LastReplyTime = -1000f;
        HostSupportsPing = false;
    }

}
