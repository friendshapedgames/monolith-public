using ControlRoomLib.Core;
using ControlRoomLib.Programs;

namespace ControlRoom.Missions;

public class DownloadLatest : Mission
{
    public DownloadLatest(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var url = "git@github.com:friendshapedgames/monolith-public.git";
        var sourceRepo =
            await VirtualRepo.GetAndInitialize(url, "VirtualPublicRepo");
        var destinationRepo =
            await VirtualRepo.GetAndInitialize();
        await CopyRepoCore.CopyCoreRepoContents(sourceRepo, destinationRepo);

        if ((await OutPipe.AgentPrompt("Do you want to pull `main` into this branch?", "no"))?.StartsWith("y") == true)
        {
            var localGit = new ProgramGit(".");
            await localGit.MergeInRemoteBranch("main");
        }
    }
}