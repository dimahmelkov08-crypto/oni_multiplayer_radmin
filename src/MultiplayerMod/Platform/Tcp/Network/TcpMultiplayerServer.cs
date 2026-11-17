using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Extensions;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer.Commands;
using MultiplayerMod.Multiplayer.Commands.Registry;
using MultiplayerMod.Network;
using MultiplayerMod.Platform.Tcp.Network.Components;
using MultiplayerMod.Platform.Tcp.Network.Messaging;
using UnityEngine;

namespace MultiplayerMod.Platform.Tcp.Network;

[Dependency, UsedImplicitly]
public class TcpMultiplayerServer : IMultiplayerServer {

    public MultiplayerServerState State { private set; get; } = MultiplayerServerState.Stopped;

    public IMultiplayerEndpoint Endpoint {
        get {
            if (State != MultiplayerServerState.Started)
                throw new NetworkPlatformException("Server isn't started");

            return new TcpEndpoint("127.0.0.1", Port);
        }
    }

    public int Port { get; private set; } = Configuration.DefaultPort;

    public List<IMultiplayerClientId> Clients => new(clients.Keys);

    public event Action<MultiplayerServerState>? StateChanged;
    public event Action<IMultiplayerClientId>? ClientConnected;
    public event Action<IMultiplayerClientId>? ClientDisconnected;
    public event Action<IMultiplayerClientId, IMultiplayerCommand>? CommandReceived;

    private readonly Core.Logging.Logger log = LoggerFactory.GetLogger<TcpMultiplayerServer>();
    private readonly MultiplayerCommandRegistry commands;
    private readonly NetworkMessageProcessor messageProcessor = new();
    private readonly NetworkMessageFactory messageFactory = new();

    private readonly Dictionary<IMultiplayerClientId, TcpConnection> clients = new();
    private readonly List<TcpConnection> pending = new();
    private readonly ConcurrentQueue<TcpConnection> accepted = new();
    private readonly IMultiplayerClientId currentPlayer = TcpMultiplayerClientId.Local;

    private TcpListener? listener;
    private GameObject? gameObject;
    private volatile bool running;

    public TcpMultiplayerServer(MultiplayerCommandRegistry commands) {
        this.commands = commands;
    }

    public void Start() {
        log.Debug("Starting...");
        SetState(MultiplayerServerState.Preparing);
        try {
            listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
        } catch (Exception e) {
            SetState(MultiplayerServerState.Error);
            throw new NetworkPlatformException($"Unable to listen on port {Port}: {e.Message}", e);
        }

        running = true;
        new Thread(AcceptLoop) { IsBackground = true, Name = "MP-TCP-Accept" }.Start();
        gameObject = UnityObject.CreateStaticWithComponent<TcpServerComponent>();
        SetState(MultiplayerServerState.Starting);
        log.Info($"Listening on port {Port}");
        SetState(MultiplayerServerState.Started);
    }

    public void Stop() {
        if (State <= MultiplayerServerState.Stopped)
            throw new NetworkPlatformException("Server isn't started");

        log.Debug("Stopping...");
        running = false;
        if (gameObject != null)
            UnityObject.Destroy(gameObject);
        try {
            listener?.Stop();
        } catch (Exception) { }
        pending.ForEach(it => it.Dispose());
        pending.Clear();
        clients.Values.ForEach(it => it.Dispose());
        clients.Clear();
        while (accepted.TryDequeue(out var connection))
            connection.Dispose();
        SetState(MultiplayerServerState.Stopped);
    }

    public void Tick() {
        if (State != MultiplayerServerState.Started)
            return;

        AcceptPending();
        ReceiveMessages();
        RemoveClosed();
    }

    public void Send(IMultiplayerClientId clientId, IMultiplayerCommand command) =>
        SendCommand(command, MultiplayerCommandOptions.None, new[] { clients[clientId] });

    public void Send(IMultiplayerCommand command, MultiplayerCommandOptions options = MultiplayerCommandOptions.None) {
        IEnumerable<KeyValuePair<IMultiplayerClientId, TcpConnection>> recipients = clients;
        if (options.HasFlag(MultiplayerCommandOptions.SkipHost))
            recipients = recipients.Where(entry => !entry.Key.Equals(currentPlayer));

        SendCommand(command, options, recipients.Select(it => it.Value));
    }

    private void SetState(MultiplayerServerState state) {
        State = state;
        StateChanged?.Invoke(state);
    }

    private void AcceptLoop() {
        while (running) {
            try {
                var socket = listener!.AcceptTcpClient();
                accepted.Enqueue(new TcpConnection(socket, expectHandshake: true));
            } catch (Exception) {
                if (!running)
                    break;
            }
        }
    }

    private void AcceptPending() {
        while (accepted.TryDequeue(out var connection))
            pending.Add(connection);

        foreach (var connection in pending.ToList()) {
            if (connection.Closed) {
                pending.Remove(connection);
                continue;
            }
            if (connection.PeerId == null)
                continue;

            pending.Remove(connection);
            IMultiplayerClientId id = new TcpMultiplayerClientId(connection.PeerId.Value);
            if (clients.TryGetValue(id, out var previous)) {
                previous.Dispose();
                clients.Remove(id);
            }
            clients[id] = connection;
            log.Debug($"Connection accepted from {id}");
            ClientConnected?.Invoke(id);
        }
    }

    private void ReceiveMessages() {
        foreach (var entry in clients.ToList()) {
            var id = entry.Key;
            var connection = entry.Value;
            while (connection.Received.TryDequeue(out var frame)) {
                NetworkMessage? message;
                try {
                    using var handle = new PinnedMessageHandle(frame);
                    message = messageProcessor.Process(connection.Id, handle);
                } catch (Exception e) {
                    log.Error($"Unable to process message from {id}: {e}");
                    connection.Dispose();
                    break;
                }
                if (message == null)
                    continue;

                var configuration = commands.GetCommandConfiguration(message.Command.GetType());
                if (configuration.ExecuteOnServer) {
                    CommandReceived?.Invoke(id, message.Command);
                } else {
                    var others = clients.Where(it => !it.Key.Equals(id)).Select(it => it.Value);
                    SendCommand(message.Command, message.Options, others);
                }
            }
        }
    }

    private void RemoveClosed() {
        foreach (var entry in clients.Where(it => it.Value.Closed).ToList()) {
            clients.Remove(entry.Key);
            log.Debug($"Connection closed for {entry.Key}");
            ClientDisconnected?.Invoke(entry.Key);
        }
    }

    private void SendCommand(
        IMultiplayerCommand command,
        MultiplayerCommandOptions options,
        IEnumerable<TcpConnection> connections
    ) {
        var recipients = connections.ToList();
        messageFactory.Create(command, options).ForEach(handle => recipients.ForEach(it => it.Send(handle)));
    }

}
