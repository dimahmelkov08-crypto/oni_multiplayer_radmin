using MultiplayerMod.Core.Events;
using MultiplayerMod.ModRuntime.StaticCompatibility;
using MultiplayerMod.Multiplayer.Players.Events;
using MultiplayerMod.Platform.Tcp.Network;
using UnityEngine;

namespace MultiplayerMod.Platform.Tcp.UI;

/// <summary>Minimal IMGUI dialog asking for the host address.</summary>
public class JoinDialog : MonoBehaviour {

    private const string prefsKey = "MultiplayerMod.Radmin.LastAddress";

    private Rect window = new(0, 0, 380, 130);
    private string address = "";
    private string error = "";

    private void Awake() {
        address = PlayerPrefs.GetString(prefsKey, "26.0.0.1:" + Configuration.DefaultPort);
        window.x = (Screen.width - window.width) / 2;
        window.y = (Screen.height - window.height) / 2;
    }

    private void OnGUI() {
        window = GUI.Window(0x4F4E49, window, Draw, "Join multiplayer (host IP:port, e.g. Radmin address)");
    }

    private void Draw(int id) {
        GUILayout.Label("Host address:");
        address = GUILayout.TextField(address);
        if (error.Length > 0)
            GUILayout.Label(error);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Connect"))
            Connect();
        if (GUILayout.Button("Cancel"))
            Destroy(gameObject);
        GUILayout.EndHorizontal();
        GUI.DragWindow();
    }

    private void Connect() {
        if (!TryParse(address.Trim(), out var host, out var port)) {
            error = "Enter an address like 26.1.2.3:7777";
            return;
        }
        PlayerPrefs.SetString(prefsKey, address.Trim());
        var endpoint = new TcpEndpoint(host, port);
        Dependencies.Get<EventDispatcher>().Dispatch(new MultiplayerJoinRequestedEvent(endpoint, host));
        Destroy(gameObject);
    }

    private static bool TryParse(string text, out string host, out int port) {
        host = text;
        port = Configuration.DefaultPort;
        if (text.Length == 0)
            return false;

        var colon = text.LastIndexOf(':');
        if (colon < 0)
            return true;

        host = text.Substring(0, colon);
        return host.Length > 0 && int.TryParse(text.Substring(colon + 1), out port) && port is > 0 and < 65536;
    }

}
