using ControlRoomLib.Core;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

public class ClearLogs : Mission
{
    public ClearLogs(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var files = new RealFileSystem(ControlRoomConstants.LogsFolder);
        foreach (var file in files.GetFilesAt("."))
        {
            if (file != ControlRoomConstants.CurrentLogFile)
            {
                await OutPipe.LogMessage($"Deleting {file}");
                files.DeleteFile(file);
            }
        }
    }
}