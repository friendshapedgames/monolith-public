using ControlRoomLib.Core;
using JetBrains.Annotations;
using SecretPlanCore.Core;

namespace ControlRoomLib.Missions;

[UsedImplicitly]
public class CleanSandbox : Mission
{
    public CleanSandbox(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var sandboxFiles = new RealFileSystem(Environment.CurrentDirectory);
        var answer = await OutPipe.Prompt($"This is going to attempt to delete your {ControlRoomConstants.SandboxPath}, are you sure?", "no");

        if (answer != null && answer.StartsWith("y"))
        {
            await OutPipe.LogMessage($"Deleting all files at {ControlRoomConstants.SandboxPath}");
            await OutPipe.LogMessage("If this fails, you might need to delete the files manually.");
            sandboxFiles.DeleteDirectory(ControlRoomConstants.SandboxPath, true);
        }
    }
}