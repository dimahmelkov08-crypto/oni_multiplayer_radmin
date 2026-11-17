using System;
using MultiplayerMod.Platform.Tcp.Network.Messaging;

namespace MultiplayerMod.Platform.Tcp.Network;

public static class Configuration {

    public const int DefaultPort = 7777;

    // A single framed message is never bigger than this; bigger commands are fragmented by NetworkMessageFactory.
    public const int MaxMessageSize = 524288; // 512 KiB

    // Hard limit for a received frame length (protection against garbage / hostile length prefixes).
    public const int MaxFrameSize = 4 * 1024 * 1024; // 4 MiB

    public static readonly int MaxFragmentDataSize = GetFragmentDataSize();

    private static int GetFragmentDataSize() {
        using var serialized = NetworkSerializer.Serialize(new NetworkMessageFragment(0, Array.Empty<byte>()));
        return MaxMessageSize - (int) serialized.Size;
    }

}
