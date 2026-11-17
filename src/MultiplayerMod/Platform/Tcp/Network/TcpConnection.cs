using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using MultiplayerMod.Platform.Tcp.Network.Messaging;

namespace MultiplayerMod.Platform.Tcp.Network;

/// <summary>
/// A framed TCP connection: [int32 length][payload]. Reading and writing happen on background threads;
/// received frames are queued and consumed by the game thread.
/// </summary>
public sealed class TcpConnection : IDisposable {

    private static int nextId;

    public uint Id { get; } = (uint) Interlocked.Increment(ref nextId);
    public ConcurrentQueue<byte[]> Received { get; } = new();
    public bool Closed => closed;

    /// <summary>Identity announced by the peer in the first frame (server side only).</summary>
    public Guid? PeerId => peerId;

    private readonly TcpClient socket;
    private readonly NetworkStream stream;
    private readonly bool expectHandshake;
    private readonly BlockingCollection<byte[]> sendQueue = new();
    private volatile bool closed;
    private volatile object? peerIdBox;
    private Guid? peerId => peerIdBox as Guid?;

    public TcpConnection(TcpClient socket, bool expectHandshake) {
        this.socket = socket;
        this.expectHandshake = expectHandshake;
        socket.NoDelay = true;
        stream = socket.GetStream();
        new Thread(ReadLoop) { IsBackground = true, Name = "MP-TCP-Read" }.Start();
        new Thread(WriteLoop) { IsBackground = true, Name = "MP-TCP-Write" }.Start();
    }

    public void SendRaw(byte[] payload) {
        if (closed)
            return;
        var frame = new byte[4 + payload.Length];
        BitConverter.GetBytes(payload.Length).CopyTo(frame, 0);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
        try {
            sendQueue.Add(frame);
        } catch (InvalidOperationException) {
            // Queue completed: connection is closing
        }
    }

    public void Send(INetworkMessageHandle handle) {
        var data = new byte[handle.Size];
        Marshal.Copy(handle.Pointer, data, 0, (int) handle.Size);
        SendRaw(data);
    }

    public void Dispose() => Close();

    private void ReadLoop() {
        var lengthBuffer = new byte[4];
        try {
            while (!closed) {
                ReadExact(lengthBuffer, 4);
                var length = BitConverter.ToInt32(lengthBuffer, 0);
                if (length <= 0 || length > Configuration.MaxFrameSize)
                    break;

                var data = new byte[length];
                ReadExact(data, length);

                if (expectHandshake && peerIdBox == null) {
                    if (length != 16)
                        break;
                    peerIdBox = new Guid(data);
                    continue;
                }
                Received.Enqueue(data);
            }
        } catch (Exception) {
            // Connection lost
        } finally {
            Close();
        }
    }

    private void ReadExact(byte[] buffer, int count) {
        var offset = 0;
        while (offset < count) {
            var read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
                throw new System.IO.EndOfStreamException();
            offset += read;
        }
    }

    private void WriteLoop() {
        try {
            foreach (var frame in sendQueue.GetConsumingEnumerable())
                stream.Write(frame, 0, frame.Length);
        } catch (Exception) {
            // Connection lost
        } finally {
            Close();
        }
    }

    private void Close() {
        if (closed)
            return;
        closed = true;
        try {
            sendQueue.CompleteAdding();
        } catch (ObjectDisposedException) { }
        try {
            socket.Close();
        } catch (Exception) { }
    }

}
