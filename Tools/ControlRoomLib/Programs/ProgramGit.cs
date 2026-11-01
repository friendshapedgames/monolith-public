using ControlRoomLib.Core;
using SecretPlanCore.Core;

namespace ControlRoomLib.Programs;

public class ProgramGit : ExternalProgram
{
    public enum GitState
    {
        Normal,
        Merge,
        Rebase,
        CherryPick,
        Revert,
        Bisect,
        Unknown
    }

    public ProgramGit(string workingDirectory) : base("git", workingDirectory)
    {
    }

    public async Task CommitAll(string commitMessage)
    {
        await AddAll();
        await Commit(commitMessage);
    }

    public async Task Commit(string commitMessage)
    {
        await Run($"commit -m \"[Automated:ControlRoom] {commitMessage}\"");
    }

    public Task AddAll()
    {
        return Run("add .");
    }

    public async Task<string> CurrentSha()
    {
        return await RunAndGetOutput("rev-parse HEAD");
    }

    public async Task<string> CurrentBranch()
    {
        return await RunAndGetOutput("branch --show-current");
    }

    public async Task<bool> IsCurrentRepoClean()
    {
        await Run("diff --quiet");
        if (!WasSuccessful())
        {
            return false;
        }

        await Run("diff --cached --quiet");
        return WasSuccessful();
    }

    public async Task CloneWithSsh(string sshUrl, string destinationPath)
    {
        await Run($"clone {sshUrl} {destinationPath}");
    }

    public async Task<string> GetOriginUrl()
    {
        return await RunAndGetOutput("remote get-url origin");
    }

    public async Task Pull()
    {
        await Run("pull");
        if (!WasSuccessful())
        {
            throw new MissionFailedException("Could not pull");
        }
    }

    public async Task CheckoutBranch(string branchName)
    {
        await Run($"checkout {branchName}");
        if (!WasSuccessful())
        {
            throw new MissionFailedException($"Could not checkout branch {branchName}");
        }
    }

    public async Task CheckoutAndPullBranch(string branchName)
    {
        await CheckoutBranch(branchName);
        await Pull();
    }

    public async Task MergeInLocalBranch(string otherBranchName)
    {
        await Run($"merge --no-edit {otherBranchName}");
        if (!WasSuccessful())
        {
            throw new MissionFailedException(
                $"Could not merge {otherBranchName} into {await CurrentBranch()} automatically, please merge it (or its parent branch) manually");
        }
    }

    /// <summary>
    ///     Prepends the branch name with `origin/` and fetches beforehand
    /// </summary>
    public async Task MergeInRemoteBranch(string otherBranchName)
    {
        await Fetch();
        await Run($"merge --no-edit origin/{otherBranchName}");
        if (!WasSuccessful())
        {
            throw new MissionFailedException(
                $"Could not merge {otherBranchName} into {await CurrentBranch()} automatically, please merge it (or its parent branch) manually");
        }
    }

    public async Task Fetch()
    {
        await Run("fetch");
    }

    public async Task<GitState> GetState()
    {
        var state = (await RunAndGetOutput("rev-parse --git-path state")).Replace(".git/", "");

        switch (state)
        {
            case "state":
                return GitState.Normal;
            case "rebase-apply":
                return GitState.Rebase;
            case "rebase-merge":
                return GitState.Rebase;
            case "MERGE_HEAD":
                return GitState.Merge;
            case "CHERRY_PICK_HEAD":
                return GitState.CherryPick;
            case "REVERT_HEAD":
                return GitState.Revert;
            case "BISECT_LOG":
                return GitState.Bisect;
        }

        return GitState.Unknown;
    }

    /// <summary>
    ///     Figure out what state we're in and abort it, then reset us to origin
    /// </summary>
    public async Task ResetAndAbort()
    {
        await OutPipe.LogMessage($"Attempting to put this repo `{WorkingDirectory}` into a clean state");

        var state = await GetState();
        switch (state)
        {
            case GitState.Normal:
                // do nothing
                break;
            case GitState.Merge:
                await Run("merge --abort");
                break;
            case GitState.Rebase:
                await Run("rebase --abort");
                break;
            case GitState.CherryPick:
                await Run("cherry-pick --abort");
                break;
            case GitState.Revert:
                await Run("revert --abort");
                break;
            case GitState.Bisect:
                await Run("bisect reset");
                break;
            default:
                throw new MissionFailedException($"Cannot abort git state: {state}");
        }

        await AddAll();
        await ResetHard();
    }

    public async Task ResetHard()
    {
        await Run("reset --hard");
    }

    public async Task PushNormal()
    {
        await Run("push");
    }

    public async Task PushSetUpstream()
    {
        var currentBranchName = await CurrentBranch();
        await Run($"push --set-upstream origin {currentBranchName}");

        if (!WasSuccessful())
        {
            throw new MissionFailedException("Push failed, see above output (may need to check log file)");
        }
    }

