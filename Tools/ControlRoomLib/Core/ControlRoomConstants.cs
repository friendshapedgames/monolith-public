using System.Globalization;
using ControlRoomLib.Programs;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using SecretPlanCore.Core;

namespace ControlRoomLib.Core;

public static class ControlRoomConstants
{
    public const string SandboxPath = "./.build";
    public const string VersionFile = "VERSION.json";

    public const string SelfRunCommand = "./controlroom";

    /// <summary>
    ///     Local path to windows build from the game's root directory
    /// </summary>
    public const string WindowsBuildOutputLocalPath = ".build/win";

    public const string LogsFolder = "ControlRoomLogs";
    public const string StatePath = "ControlRoomState.json";

    public const string TestInternalSteamBranch = "test_internal";

    public const string PatchNotesFile = "PATCH_NOTES.md";
    private static string? _fileName;

    public static readonly RealFileSystem WorkingDirectoryFiles = new(Environment.CurrentDirectory);
    public static readonly IFileSystem LocalUntrackedFiles = WorkingDirectoryFiles.GetDirectory("Local");

    public static SteamBuildInfo FlockAroundFullGame = new(3618030, "flockaround_main",
    [
        new SteamDepotInfo(3618031, BuildTarget.Windows),
        new SteamDepotInfo(3618032, BuildTarget.MacOsUniversal),
        new SteamDepotInfo(3618033, BuildTarget.Linux)
    ]);

    public static SteamBuildInfo FlockAroundDemo = new(4211680, "flockaround_demo",
    [
        new SteamDepotInfo(4211681, BuildTarget.Windows),
        new SteamDepotInfo(4211682, BuildTarget.MacOsUniversal)
    ]);

    public static SteamBuildInfo FlockAroundPlaytest =
        new(4131070, "flockaround_playtest",
        [
            new SteamDepotInfo(4131071, BuildTarget.Windows),
            new SteamDepotInfo(4131072, BuildTarget.MacOsUniversal)
        ]);


    public static string CurrentLogFile
    {
        get { return _fileName ??= $"ControlRoom_{DateTime.Now.ToFileTimeUtc()}.txt"; }
    }

    public static SteamBuildInfo GetFlockAroundSteamBuildInfo(SteamAppType desiredBuildType)
    {
        switch (desiredBuildType)
        {
            case SteamAppType.Playtest:
                return FlockAroundPlaytest;
            case SteamAppType.Demo:
                return FlockAroundDemo;
            case SteamAppType.FullGame:
            case SteamAppType.FullGameWithAdditionalBranch:
                return FlockAroundFullGame;
            default:
                throw new MissionFailedException($"Unable to get build info for {desiredBuildType}");
        }
    }

    public static string GenerateMissionCommand<T>(string? args = null) where T : Mission
    {
        var suffix = string.Empty;
        if (args != null)
        {
            suffix = " " + args;
        }

        return $"{SelfRunCommand} {typeof(T).Name.ToLower()}{suffix}";
    }

    public static async Task<T?> JsonDeserializeSafe<T>(string json)
    {
        try
        {
            var result = JsonConvert.DeserializeObject<T>(json);
            return result;
        }
        catch (Exception)
        {
            await OutPipe.AgentLogWarning($"Could not deserialize json into {typeof(T).Name}, returning a default");
            return default;
        }
    }

    public static string NormalizeCapitalizationOnPath(string gameDirectoryPath)
    {
        var directoryInfo = WorkingDirectoryFiles.DirectoryInfoAt(gameDirectoryPath);
        MissionAssert.IsTrue(directoryInfo.Exists, $"Directory {gameDirectoryPath} does not exist");

        // Make sure the directory name is EXACTLY right (this only matters on windows where birdGame -> BirdGame
        gameDirectoryPath = directoryInfo.Name;
        return gameDirectoryPath;
    }

    public static string ReleaseBranchName(string gameDirectory, SecretPlanVersion releaseVersion)
    {
        return $"release/{gameDirectory.ToLower()}_{releaseVersion.BuildBranch}_{releaseVersion.SemVerString}";
    }

    public static string HumanReadableStringify(object? value)
    {
        if (value == null)
        {
            return "(null)";
        }

        if (value is string)
        {
            return $"\"{value}\"";
        }

        return value.ToString()!;
    }

    // todo: move this to Constants
    public static CsvConfiguration DefaultCsvConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null
        };
    }


    /// <summary>
    ///     We can't rely on Google Sheets to export a tsv reliably so we instead import csv and export tsv
    /// </summary>
    public static CsvConfiguration GetDefaultTsvConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            Delimiter = "\t"
        };
    }

    public static IFileExplorerProgram FileExplorerProgram()
    {
        switch (Platform.GetHostOperatingSystem())
        {
            case HostOperatingSystem.Windows:
                return new ProgramExplorerWindows();
            case HostOperatingSystem.MacOs:
                return new ProgramOpenMacOs();
            case HostOperatingSystem.Linux:
                throw new MissionFailedException(
                    "We don't have a file explorer implemented for Linux yet (todo: Zenity?)");
            case HostOperatingSystem.Unknown:
            default:
                throw new MissionFailedException(
                    "Could not create a file explorer program because we don't know what OS we're on");
        }
    }

    /// <summary>
    ///     URL for the webpage corresponding to a branch
    /// </summary>
    public static string GitHubWebUrl(string branchName)
    {
        return $"https://github.com/friendshapedgames/monolith/tree/{branchName}";
    }

    public static string GitHubWebPatchNotesUrl(string branchName)
    {
        return $"https://github.com/friendshapedgames/monolith/tree/{branchName}/PATCH_NOTES.md";
    }

    public static string GitHubWebCommitUrl(string sha)
    {
        return $"https://github.com/friendshapedgames/monolith/commit/{sha}";
    }

    public static async Task<bool> CheckLocalIsLatest()
    {
        var rootGit = new ProgramGit(Environment.CurrentDirectory);
        rootGit.LogLevel = LogLevel.LogFileOnly;

        await rootGit.Fetch();
        var commitsOutOfDate = await rootGit.CountCommitsOutOfDate();

        if (commitsOutOfDate == 0)
        {
            return true;
        }

        var answer = await OutPipe.AgentPrompt(
            $"You are {commitsOutOfDate} commit(s) out of date on this branch. Type `continue` to continue, anything else will bail");
        if (!string.IsNullOrWhiteSpace(answer) && answer.StartsWith("continue"))
        {
            return true;
        }

        return false;
    }
}