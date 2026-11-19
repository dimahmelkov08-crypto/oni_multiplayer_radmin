using System;
using System.Net.Sockets;
using System.Threading;
using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Extensions;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer.Commands;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.Tcp.Network.Components;
using MultiplayerMod.Platform.Tcp.Network.Messaging;
using UnityEngine;

namespace MultiplayerMod.Platform.Tcp.Network;

[Dependency, UsedImplicitly]
public class TcpMultiplayerClient : IMultiplayerClient {

    private const int connectTimeoutMs = 10000;

    public IMultiplayerClientId Id => TcpMultiplayerClientId.Local;
    public MultiplayerClientState State { get; private set; } = MultiplayerClientState.Disconnected;

    /// <summary>True after an established connection to the host was lost (until the next connect).</summary>
    public static bool ConnectionLost { get; private set; }

    public event Action<MultiplayerClientState>? StateChanged;
    public event Action<IMultiplayerCommand>? CommandReceived;

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<TcpMultiplayerClient>();
    private readonly NetworkMessageProcessor messageProcessor = new();
    private readonly NetworkMessageFactory messageFactory = new();

    private TcpConnection? connection;
    private GameObject? gameObject;

    // Written by the connect thread, read by Tick()
    private volatile TcpClient? connectedSocket;
    private volatile string? connectError;
    private int connectAttempt;
    private bool connectionWasLost;
    private bool droppedWarningShown;

    public void Connect(IMultiplayerEndpoint endpoint) {
        var tcpEndpoint = (TcpEndpoint) endpoint;
        if (State is MultiplayerClientState.Connecting or MultiplayerClientState.Connected)
            ReleaseResources();

        connectedSocket = null;
        connectError = null;
        connectionWasLost = false;
        ConnectionLost = false;
        droppedWarningShown = false;
        var attempt = Interlocked.Increment(ref connectAttempt);
        SetState(MultiplayerClientState.Connecting);
        log.Info($"Connecting to {tcpEndpoint.Host}:{tcpEndpoint.Port}");

        gameObject = UnityObject.CreateStaticWithComponent<TcpClientComponent>();
        new Thread(() => ConnectWorker(tcpEndpoint, attempt)) { IsBackground = true, Name = "MP-TCP-Connect" }.Start();
    }

    public void Disconnect() {
        if (State == MultiplayerClientState.Disconnected)
            throw new NetworkPlatformException("Client not connected");

        ReleaseResources();
        SetState(MultiplayerClientState.Disconnected);
    }

    public void Tick() {
        switch (State) {
            case MultiplayerClientState.Connecting:
                TickConnecting();
                break;
            case MultiplayerClientState.Connected:
                TickConnected();
                break;
        }
    }

    public void Send(IMultiplayerCommand command, MultiplayerCommandOptions options = MultiplayerCommandOptions.None) {
        if (State == MultiplayerClientState.Disconnected && connectionWasLost) {
            // Connection dropped: game events (mouse, pause...) keep firing, drop them instead of throwing every frame
            if (!droppedWarningShown) {
                droppedWarningShown = true;
                log.Warning("Not connected to the host, outgoing commands are being dropped");
            }
            return;
        }

        if (State != MultiplayerClientState.Connected || connection == null)
            throw new NetworkPlatformException("Client not connected");

        messageFactory.Create(command, options).ForEach(handle => connection.Send(handle));
    }

    private void ConnectWorker(TcpEndpoint endpoint, int attempt) {
        TcpClient? socket = null;
        try {
            socket = new TcpClient();
            var result = socket.BeginConnect(endpoint.Host, endpoint.Port, null, null);
            if (!result.AsyncWaitHandle.WaitOne(connectTimeoutMs))
                throw new TimeoutException("Connection timed out");
            socket.EndConnect(result);
            if (attempt != connectAttempt) {
                socket.Close();
                return;
            }
            connectedSocket = socket;
        } catch (Exception e) {
            try {
                socket?.Close();
            } catch (Exception) { }
            if (attempt == connectAttempt)
                connectError = e.Message;
        }
    }

    private void TickConnecting() {
        var error = connectError;
        if (error != null) {
            log.Error($"Connection failed: {error}");
            ReleaseResources();
            SetState(MultiplayerClientState.Error);
            return;
        }

        var socket = connectedSocket;
        if (socket == null)
            return;

        connectedSocket = null;
        connection = new TcpConnection(socket, expectHandshake: false);
        connection.SendRaw(TcpMultiplayerClientId.Local.Id.ToByteArray());
        SetState(MultiplayerClientState.Connected);
    }

    private void TickConnected() {
        if (connection == null)
            return;

        while (connection.Received.TryDequeue(out var frame)) {
            NetworkMessage? message;
            try {
                using var handle = new PinnedMessageHandle(frame);
                message = messageProcessor.Process(connection.Id, handle);
            } catch (Exception e) {
                log.Error($"Unable to process message: {e}");
                continue;
            }
            if (message != null)
                CommandReceived?.Invoke(message.Command);
        }

        if (connection.Closed) {
            log.Warning("Connection to the host was lost");
            connectionWasLost = true;
            ConnectionLost = true;
            ReleaseResources();
            SetState(MultiplayerClientState.Disconnected);
        }
    }

    private void ReleaseResources() {
        Interlocked.Increment(ref connectAttempt);
        if (gameObject != null) {
            UnityObject.Destroy(gameObject);
            gameObject = null;
        }
        connection?.Dispose();
        connection = null;
        try {
            connectedSocket?.Close();
        } catch (Exception) { }
        connectedSocket = null;
    }

    private void SetState(MultiplayerClientState state) {
        State = state;
        StateChanged?.Invoke(state);
    }

}
