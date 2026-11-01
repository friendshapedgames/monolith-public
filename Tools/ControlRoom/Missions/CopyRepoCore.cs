using System.Text;
using System.Text.RegularExpressions;
using ControlRoomLib.BaseMissions;
using ControlRoomLib.Core;
using ControlRoomLib.Missions;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

public class CopyRepoCore : Mission
{
    public CopyRepoCore(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var sourceRepoUrl = PositionalArgs.Get(0, "Source Repo ssh URL").ParseAsString();

        if (sourceRepoUrl == "self")
        {
            // null means "use own URL"
            sourceRepoUrl = null;
        }


        var sourceRepo =
            await VirtualRepo.GetAndInitialize(sourceRepoUrl, sourceRepoUrl != null ? MakeSafeFilePath(sourceRepoUrl) : "SelfRepo");

        var destinationRepoUrl = PositionalArgs.Get(1, "Target Repo ssh URL").ParseAsString();
        var destinationRepo =
            await VirtualRepo.GetAndInitialize(destinationRepoUrl, MakeSafeFilePath(destinationRepoUrl));

        await CopyCoreRepoContents(sourceRepo, destinationRepo);
    }

    private string MakeSafeFilePath(string input)
    {
        var replacementChar = '_';
        
        string[] reservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        if (string.IsNullOrWhiteSpace(input))
        {
            return "untitled";
        }

        // 1. Remove invalid characters based on the OS
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            sb.Append(invalidChars.Contains(c) ? replacementChar : c);
        }

        var result = sb.ToString().Trim();

        // 2. Handle Windows-specific edge cases (trailing dots/spaces and reserved words)
        // Strip trailing periods or spaces which are invalid on Windows
        result = Regex.Replace(result, @"[\.\s]+$", "");

        // If the entire string was stripped, provide a fallback
        if (string.IsNullOrEmpty(result))
        {
            return "untitled";
        }

        // Check against Windows reserved system names
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(result).ToUpperInvariant();
        if (reservedNames.Contains(nameWithoutExtension))
        {
            result = replacementChar + result;
        }

        // 3. Enforce Max Length constraints (Windows standard max filename is 255 chars)
        if (result.Length > 255)
        {
            var ext = Path.GetExtension(result);
            var maxNameLength = 255 - ext.Length;
            if (maxNameLength <= 0)
            {
                result = result.Substring(0, 255);
            }
            else
            {
                result = result.Substring(0, maxNameLength) + ext;
            }
        }

        return result;
    }

    public static async Task CopyCoreRepoContents(VirtualRepo sourceRepo, VirtualRepo destinationRepo,
        string? message = null, Func<RealFileSystem, Task>? scrub = null)
    {
        string[] exactFilesToCopy =
        [
            ".gitattributes",
            ".gitignore",
            "controlroom",
            "ControlRoom.cmd",
            "Directory.Build.props"
        ];

        foreach (var filePath in exactFilesToCopy)
        {
            await CopyFile(sourceRepo.Files, destinationRepo.Files, filePath);
        }

        string[] directoriesToCopy =
        [
            "Packages",
            "Tools",
            "Templates"
        ];

        foreach (var directoryPath in directoriesToCopy)
        {
            destinationRepo.Files.DeleteDirectory(directoryPath, true);

            await CopyDirectory(sourceRepo.Files.GetPathOfFile(directoryPath),
                destinationRepo.Files.GetPathOfFile(directoryPath));
        }

        string[] missionsToKeep =
        [
            nameof(AllSln),
            nameof(BuildGodotProject),
            nameof(CleanSandbox),
            nameof(ClearLogs),
            nameof(Echo),
            nameof(ListAllMissions),
            nameof(MissionVariablesEdit),
            nameof(NewProject),
            nameof(PatchNotes),
            nameof(PruneBranches),
            nameof(Shortcut),
            nameof(VersionChange),
            nameof(ConfigManagement),
            nameof(AsepriteAtlas),
            nameof(CopyRepoCore),
            nameof(DownloadLatest),
            nameof(Repl)
        ];

        var missionFiles = destinationRepo.Files.GetDirectory("Tools/ControlRoom/Missions");
        var missionFileNames = missionFiles.GetFilesAt(".");

        if (missionFileNames.Count == 0)
        {
            throw new MissionFailedException("Missions directory in the public repo is empty! Did it get moved?");
        }

        foreach (var fileName in missionFileNames)
        {
            var missionName = new FileInfo(fileName).Name.RemoveFileExtension();
            if (!missionsToKeep.Contains(missionName))
            {
                await OutPipe.LogMessage($"Deleting mission: {missionName}");
                missionFiles.DeleteFile(fileName);
            }
        }

        if (scrub != null)
        {
            await scrub.Invoke(destinationRepo.Files);
        }

        await PushContents(destinationRepo,
            message ?? $"Copied {await sourceRepo.RepoUrl()}@{await sourceRepo.Git.CurrentSha()}");
    }

    private static async Task PushContents(VirtualRepo destinationRepo, string message)
    {
        await destinationRepo.Git.AddAll();

        if (await destinationRepo.Git.CountPendingFiles() == 0)
        {
            await OutPipe.LogMessage("Up-to-date. Nothing to upload");
            return;
        }

        await destinationRepo.CommitAndPush(message);
    }

    /// <summary>
    ///     If the file contains a line that matches the forbidden keyword, remove it
    /// </summary>
    public static async Task ScrubLinesFromFile(RealFileSystem files, string filePath,
        string[] forbiddenKeywords)
    {
        var oldContentLines = (await files.ReadFileAsync(filePath)).SplitLines();
        var newContent = new StringBuilder();

        foreach (var line in oldContentLines)
        {
            var shouldSkip = false;
            foreach (var removeKeyword in forbiddenKeywords)
            {
                if (line.Contains(removeKeyword))
                {
                    shouldSkip = true;
                }
            }

            if (shouldSkip)
            {
                continue;
            }

            newContent.AppendLine(line);
        }

        await files.WriteToFileAsync(filePath, newContent.ToString());
    }


    public static async Task CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        var dir = new DirectoryInfo(sourceDirectory);

        await OutPipe.LogMessage($"Copying directory {sourceDirectory} -> {destinationDirectory}");

        if (!dir.Exists)
        {
            throw new MissionFailedException($"Source not found: {dir.FullName}");
        }

        Directory.CreateDirectory(destinationDirectory);


        // Copy files
        foreach (var file in dir.GetFiles())
        {
            file.CopyTo(Path.Combine(destinationDirectory, file.Name), true);
        }

        // Recursive call for subdirectories
        foreach (var subDir in dir.GetDirectories())
        {
            await CopyDirectory(subDir.FullName, Path.Combine(destinationDirectory, subDir.Name));
        }
    }

    public static async Task CopyFile(RealFileSystem sourceFiles, RealFileSystem destinationFiles, string filePath)
    {
        var sourcePath = sourceFiles.GetPathOfFile(filePath);
        var destPath = destinationFiles.GetPathOfFile(filePath);
        var file = new FileInfo(sourcePath);

        await OutPipe.LogMessage($"Copying file {sourcePath} -> {destPath}");
        file.CopyTo(destPath, true);
    }
}