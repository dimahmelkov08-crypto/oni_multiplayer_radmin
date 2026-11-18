using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MultiplayerMod.Core.Logging;
using static MultiplayerMod.Platform.Tcp.Network.Configuration;

namespace MultiplayerMod.Platform.Tcp.Network.Messaging;

public class NetworkMessageProcessor {

    private readonly ConcurrentDictionary<uint, ConcurrentDictionary<int, FragmentsBuffer>> fragments = new();
    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<NetworkMessageProcessor>();

    public NetworkMessage? Process(uint clientId, INetworkMessageHandle handle) =>
        NetworkSerializer.Deserialize(handle) switch {
            NetworkMessage message => message,
            NetworkMessageFragmentsHeader header => ProcessFragmentsHeader(clientId, header),
            NetworkMessageFragment fragment => ProcessMessageFragment(clientId, fragment),
            _ => null
        };

    private NetworkMessage? ProcessFragmentsHeader(uint clientId, NetworkMessageFragmentsHeader header) {
        fragments.TryGetValue(clientId, out var index);
        if (index == null) {
            index = new ConcurrentDictionary<int, FragmentsBuffer>();
            fragments[clientId] = index;
        }
        var buffer = new FragmentsBuffer(header.FragmentsCount);
        buffer.Timeout += () => {
            log.Warning($"Fragments buffer timed out (message id: {header.MessageId})");
            index.TryRemove(header.MessageId, out _);
        };
        index[header.MessageId] = buffer;
        return null;
    }

    private NetworkMessage? ProcessMessageFragment(uint clientId, NetworkMessageFragment fragment) {
        string ExceptionMessage() =>
            $"Message (id: {fragment.MessageId}) fragment received, but no fragments buffer found";

        if (!fragments.TryGetValue(clientId, out var index)) {
            log.Warning(ExceptionMessage());
            return null;
        }

        if (!index.TryGetValue(fragment.MessageId, out var buffer)) {
            log.Warning(ExceptionMessage());
            return null;
        }

        var message = buffer.Append(fragment);
        if (message != null)
            index.TryRemove(fragment.MessageId, out _);

        return message;
    }

    private class FragmentsBuffer {
        private const int watchdogIntervalMs = 60000; // a world save over Radmin/VPN can take well over 5 s

        private int index;
        private readonly int count;
        private readonly byte[] buffer;

        public event System.Action? Timeout;

        private readonly System.Timers.Timer watchdog = new(watchdogIntervalMs) {
            Enabled = true,
            AutoReset = false
        };

        public FragmentsBuffer(int count) {
            this.count = count;
            buffer = new byte[count * MaxFragmentDataSize];
            watchdog.Elapsed += (_, _) => {
                watchdog.Dispose();
                Timeout?.Invoke();
            };
        }

        public NetworkMessage? Append(NetworkMessageFragment fragment) {
            if (index >= count)
                throw new NetworkPlatformException("Invalid fragmentation: more fragments than expected.");

            try {
                watchdog.Interval = watchdogIntervalMs; // restart the timeout on every received fragment
            } catch (ObjectDisposedException) {
                throw new NetworkPlatformException("Fragments buffer already timed out.");
            }
            Buffer.BlockCopy(fragment.Data, 0, buffer, index * MaxFragmentDataSize, fragment.Data.Length);
            if (++index != count)
                return null;

            watchdog.Dispose();
            using var stream = new MemoryStream(buffer);
            return (NetworkMessage) new BinaryFormatter().Deserialize(stream);
        }
    }

}
