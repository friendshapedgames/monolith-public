using System.Text;
using ControlRoomLib.BaseMissions;
using Newtonsoft.Json;
using SecretPlanCore.Core;

namespace ControlRoomLib.Core;

public static class Platform
{
    public static SerializedState SerializedState { get; private set; } = new();

    public static string Shortcut(string shortcut, string? helpOnFail = null)
    {
        if (SerializedState.Shortcuts.TryGetValue(shortcut, out var value))
        {
            return value;
        }

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"Could not find shortcut: `{shortcut}`");

        if (helpOnFail != null)
        {
            stringBuilder.AppendLine(helpOnFail);
        }
        else
        {
            stringBuilder.AppendLine(
                $"hint: {ControlRoomConstants.GenerateMissionCommand<Shortcut>($"add {shortcut} <value>")}");
        }

        throw new MissionFailedException(stringBuilder.ToString());
    }

    public static void Startup()
    {
        if (File.Exists(ControlRoomConstants.StatePath))
        {
            var json = File.ReadAllText(ControlRoomConstants.StatePath);
            try
            {
                var state = JsonConvert.DeserializeObject<SerializedState>(json);
                if (state != null)
                {
                    SerializedState = state;
                }
            }
            catch (Exception exception)
            {
                throw new MissionFailedException("Failed to read state", exception);
            }
        }

        Console.OutputEncoding = Encoding.UTF8;
        Directory.CreateDirectory(ControlRoomConstants.LogsFolder);
        OutPipe.Open($"{ControlRoomConstants.LogsFolder}/{ControlRoomConstants.CurrentLogFile}");
    }

    public static void Shutdown()
    {
        File.WriteAllText(ControlRoomConstants.StatePath,
            JsonConvert.SerializeObject(SerializedState, Formatting.Indented));
        OutPipe.Close();
    }

    public static bool HasShortcut(string key)
    {
        if (SerializedState.Shortcuts.ContainsKey(key))
        {
            return true;
        }

        return false;
    }

    public static HostOperatingSystem GetHostOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return HostOperatingSystem.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return HostOperatingSystem.MacOs;
        }

        if (OperatingSystem.IsLinux())
        {
            return HostOperatingSystem.Linux;
        }

        return HostOperatingSystem.Unknown;
    }
    
    public static async Task AttemptToRunMissionWithArgs(List<string> givenArgs, MissionVariables? overrideMissionVariables = null)
    {
        var desiredMissionName = givenArgs[0];

        // remove mission name
        givenArgs.RemoveAt(0);

        var missionTypes = Reflection.GetAllTypesThatDeriveFrom<Mission>();
        var missionDictionary = missionTypes.ToDictionary(a => a.Name.ToLower(), a => a);
        var queryResult = missionDictionary.Keys.Where(a => a.StartsWith(desiredMissionName.ToLower())).ToList();

        switch (queryResult.Count)
        {
            case 1:
            {
                var type = missionDictionary[queryResult.First()];
                var mission = await MissionDispatch.CreateMission(type, givenArgs);

                if (mission != null)
                {
                    if (overrideMissionVariables != null)
                    {
                        mission.MissionVariables = overrideMissionVariables;
                    }

                    await MissionDispatch.Execute(mission);
                }

                break;
            }
            case 0:
                await OutPipe.LogError($"No missions found matching {desiredMissionName}");
                break;
            default:
                await OutPipe.LogError(
                    $"Found {queryResult.Count} matching missions:\n{string.Join("\n", queryResult.Select(a => "- " + missionDictionary[a].Name))}");
                await OutPipe.LogError("Please run again with more specific mission name");
                break;
        }
    }
}