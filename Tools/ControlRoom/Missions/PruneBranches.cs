using ControlRoomLib.Core;
using JetBrains.Annotations;

namespace ControlRoom.Missions;

[UsedImplicitly]
public class PruneBranches : Mission
{
    public PruneBranches(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var virtualRepo = await VirtualRepo.GetAndInitialize();
        virtualRepo.Git.LogLevel = LogLevel.LogFileOnly;
        
        foreach (var branchName in await virtualRepo.Git.GetAllLocalBranchNames())
        {
            bool hasUpstream = await virtualRepo.Git.DoesBranchHaveUpstreamCounterpart(branchName);
            if (!hasUpstream)
            {
                await OutPipe.LogMessage($"Deleting branch: {branchName}");
                await virtualRepo.Git.DeleteLocalBranch(branchName);
            }
        }
        
    }
}