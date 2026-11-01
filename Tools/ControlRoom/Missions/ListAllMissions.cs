using ControlRoomLib.Core;
using JetBrains.Annotations;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

[UsedImplicitly]
public class ListAllMissions : Mission
{
    public ListAllMissions(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var allMissions = Reflection.GetAllTypesThatDeriveFrom<Mission>();

        foreach (var mission in allMissions)
        {
            await OutPipe.LogMessage($" - {mission.Name}");
        }
    }
}