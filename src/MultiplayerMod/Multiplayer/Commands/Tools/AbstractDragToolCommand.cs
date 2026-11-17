using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerMod.Core.Extensions;
using MultiplayerMod.Game.Context;
using MultiplayerMod.Game.UI.Tools.Context;
using MultiplayerMod.Game.UI.Tools.Events;

namespace MultiplayerMod.Multiplayer.Commands.Tools;

[Serializable]
public abstract class AbstractDragToolCommand<T> : MultiplayerCommand where T : DragTool, new() {

    protected DragCompleteEventArgs Arguments;

    protected AbstractDragToolCommand(DragCompleteEventArgs arguments) {
        Arguments = arguments;
    }

    // ReSharper disable once Unity.IncorrectMonoBehaviourInstantiation
    public override void Execute(MultiplayerCommandContext context) {
        var tool = new T();
        InitializeTool(tool);
        GameContext.Override(CreateContext(), () => InvokeTool(tool));
    }

    protected virtual IGameContext CreateContext() => new PrioritySettingsContext(Arguments.Priority);

    protected virtual void InitializeTool(T tool) {
        tool.downPos = Arguments.CursorDown;

        if (tool is not FilteredDragTool filteredTool)
            return;

        var active = new HashSet<string>(Arguments.Parameters ?? System.Array.Empty<string>());
        filteredTool.currentFilters = new[] {
            ToolParameterMenu.FILTERLAYERS.ALL,
            ToolParameterMenu.FILTERLAYERS.WIRES,
            ToolParameterMenu.FILTERLAYERS.LIQUIDCONDUIT,
            ToolParameterMenu.FILTERLAYERS.GASCONDUIT,
            ToolParameterMenu.FILTERLAYERS.SOLIDCONDUIT,
            ToolParameterMenu.FILTERLAYERS.BUILDINGS,
            ToolParameterMenu.FILTERLAYERS.BACKWALL,
            ToolParameterMenu.FILTERLAYERS.LOGIC
        }.Select(
            name => new ToolParameterMenu.ToggleData(
                name,
                active.Contains(name) ? ToolParameterMenu.ToggleState.On : ToolParameterMenu.ToggleState.Off
            )
        ).ToArray();
    }

    protected virtual void InvokeTool(T tool) => Arguments.Cells.ForEach(it => tool.OnDragTool(it, 0));

}
