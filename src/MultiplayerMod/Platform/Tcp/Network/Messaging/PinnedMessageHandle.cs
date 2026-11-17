using System;
using System.Runtime.InteropServices;

namespace MultiplayerMod.Platform.Tcp.Network.Messaging;

/// <summary>Exposes a received byte array as an unmanaged message handle.</summary>
public class PinnedMessageHandle : INetworkMessageHandle {

    public IntPtr Pointer { get; }
    public uint Size { get; }

    private GCHandle handle;

    public PinnedMessageHandle(byte[] data) {
        handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        Pointer = handle.AddrOfPinnedObject();
        Size = (uint) data.Length;
    }

    public void Dispose() => handle.Free();

}
