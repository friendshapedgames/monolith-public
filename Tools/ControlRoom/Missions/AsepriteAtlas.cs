using ControlRoomLib.Core;
using ControlRoomLib.Programs;

namespace ControlRoom.Missions;

public class AsepriteAtlas : Mission
{
    public AsepriteAtlas(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var fileNames = MissionVariables.GameDirectoryFiles().GetFilesAt("Art/Aseprite");

        await OutPipe.LogMessage($"Atlasing {fileNames.Count} files");

        var aseprite = new AsepriteProgram(MissionVariables.GameDirectory);
        await aseprite.BuildAtlas(fileNames, "Art/FullAtlas");
    }
}