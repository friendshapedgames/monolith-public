using System.Collections;
using System.Reflection;
using System.Text;
using ControlRoomLib.Core;
using JetBrains.Annotations;
using SecretPlanCore.Configuration;
using SecretPlanCore.Core;
using SecretPlanGodot.Configuration;

namespace ControlRoomLib.Missions;

[UsedImplicitly]
public class ConfigManagement : Mission
{
    public ConfigManagement(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var command = PositionalArgs.Get(0, "Command")
            .ParseAsSpecificString("list", "create", "add", "renormalize", "read", "catalogue", "copy", "rename");

        await OutPipe.LogMessage($"Loading assembly: {DataAssemblyName()}");
        Assembly.Load(DataAssemblyName());

        switch (command)
        {
            case "list":
                await ListConfigs();
                break;
            case "rename":
                await RenameConfig(PositionalArgs.Get(1, "Old Config").ParseAsString(),
                    PositionalArgs.Get(2, "New Name").ParseAsString());
                break;
            case "create" or "add":
                await CreateConfig(PositionalArgs.Get(1, "Type Name").ParseAsString(),
                    PositionalArgs.Get(2, "Instance Name").ParseAsString());
                break;
            case "renormalize":
                await RenormalizeConfigs();
                break;
            case "read":
                await ReadConfig(PositionalArgs.Get(1, "Instance Name").ParseAsString());
                break;
            case "copy":
                await CopyConfig(PositionalArgs.Get(1, "Existing Instance Name").ParseAsString(),
                    PositionalArgs.Get(2, "New Instance Name").ParseAsString());
                break;
            case "catalogue":
                await WriteConfigCatalogue();
                break;
        }
    }

    private async Task RenameConfig(string oldName, string newName)
    {
        await SetupConfigServer(MissionVariables.GameDirectoryFiles());
        
        var searchResult = ConfigServer.Instance.SearchForInstance<Config>(oldName).ToList();
        if (searchResult.Count == 0)
        {
            throw new MissionFailedException($"Could not find a config matching {oldName}");
        }

        var oldConfig = searchResult.First();
        var gameDirectoryFiles = MissionVariables.GameDirectoryFiles();

        if (!ConfigServer.Instance.TryRenameInstance(oldConfig, newName, gameDirectoryFiles))
        {
            throw new Exception("Rename failed!");
        }
        
        await OutPipe.LogMessage($"Renamed {oldName} to {newName}");

        await WriteConfigCatalogue();
    }

    private async Task WriteConfigCatalogue()
    {
        await SetupConfigServer(MissionVariables.GameDirectoryFiles());

        // Get the data assembly (eg: ./Data/BirdGameData)
        var dataProjectFiles = new RealFileSystem(Path.Join("Data", DataAssemblyName()));
        var gameDirectoryFiles = MissionVariables.GameDirectoryFiles();
        ConfigServer.Instance.WriteCatalogue(dataProjectFiles, gameDirectoryFiles);
    }

    private string DataAssemblyName()
    {
        return MissionVariables.GameDirectory + "Data";
    }

    private async Task CopyConfig(string path, string newName)
    {
        await SetupConfigServer(MissionVariables.GameDirectoryFiles());
        var instanceId = GetInstanceIdFromName(path);

        var instance = ConfigServer.Instance.GetInstanceUntyped(instanceId ?? 0);
        if (instance == null)
        {
            throw new MissionFailedException($"Could not find instance with id {instanceId}");
        }

        var newInstance = ConfigServer.Instance.Duplicate(instance, newName);

        if (newInstance == null)
        {
            throw new MissionFailedException($"Failed to duplicate {instance}, got null");
        }

        WriteInstance(newName, newInstance);
        await WriteConfigCatalogue();
    }

    private void WriteInstance(string name, Config instance)
    {
        var typeId = instance.InstanceInfo.TypeId;
        if (!name.StartsWith(typeId))
        {
            name = $"{typeId}_{name}";
        }

        instance.Serialize($"{name}.json").WriteToFile(MissionVariables.GameDirectoryFiles().GetDirectory("Config"));
    }

