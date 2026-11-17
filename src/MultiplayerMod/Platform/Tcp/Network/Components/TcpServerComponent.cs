using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Unity;
using UnityEngine;

// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace MultiplayerMod.Platform.Tcp.Network.Components;

public class TcpServerComponent : MultiplayerMonoBehaviour {

    [InjectDependency]
    private TcpMultiplayerServer server = null!;

    private string addresses = "";

    protected override void Awake() {
        base.Awake();
        addresses = GetAddresses();
    }

    private void Update() => server.Tick();

    // Small always-visible hint for the host: which address should friends use.
    private void OnGUI() {
        var text = $"Hosting on port {server.Port}. Friends connect to: {addresses}";
        var width = 620f;
        GUI.Box(new Rect((Screen.width - width) / 2, 2, width, 22), text);
    }

    private static string GetAddresses() {
        var radmin = new List<string>();
        var others = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            var isRadmin = (nic.Name + nic.Description).ToLowerInvariant().Contains("radmin");
            foreach (var address in nic.GetIPProperties().UnicastAddresses) {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                (isRadmin ? radmin : others).Add(address.Address.ToString());
            }
        }
        var all = radmin.Select(it => it + " (Radmin)").Concat(others).ToList();
        return all.Count == 0 ? "unknown" : string.Join(", ", all);
    }

}
