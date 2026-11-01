using ControlRoomLib.Core;
using ControlRoomLib.Programs;
using Newtonsoft.Json;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

public class PatchNotes : Mission
{
    public PatchNotes(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    private string GameDirectory => MissionVariables.GameDirectory!;

    public override async Task Run()
    {
        MissionAssert.MissionVariableNotNull(MissionVariables, nameof(MissionVariables.GameDirectory));

        var virtualRepo = await VirtualRepo.GetAndInitialize();

        ControlRoomConstants.WorkingDirectoryFiles.WriteToFile(ControlRoomConstants.PatchNotesFile,
            await GeneratePatchNotesFile(
                ControlRoomConstants.FlockAroundFullGame, GameDirectory, virtualRepo.Files, virtualRepo.Git));
    }

    public static async Task<string[]> GeneratePatchNotesFile(SteamBuildInfo buildInfo, string gameDirectory,
        RealFileSystem files,
        ProgramGit git)
    {
        await OutPipe.LogMessage("Building patch notes... (this might take a while)");
        git.LogLevel = LogLevel.LogFileOnly;
        var sourceBranchName = buildInfo.BranchToForkFrom;
        var versionToProbe = new SecretPlanVersion
        {
            BuildBranch = sourceBranchName
        };

        var versionPath = VersionChange.GetPathToVersionFile(files, gameDirectory);
        var relativeVersionPath = Path.GetRelativePath(files.GetCurrentDirectory(), versionPath);

        var sourceBranch = await git.GetBranchInfo(sourceBranchName);
        var cacheFilePath = $"Local/{nameof(PatchNotes)}_{gameDirectory}_{sourceBranchName}_cache";
        var cache = await ControlRoomConstants.JsonDeserializeSafe<Cache>(
                        await ControlRoomConstants.WorkingDirectoryFiles.ReadFileAsync(cacheFilePath)) ??
                    new Cache();

        await OutPipe.LogMessage("Fetching branches");
        foreach (var releaseBranch in git.GetAllOriginBranches())
        {
            if (cache.KnownBranches.Contains(releaseBranch.BranchName))
            {
                continue;
            }

            if (!releaseBranch.BranchName.StartsWith(ControlRoomConstants.ReleaseBranchPrefix(gameDirectory)) ||
                !releaseBranch.BranchName.Contains(versionToProbe.BuildBranch))
            {
                continue;
            }

            var fork = new GitForkedBranchInfo(releaseBranch, sourceBranch,
                await git.GetMergeBaseWithRemoteBranch(sourceBranchName, releaseBranch.BranchName));

            var versionContent = await git.Show(fork.CommonSha, relativeVersionPath);

            if (string.IsNullOrWhiteSpace(versionContent))
            {
                throw new MissionFailedException($"Could not find version of branch {fork.ForkBranch}");
            }

            var releaseBranchVersion = await ControlRoomConstants.JsonDeserializeSafe<SecretPlanVersion>(versionContent);

            if (releaseBranchVersion == null)
            {
                throw new MissionFailedException($"Could not decode version of branch {fork.ForkBranch}");
            }

            await OutPipe.LogMessage(
                $"{releaseBranch.BranchName} was split from {fork.CommonSha} as {releaseBranchVersion.SemVerString}");
            cache.ShaToVersion[fork.CommonSha] = releaseBranchVersion;
            cache.KnownBranches.Add(releaseBranch.BranchName);
        }

        var patchNotesLines = new List<string>
        {
            "# Patch Notes",
            "",
            "## Next Release",
            "",
            "> Anything in this section is not yet released",
            ""
        };

        await OutPipe.LogMessage("Crawling commit history");
        var allCommits = git.FullHistoryOfBranch(sourceBranchName).ToList();
        foreach (var commit in allCommits)
        {
            var isRelevantCommit = false;

            if (cache.CommitRelevance.TryGetValue(commit.Sha, out var value))
            {
                // relevance is known, assign it
                isRelevantCommit = value;
            }
            else
            {
                // relevance is not known, calculate it
                foreach (var file in await git.FilesAffectedByCommit(commit.Sha))
                {
                    if (file.FullName.Contains(gameDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        isRelevantCommit = true;
                        break;
                    }
                }
            }

            if (cache.ShaToVersion.TryGetValue(commit.Sha, out var version))
            {
                // Add header for version
                patchNotesLines.Add($"## {version.SemVerString}");
            }

            if (isRelevantCommit)
            {
                patchNotesLines.Add(
                    $"- {commit.Message} (by {commit.Author} [See Commit]({ControlRoomConstants.GitHubWebCommitUrl(commit.Sha)}))");
            }

            cache.CommitRelevance[commit.Sha] = isRelevantCommit;
        }

        ControlRoomConstants.WorkingDirectoryFiles.WriteToFile(cacheFilePath, JsonConvert.SerializeObject(cache, Formatting.Indented));

        git.LogLevel = LogLevel.ConsoleAndLogFile;
        return patchNotesLines.ToArray();
    }

    private class Cache
    {
        [JsonProperty("sha_to_version")]
        public Dictionary<string, SecretPlanVersion> ShaToVersion { get; init; } = new();

        [JsonProperty("known_branches")]
        public HashSet<string> KnownBranches { get; init; } = new();

        [JsonProperty("commit_relevance")]
        public Dictionary<string, bool> CommitRelevance { get; init; } = new();
    }
}