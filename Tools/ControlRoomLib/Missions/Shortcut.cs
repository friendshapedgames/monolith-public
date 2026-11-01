using ControlRoomLib.Core;
using JetBrains.Annotations;

namespace ControlRoomLib.BaseMissions;

[UsedImplicitly]
public class Shortcut : Mission
{
    public Shortcut(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var commandType = PositionalArgs.Get(0, "Command").ParseAsSpecificString("add", "remove", "list");

        if (commandType == "add")
        {
            var key = PositionalArgs.Get(1, "Key").ParseAsString();
            var value = PositionalArgs.Get(2, "Value").ParseAsString();
            Platform.SerializedState.Shortcuts[key] = value;
            await OutPipe.LogMessage($"Added shortcut [{key}] = {value}");
        }
        
        if (commandType == "remove")
        {
            var key = PositionalArgs.Get(1, "Key").ParseAsString();
            if (Platform.SerializedState.Shortcuts.Remove(key))
            {
                await OutPipe.LogMessage($"Removed {key} successfully");
            }
            else
            {
                await OutPipe.LogMessage($"{key} did not have a value, did nothing");
            }
        }

        if (commandType == "list")
        {
            foreach (var (existingKey, existingValue) in Platform.SerializedState.Shortcuts)
            {
                await OutPipe.LogMessage($"[{existingKey}] = {existingValue}");
            }
        }
    }
}