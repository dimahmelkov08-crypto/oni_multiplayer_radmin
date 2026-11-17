using JetBrains.Annotations;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Events;
using MultiplayerMod.Multiplayer.CoreOperations.Events;

namespace MultiplayerMod.Multiplayer.UI;

[Dependency, UsedImplicitly]
public class Notifications {

    public Notifications(EventDispatcher events) {
        events.Subscribe<ConnectionLostEvent>(OnConnectionLost);
    }

    private void OnConnectionLost(ConnectionLostEvent @event) {
        var screen = (InfoDialogScreen) GameScreenManager.Instance.StartScreen(
            ScreenPrefabs.Instance.InfoDialogScreen.gameObject,
            GameScreenManager.Instance.ssOverlayCanvas.gameObject
        );
        screen.SetHeader("Multiplayer");
        screen.AddPlainText("Соединение потеряно. Дальнейшая игра не будет синхронизироваться");
        screen.AddOption(
            "OK",
            _ => PauseScreen.Instance.OnQuitConfirm(true)
        );
    }
}
