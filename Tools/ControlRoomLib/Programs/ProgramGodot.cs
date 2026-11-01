using ControlRoomLib.Core;

namespace ControlRoomLib.Programs;

public abstract class ProgramGodot : ExternalProgramFromShortcut
{
    public ProgramGodot(string shortcut, string? workingDirectory = null) : base(shortcut, workingDirectory, null)
    {
    }

    public async Task RunPresetBuild(string projectDirectory, string exeNameWithoutExtension, BuildTarget buildTarget)
    {
        var outputDirectory = GetOutputDirectoryForProject(projectDirectory, buildTarget);

        if (outputDirectory.Exists)
        {
            await OutPipe.LogMessage($"Clearing {outputDirectory.FullName} before we start build.");

            try
            {
                outputDirectory.Delete(true);
            }
            catch (Exception e)
            {
                await OutPipe.LogWarning($"Could not fully clear {outputDirectory}: {e}");
            }
        }

        await OutPipe.LogMessage($"Creating {outputDirectory.FullName}");
        outputDirectory.Create();

        await Run(
            $"--headless --export-release \"{buildTarget.GodotBuildTemplateName}\" \"{Path.Join(outputDirectory.FullName, buildTarget.GetExeName(exeNameWithoutExtension))}\" --path \"{projectDirectory}\"");
    }

    public DirectoryInfo GetOutputDirectoryForProject(string projectDirectory, BuildTarget buildTarget)
    {
        return new DirectoryInfo(Path.Join("./", ".build", projectDirectory, buildTarget.ShortHandName));
    }

    public async Task<string> GetVersion()
    {
        return await RunAndGetOutput("--version");
    }
}