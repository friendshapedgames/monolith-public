using System.Diagnostics.Contracts;
using System.Text;
using System.Text.RegularExpressions;

namespace ControlRoomLib.Core;

public static class OutPipe
{
    private static readonly Regex _ansiRegex = new(
        @"\x1B\[[0-9;]*[A-Za-z]",
        RegexOptions.Compiled
    );
    
    private static StreamWriter? _logFile;

    private static readonly SemaphoreSlim _consoleLock = new(1, 1);
    private static readonly SemaphoreSlim _logLock = new(1, 1);

    public static async Task LogMessage(string? message)
    {
        await LogInternal("💬", message);
    }
    
    public static async Task LogRunCommand(string message, LogLevel logLevel = LogLevel.ConsoleAndLogFile)
    {
        await LogInternal("💻", message, logLevel);
    }
    
    public static async Task LogWarning(string message)
    {
        await LogInternal("🔶", message);
    }
    
    public static async Task LogError(string message)
    {
        await LogInternal("💥", message);
    }

    private static async Task LogInternal(string prefix, string? message, LogLevel logLevel = LogLevel.ConsoleAndLogFile)
    {
        var composedString = $"{prefix} {message}\n";
        var charArray = composedString.ToCharArray();
        await WriteDirect(charArray, charArray.Length, null, logLevel);
    }

    public static async Task WriteDirect(char[] buffer, int read, StringBuilder? stringBuilder, LogLevel logLevel)
    {
        stringBuilder?.Append(buffer, 0, read);

        if (logLevel == LogLevel.ConsoleAndLogFile)
        {
            await _consoleLock.WaitAsync();
            try
            {
                await Console.Out.WriteAsync(buffer, 0, read);
            }
            finally
            {
                _consoleLock.Release();
            }
        }
        
        if (_logFile != null)
        {
            await _logLock.WaitAsync();
            try
            {
                await _logFile.WriteAsync(buffer, 0, read);
            }
            finally
            {
                _logLock.Release();
            }
        }
    }

    public static void Close()
    {
        _logFile?.Close();
    }

    public static void Open(string path)
    {
        _logFile = new StreamWriter(path, true, encoding: new UTF8Encoding(false));
    }

    [Pure]
    public static async Task<string?> Prompt(string message, string? defaultAnswer = null)
    {
        await LogInternal("❓", message + $" (type answer to continue{(defaultAnswer == null ? "" : $", default answer: {defaultAnswer}")})");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultAnswer;
        }
        return input.Trim();
    }
    
    [Pure]
    public static async Task<string?> ReplPrompt()
    {
        var chars = ">>> ".ToCharArray();
        await WriteDirect(chars, chars.Length, null, LogLevel.ConsoleAndLogFile);
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }
        return input.Trim();
    }
}