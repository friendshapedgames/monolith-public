using ControlRoomLib.Core;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

public class Repl : Mission
{
    public Repl(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        await OutPipe.LogMessage("Entered REPL mode");

        while (true)
        {
            var input = await OutPipe.ReplPrompt();

            if (input == null)
            {
                continue;
            }

            if (input is "quit" or "exit")
            {
                break;
            }

            var givenArgs = input.Trim().SplitTokens().ToList();
            await Platform.AttemptToRunMissionWithArgs(givenArgs, MissionVariables);
        }
    }
}