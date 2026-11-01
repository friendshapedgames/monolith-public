using ControlRoomLib.Core;
using ControlRoomLib.Programs;
using JetBrains.Annotations;
using Newtonsoft.Json;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

[UsedImplicitly]
public class VersionChange : Mission
{
    public enum VersionBumpType
    {
        Major,
        Minor,
        Patch
    }

    public VersionChange(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var command = PositionalArgs.Get(0, "Command")
            .ParseAsSpecificString("create-empty", "bump-minor", "bump-major", "bump-patch");

        MissionAssert.MissionVariableNotNull(MissionVariables, nameof(MissionVariables.GameDirectory));

        var gameDirectoryPath = MissionVariables.GameDirectory!;

        MissionAssert.IsTrue(ControlRoomConstants.WorkingDirectoryFiles.HasDirectory(gameDirectoryPath),
            $"{gameDirectoryPath} is not a valid directory");

        var gameFiles = new RealFileSystem(gameDirectoryPath);

        if (command == "create-empty")
        {
            await OutPipe.LogMessage($"Creating empty {ControlRoomConstants.VersionFile} in {gameDirectoryPath}");
            WriteVersionFile(gameFiles, new SecretPlanVersion());
            return;
        }

        await OutPipe.LogMessage($"Bumping version for {gameDirectoryPath}");

        var newVersion = await GetVersionInDirectory(ControlRoomConstants.WorkingDirectoryFiles, gameDirectoryPath);
        var oldVersionNumber = newVersion.Version;

        switch (command)
        {
            case "bump-major":
                newVersion.BumpMajor();
                break;
            case "bump-minor":
                newVersion.BumpMinor();
                break;
            case "bump-patch":
                newVersion.BumpPatch();
                break;
        }

        WriteVersionFile(gameFiles, newVersion);
        await OutPipe.LogMessage($"Bumped version from: {oldVersionNumber} -> {newVersion.Version}");

        await CommitAndPushIfRequested(new ProgramGit(Environment.CurrentDirectory), gameDirectoryPath, newVersion);
    }

    public static async Task CommitAndPushIfRequested(ProgramGit git, string gameDirectoryPath, SecretPlanVersion newVersion)
    {
        await OutPipe.LogMessage("You must commit and push the bumped version file before you build.");
        var numberOfChangedFiles = await git.CountPendingFiles();

        var shouldContinue = true;
        if (numberOfChangedFiles != 1)
        {
            var response = await OutPipe.Prompt(
                $"Do you want me to commit and push for you (will push {numberOfChangedFiles} files)?", "no");
            shouldContinue = response != null && response.StartsWith("y");
        }

        if (shouldContinue)
        {
            await git.CommitAll($"Bump version {gameDirectoryPath} {newVersion.Version}");
            await git.PushNormal();
        }
    }

    public static async Task<SecretPlanVersion> GetVersionInDirectory(RealFileSystem files,
        string gameDirectoryPath)
    {
        var gameFiles = (RealFileSystem)files.GetDirectory(gameDirectoryPath);
        MissionAssert.IsTrue(gameFiles.HasFile(ControlRoomConstants.VersionFile),
            $"Expected to find {ControlRoomConstants.VersionFile} in {gameFiles.GetCurrentDirectory()}");

        var versionJson = await gameFiles.ReadFileAsync(ControlRoomConstants.VersionFile);
        var newVersion = await ControlRoomConstants.JsonDeserializeSafe<SecretPlanVersion>(versionJson);
        MissionAssert.IsNotNull(newVersion,
            $"Failed to parse {gameFiles.GetCurrentDirectory()}/{ControlRoomConstants.VersionFile}");
        return newVersion;
    }

    public static string GetPathToVersionFile(RealFileSystem files, string gameDirectoryPath)
    {
        var gameFiles = (RealFileSystem)files.GetDirectory(gameDirectoryPath);
        MissionAssert.IsTrue(gameFiles.HasFile(ControlRoomConstants.VersionFile),
            $"Expected to find {ControlRoomConstants.VersionFile} in {gameFiles.GetCurrentDirectory()}");

        return Path.Join(gameFiles.GetCurrentDirectory(), ControlRoomConstants.VersionFile);
    }

    public static void WriteVersionFile(RealFileSystem gameFiles, SecretPlanVersion version)
    {
        var json = JsonConvert.SerializeObject(version, Formatting.Indented);
        gameFiles.WriteToFile(ControlRoomConstants.VersionFile, json);
    }

    public static async Task<SecretPlanVersion> Stamp(RealFileSystem virtualRepoFiles, string gameDirectoryPath)
    {
        await OutPipe.LogMessage($"Stamping {ControlRoomConstants.VersionFile} with git info");
        var git = new ProgramGit(virtualRepoFiles.GetCurrentDirectory());
        MissionAssert.IsTrue(virtualRepoFiles.HasDirectory(gameDirectoryPath),
            $"Could not find game directory {virtualRepoFiles.ToAbsolutePath(gameDirectoryPath)}");

        var gameFiles = (RealFileSystem)virtualRepoFiles.GetDirectory(gameDirectoryPath);
        var sha = await git.CurrentSha();
        var branch = await git.CurrentBranch();
        var existingVersion = await GetVersionInDirectory(virtualRepoFiles, gameDirectoryPath);
        existingVersion.StampGitInfo(branch, sha);
        WriteVersionFile(gameFiles, existingVersion);
        return existingVersion;
    }

    public static async Task<SecretPlanVersion> BumpVersion(RealFileSystem virtualRepoFiles, string gameDirectoryPath,
        VersionBumpType versionBumpType)
    {
        await OutPipe.LogMessage($"Bumping {ControlRoomConstants.VersionFile}");
        MissionAssert.IsTrue(virtualRepoFiles.HasDirectory(gameDirectoryPath),
            $"Could not find game directory {virtualRepoFiles.ToAbsolutePath(gameDirectoryPath)}");

        var gameFiles = (RealFileSystem)virtualRepoFiles.GetDirectory(gameDirectoryPath);
        var existingVersion = await GetVersionInDirectory(virtualRepoFiles, gameDirectoryPath);

        switch (versionBumpType)
        {
            case VersionBumpType.Major:
                existingVersion.BumpMajor();
                break;
            case VersionBumpType.Minor:
                existingVersion.BumpMinor();
                break;
            case VersionBumpType.Patch:
                existingVersion.BumpPatch();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(versionBumpType), versionBumpType, null);
        }

        WriteVersionFile(gameFiles, existingVersion);

        return existingVersion;
    }
}