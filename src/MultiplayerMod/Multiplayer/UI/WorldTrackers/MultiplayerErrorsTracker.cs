using System;
using MultiplayerMod.Multiplayer.World.Debug;

namespace MultiplayerMod.Multiplayer.UI.WorldTrackers;

public class MultiplayerErrorsTracker : WorldTracker {

    // KleiStringBuilder.Append has ReadOnlySpan<char> overloads, which the net48 compiler can't resolve,
    // so bind the Append(char) overload at runtime.
    private delegate void AppendChar(ref KleiStringBuilder builder, char value);

    private static readonly AppendChar appendChar = (AppendChar) Delegate.CreateDelegate(
        typeof(AppendChar),
        typeof(KleiStringBuilder).GetMethod("Append", new[] { typeof(char) })!
    );

    public MultiplayerErrorsTracker(int worldId) : base(worldId) { }

    public override void UpdateData() {
        AddPoint(WorldDebugSnapshotRunner.ErrorsCount);
    }

    // Game build 763796+ replaced FormatValueString(float) with AppendValueString(ref KleiStringBuilder, float)
    public override void AppendValueString(ref KleiStringBuilder builder, float value) {
        foreach (var c in value.ToString()) appendChar(ref builder, c);
    }

}