    private async Task ReadConfig(string path)
    {
        await SetupConfigServer(MissionVariables.GameDirectoryFiles());
        var instanceId = GetInstanceIdFromName(path);

        if (!instanceId.HasValue)
        {
            throw new MissionFailedException($"Could not find an instance id for {path}");
        }

        var instance = ConfigServer.Instance.GetInstanceUntyped(instanceId.Value);

        if (instance == null)
        {
            throw new MissionFailedException(
                $"Found id {instanceId} but somehow this didn't resolve to a config, maybe cache is empty?");
        }

        foreach (var member in instance.GetType()
                     .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var type = Reflection.GetPropertyOrFieldType(member);

            if (type == null)
            {
                continue;
            }

            var value = Reflection.GetPropertyOrFieldValue(member, instance);

            if (Reflection.IsArrayOrList(type))
            {
                if (value is IEnumerable collection)
                {
                    var stringBuilder = new StringBuilder();

                    var enumerator = collection.GetEnumerator();
                    using var disposableEnumerator = enumerator as IDisposable;

                    stringBuilder.Append("[");
                    var hasNext = enumerator.MoveNext();
                    while (hasNext)
                    {
                        stringBuilder.Append(Stringify(enumerator.Current));
                        hasNext = enumerator.MoveNext();

                        if (hasNext)
                        {
                            stringBuilder.Append(", ");
                        }
                    }

                    stringBuilder.Append("]");

                    value = stringBuilder.ToString();
                }
            }
            else
            {
                value = Stringify(value);
            }

            await OutPipe.LogMessage(member.Name + ": " + value);
        }
    }

    private uint? GetInstanceIdFromName(string nameOrPath)
    {
        var name = new FileInfo(nameOrPath).Name.RemoveFileExtension();

        foreach (var config in ConfigServer.Instance.GetAllInstances())
        {
            if (config.InstanceInfo.Name.Contains(name))
            {
                return config.InstanceInfo.InstanceId;
            }
        }

        return null;
    }

    private string Stringify(object? value)
    {
        if (value == null)
        {
            return "(null)";
        }

        if (Reflection.HasUnderlyingType(value.GetType(), typeof(IResourceReference)))
        {
            var x = value as IResourceReference;
            return x?.Path ?? string.Empty;
        }

        return value.ToString()!;
    }

    private async Task RenormalizeConfigs()
    {
        var files = MissionVariables.GameDirectoryFiles();

        await SetupConfigServer(MissionVariables.GameDirectoryFiles());
        ConfigServer.Instance.WriteAllConfigs(files);
    }

    private async Task CreateConfig(string typeId, string name)
    {
        await SetupConfigServer(MissionVariables.GameDirectoryFiles());

        var newInstance = ConfigServer.Instance.CreateInstance(typeId, name, "Config");

        if (newInstance == null)
        {
            throw new MissionFailedException($"Failed to create instance of Type ID {typeId}");
        }

        WriteInstance(name, newInstance);
        await WriteConfigCatalogue();
    }

    private async Task ListConfigs()
    {
        await SetupConfigServer(MissionVariables.GameDirectoryFiles());

        foreach (var config in ConfigServer.Instance.GetAllInstances())
        {
            await OutPipe.LogMessage("- " + config);
        }
    }

    public static async Task SetupConfigServer(IFileSystem gameDirectoryFiles)
    {
        await OutPipe.LogMessage("Loading Configs into memory");
        ConfigServer.Clear();
        foreach (var filePath in gameDirectoryFiles.GetFilesAt(".", ConfigServer.FileExtensionNoDot))
        {
            if (filePath.StartsWith(".godot"))
            {
                continue;
            }

            ConfigServer.Instance.LoadFromJsonUntyped(filePath, await gameDirectoryFiles.ReadFileAsync(filePath), true);
        }
    }
}