using ControlRoomLib.Core;
using JetBrains.Annotations;

namespace ControlRoom.Missions;

[UsedImplicitly]
public class Echo : Mission
{
    public Echo(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var message = PositionalArgs.Get(0, "message").ParseAsString();
        await OutPipe.LogMessage(message);
    }
}