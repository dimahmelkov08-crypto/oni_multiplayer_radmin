using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerMod.Core.Extensions;
using MultiplayerMod.Game.UI.Tools.Events;

namespace MultiplayerMod.Multiplayer.Commands.Tools;

[Serializable]
public class Harvest : AbstractDragToolCommand<HarvestTool> {

    public Harvest(DragCompleteEventArgs arguments) : base(arguments) { }

    protected override void InitializeTool(HarvestTool tool) {
        base.InitializeTool(tool);
        var active = new HashSet<string>(Arguments.Parameters ?? System.Array.Empty<string>());
        tool.options = new[] { "HARVEST_WHEN_READY", "DO_NOT_HARVEST" }
            .Select(
                name => new ToolParameterMenu.ToggleData(
                    name,
                    active.Contains(name) ? ToolParameterMenu.ToggleState.On : ToolParameterMenu.ToggleState.Off
                )
            ).ToArray();
    }

}
