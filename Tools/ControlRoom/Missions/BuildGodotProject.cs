using ControlRoomLib.Core;
using ControlRoomLib.Programs;

namespace ControlRoom.Missions;

public class BuildGodotProject : Mission
{
    public BuildGodotProject(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    private string GameDirectory => MissionVariables.GameDirectory!;
    
    public override async Task Run()
    {
        MissionAssert.MissionVariableNotNull(MissionVariables, nameof(MissionVariables.GameDirectory));
        var buildTarget = PositionalArgs.Get(0, "Target OS").ParseAsBuildTarget();
        var steamBuildInfo = ControlRoomConstants.GetGameSteamBuildInfo(GameDirectory, SteamAppType.FullGame);
        
        var virtualRepo = await VirtualRepo.GetAndInitialize();
        
        var directoryInfo = virtualRepo.Files.DirectoryInfoAt(GameDirectory);
        MissionAssert.IsTrue(directoryInfo.Exists, $"Directory {GameDirectory} does not exist");
        
        var godot = new ProgramGodotLatest(virtualRepo.Files.GetCurrentDirectory());
        await godot.RunPresetBuild(GameDirectory, "Game", buildTarget);
        if (!godot.WasSuccessful())
        {
            throw new Exception("Godot build failed!");
        }

        var outputDirectory = godot.GetOutputDirectoryForProject(GameDirectory, buildTarget);
        await OutPipe.LogMessage("Preparing local environment for smoke test");
        await File.WriteAllTextAsync(Path.Join(outputDirectory.FullName, "steam_appid.txt"), steamBuildInfo.AppId.ToString());
        
        var explorer = ControlRoomConstants.FileExplorerProgram();
        await explorer.Open(godot.GetOutputDirectoryForProject(GameDirectory, buildTarget).FullName);
    }
}