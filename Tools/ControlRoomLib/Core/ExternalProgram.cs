using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;

namespace ControlRoomLib.Core;

public class ExternalProgram
{
    private readonly string _invokeString;
    public LogLevel LogLevel { get; set; }

    public ExternalProgram(string invokeString, string? workingDirectory, string? nickname = null)
    {
        _invokeString = invokeString;
        WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
        Nickname = nickname ?? invokeString;

        if (!Directory.Exists(WorkingDirectory))
        {
            Directory.CreateDirectory(WorkingDirectory);
        }
    }

    protected string WorkingDirectory { get; }

    public int MostRecentExitCode { get; private set; }

    private string Nickname { get; }

    [Pure]
    public async Task<string> RunAndGetOutput(params string[] args)
    {
        StringBuilder stringBuilder = new();
        await RunInternal(args, stringBuilder);
        return NormalizeCapturedOutput(stringBuilder.ToString());
    }

    public async Task Run(params string[] args)
    {
        await RunInternal(args, null);
    }

    private async Task RunInternal(string[] args, StringBuilder? stringBuilder)
    {
        var process = await BuildAndStartProcess(args);

        Task ReadToConsoleAndCapture(StreamReader reader)
        {
            var buffer = new char[1024];

            return Task.Run((Func<Task?>)(async () =>
            {
                int read;
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await OutPipe.WriteDirect(buffer, read, stringBuilder, LogLevel);
                }
            }));
        }

        await Task.WhenAll(
            ReadToConsoleAndCapture(process.StandardOutput),
            ReadToConsoleAndCapture(process.StandardError)
        );
        await process.WaitForExitAsync();
        MostRecentExitCode = process.ExitCode;
        if (MostRecentExitCode == 0)
        {
            await OutPipe.LogRunCommand($"🆗 {Nickname} finished", LogLevel);
        }
        else
        {
            await OutPipe.LogRunCommand(
                $"🔶 {Nickname} finished with nonzero exit code: {MostRecentExitCode}", LogLevel);
        }
    }

    private async Task<Process> BuildAndStartProcess(string[] args)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _invokeString,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = WorkingDirectory
        };

        if (args.Length == 1)
        {
            processStartInfo.Arguments = args[0];
        }
        else
        {
            foreach (var arg in args)
            {
                processStartInfo.ArgumentList.Add(arg);
            }
        }

        await OutPipe.LogRunCommand(
            $"🟦 Running `{_invokeString} {string.Join(" ", args)}` at working directory {new DirectoryInfo(WorkingDirectory).FullName}", LogLevel);

        var process = new Process
        {
            StartInfo = processStartInfo
        };

        try
        {
            process.Start();
        }
        catch (Exception e)
        {
            throw new MissionFailedException($"Unable to run program {_invokeString} {args}", e);
        }

        return process;
    }

    private static string NormalizeCapturedOutput(string raw)
    {
        // Split by real newlines for structure
        var lines = raw.Replace("\r", "\n").Split('\n');

        var cleaned = new StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            cleaned.AppendLine(line);
        }

        return cleaned.ToString().Trim();
    }

    public bool WasSuccessful()
    {
        return MostRecentExitCode == 0;
    }
}