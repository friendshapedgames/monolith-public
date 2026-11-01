using ControlRoomLib.Missions;
using ControlRoomLib.Programs;
using SecretPlanCore.Core;

namespace ControlRoomLib.Core;

public class VirtualRepo
{
    public readonly RealFileSystem Files;
    public readonly ProgramGit Git;
    private string? _repoUrl;
    private readonly string _localPathWithinSandbox;

    private VirtualRepo(string? repoUrl, string localPathWithinSandbox)
    {
        _repoUrl = repoUrl;
        var workingDirectory = Path.Join(ControlRoomConstants.SandboxPath, localPathWithinSandbox);
        _localPathWithinSandbox = localPathWithinSandbox;
        Git = new ProgramGit(workingDirectory);
        Files = new RealFileSystem(workingDirectory);
    }

    public static async Task<VirtualRepo> GetAndInitialize(string? repoUrl, string localPath)
    {
        var repo = new VirtualRepo(repoUrl, localPath);
        await repo.CloneOrReset();
        return repo;
    }
    
    public static async Task<VirtualRepo> GetAndInitialize()
    {
        return await GetAndInitialize(null, "VirtualMonoRepo");
    }

    public async Task<string> RepoUrl()
    {
        if (_repoUrl == null)
        {
            var rootGit = new ProgramGit(".");
            var ownRepoUrl = await rootGit.GetOriginUrl();
            await OutPipe.LogMessage(
                $"VirtualRepo was not given a URL, so we assume this is the same as the current repo, which is: {ownRepoUrl}");
            _repoUrl = ownRepoUrl;
        }

        return _repoUrl;
    }

    /// <summary>
    ///     Clones the Virtual Repo and/or resets it into a vanilla state
    /// </summary>
    private async Task CloneOrReset()
    {
        Git.LogLevel = LogLevel.LogFileOnly;
        // This git lives in the "sandbox" aka: `.build` 
        var sandboxGit = new ProgramGit(ControlRoomConstants.SandboxPath);
        sandboxGit.LogLevel = LogLevel.LogFileOnly;
        await sandboxGit.CloneWithSsh(await RepoUrl(), _localPathWithinSandbox);
        sandboxGit.LogLevel = LogLevel.ConsoleAndLogFile;
        if (!sandboxGit.WasSuccessful())
        {
            await VirtualRepoLog("Looks like the repo already exists, we will reuse it");
        }

        var originUrl = await Git.GetOriginUrl();
        MissionAssert.AreEqual(originUrl, await RepoUrl(),
            $"Virtual repo has wrong remote URL, run `{ControlRoomConstants.GenerateMissionCommand<CleanSandbox>()}`");

        await VirtualRepoLog("Setting up repo");

        await VirtualRepoLog("Attempting to abort any current state");
        await Git.ResetAndAbort();

        await VirtualRepoLog("Checking out main");
        await Git.CheckoutBranch("main");

        await VirtualRepoLog("Resetting to origin");
        await Git.ResetToOrigin();

        await VirtualRepoLog("Attempting to abort any current state (again, for safety)");
        await Git.ResetAndAbort();

        await VirtualRepoLog("Pulling latest");
        await Git.Pull();

        Git.LogLevel = LogLevel.ConsoleAndLogFile;
        await VirtualRepoLog("Ready!");
    }

    public RealFileSystem GetSubdirectory(string directory)
    {
        return (RealFileSystem)Files.GetDirectory(directory);
    }

    public async Task CommitAndPush(string commitMessage)
    {
        await VirtualRepoLog($"Committing everything with message: {commitMessage}");
        await Git.CommitAll(commitMessage);
        await Git.PushSetUpstream();
    }

    public async Task CheckoutNewBranchAndPush(string releaseBranchName)
    {
        await VirtualRepoLog($"Checking out and pushing: {releaseBranchName}");
        await Git.CheckoutNewBranch(releaseBranchName);
        await Git.PushSetUpstream();
    }

    private Task VirtualRepoLog(string message)
    {
        return OutPipe.LogMessage($"VirtualRepo ({_localPathWithinSandbox}): {message}");
    }

    public FileInfo BuildAndDumpVdf(SteamUploadSku uploadSku, DirectoryInfo contentRoot, string description)
    {
        var buildVdfLocalPath = "build.vdf";
        Files.WriteToFile(buildVdfLocalPath,
            ProgramSteamCmd.GenerateBuildVdf(uploadSku, contentRoot.FullName, description));

        return new FileInfo(Path.Join(Files.GetCurrentDirectory(), buildVdfLocalPath));
    }
}