    public async Task Add(string pattern)
    {
        await Run($"add {pattern}");
    }

    public async Task CheckoutNewBranch(string branchName)
    {
        await Run($"checkout -b {branchName}");

        if (!WasSuccessful())
        {
            throw new MissionFailedException($"Attempted to create branch that already exists: {branchName}");
        }
    }

    public async Task ResetToOrigin()
    {
        await Run($"reset origin/{await CurrentBranch()}");
    }

    public async Task CreateBranchWithoutSwitching(string branchName)
    {
        await Run($"branch {branchName}");

        if (!WasSuccessful())
        {
            throw new MissionFailedException($"Attempted to create branch that already exists: {branchName}");
        }
    }

    public async Task<int> CountPendingFiles()
    {
        var shortStatus = await RunAndGetOutput("status -s");

        if (shortStatus.Length == 0)
        {
            return 0;
        }

        var lines = shortStatus.SplitLines();
        return lines.Length;
    }

    public async Task<bool> DoesBranchExistOnOrigin(string branchName)
    {
        var output = await RunAndGetOutput($"ls-remote --heads origin {branchName}");
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        return true;
    }

    public IEnumerable<GitBranchInfo> GetAllOriginBranches()
    {
        var output = RunAndGetOutput("ls-remote --heads origin").Result.SplitLines();
        foreach (var line in output)
        {
            var tokens = line.Split();
            yield return new GitBranchInfo(tokens[0], tokens[1].Substring("refs/heads/".Length));
        }
    }

    public async Task<bool> DoesBranchHaveUpstreamCounterpart(string localBranchName)
    {
        await Run($"rev-parse --abbrev-ref --symbolic-full-name {localBranchName}@{{u}}");
        return WasSuccessful();
    }

    public async Task<IEnumerable<string>> GetAllLocalBranchNames()
    {
        // Runs `git branch`
        // this returns a list of all branches with a * next to the current branch, so we gotta trim out the star
        return (await RunAndGetOutput("branch")).SplitLines().Select(a => a.Replace("*", "").Trim());
    }

    public async Task DeleteLocalBranch(string branchName)
    {
        await Run($"branch -D {branchName}");
    }

    public IEnumerable<GitCommitInfo> FullHistoryOfBranch(string branchName)
    {
        var output = RunAndGetOutput("log", "--oneline", "--pretty=format:%H :: %an :: %s", branchName).Result;
        var lines = output.SplitLines();
        foreach (var line in lines)
        {
            var separator = " :: ";
            var split = line.Split(separator);
            yield return new GitCommitInfo(split[0], split[1], string.Join(separator, split[2..]));
        }
    }

    public async Task<string?> Show(string commitSha, string filePath)
    {
        var str = await RunAndGetOutput("show", $"{commitSha}:{filePath.Replace("\\", "/")}");

        if (!WasSuccessful())
        {
            return null;
        }

        return str;
    }

    /// <summary>
    ///     Gets common ancestor SHA between two branches
    /// </summary>
    public async Task<string> GetMergeBaseWithRemoteBranch(string localBranch, string localNameOfRemoteBranch)
    {
        await FetchSpecificBranch(localNameOfRemoteBranch);
        var result = await RunAndGetOutput("merge-base", localBranch, $"origin/{localNameOfRemoteBranch}");

        if (!WasSuccessful())
        {
            throw new MissionFailedException(
                $"Cannot get merge base between {localBranch} and {localNameOfRemoteBranch}");
        }

        return result;
    }

    public async Task FetchSpecificBranch(string branchName)
    {
        await Run("fetch", "origin", $"{branchName}:refs/remotes/origin/{branchName}");
    }

    public async Task<GitBranchInfo> GetBranchInfo(string sourceBranchName)
    {
        return new GitBranchInfo(await GetMergeBaseWithRemoteBranch(sourceBranchName, sourceBranchName),
            sourceBranchName);
    }

    public async Task<int> CountCommitsOutOfDate()
    {
        var head = await RunAndGetOutput("rev-parse", "--abbrev-ref", "HEAD");
        if (!await DoesBranchExistOnOrigin(head))
        {
            return 0;
        }

        var output = await RunAndGetOutput("rev-list", "--count", $"HEAD..origin/{head}");
        if (int.TryParse(output, out var parsed))
        {
            return parsed;
        }

        throw new MissionFailedException("Failed to parse output");
    }

    /// <summary>
    ///     Returns local paths of files affected by a commit
    /// </summary>
    /// <param name="commitSha"></param>
    /// <returns></returns>
    public async Task<List<FileInfo>> FilesAffectedByCommit(string commitSha)
    {
        var output = (await RunAndGetOutput("show", "--name-only", "--oneline", commitSha)).SplitLines().ToList();
        output.RemoveAt(0);

        output.RemoveAll(string.IsNullOrWhiteSpace);

        return output.Select(a => new FileInfo(a)).ToList();
    }
}