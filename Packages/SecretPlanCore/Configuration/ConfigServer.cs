using System.Text;
using SecretPlanCore.Core;

namespace SecretPlanCore.Configuration;

public class ConfigServer
{
    public const string FileExtensionNoDot = "json";
    private static readonly NoiseBasedRng _random = new(0);

    private readonly HashSet<int> _assignabilityTable = new();

    private readonly Dictionary<uint, Config?> _instances = new();

    /// <summary>
    ///     Table of all type ids for all config types, usually this is a string representation of the config name, sometimes
    ///     it's overwritten in the ConfigForceTypeId Attribute.
    /// </summary>
    private readonly BidirectionalDictionary<Type, string> _typeIds = new();

    /// <summary>
    ///     True if a new assembly has been loaded since we last scanned
    /// </summary>
    private bool _hasNewAssemblyLoadedSinceLastScan = true;

    private ConfigServer()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        ForceScanForConfigTypes();
    }

    public static ConfigServer Instance { get; private set; } = new();

    private void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        _hasNewAssemblyLoadedSinceLastScan = true;
    }

    private void OnDestroy()
    {
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
    }

    public IEnumerable<Config> GetAllInstances()
    {
        foreach (var value in _instances.Values)
        {
            if (value != null)
            {
                yield return value;
            }
        }
    }

    public IEnumerable<T> GetAllInstancesOfType<T>()
    {
        foreach (var item in GetAllInstances())
        {
            if (item is T casted)
            {
                yield return casted;
            }
        }
    }

    public IEnumerable<Config> GetAllInstancesOfTypeNonGeneric(Type type)
    {
        foreach (var item in GetAllInstances())
        {
            if (type.IsInstanceOfType(item))
            {
                yield return item;
            }
        }
    }

    public Config? GetInstanceUntyped(uint instanceId)
    {
        return _instances.GetValueOrDefault(instanceId);
    }

    public T? GetInstance<T>(uint instanceId) where T : Config
    {
        return GetInstanceUntyped(instanceId) as T;
    }

    public T GetInstanceOrDefault<T>(uint instanceId) where T : Config, new()
    {
        return GetInstance<T>(instanceId) ?? new T();
    }

    /// <summary>
    ///     Load JSON into object when we're not sure what type we're looking at. If the returned value is not null it will be
    ///     the correct underlying type.
    /// </summary>
    public Config? LoadFromJsonUntyped(string configName, string jsonText, bool shouldCache)
    {
        var typeId = GetTypeIdFromJson(jsonText);

        if (typeId == null)
        {
            return null;
        }

        var type = TypeFromId(typeId);

        if (type == null || type == typeof(UnknownConfig))
        {
            return null;
        }

        return LoadFromJsonInternal(configName, jsonText, type, shouldCache);
    }

    public string? GetTypeIdFromJson(string jsonText)
    {
        return LoadFromJson<UnknownConfig>("Temp", jsonText, false)?.InstanceInfo.TypeId;
    }

    /// <summary>
    ///     Load JSON into object when we already know what type we want
    /// </summary>
    public T? LoadFromJson<T>(string configName, string jsonText, bool shouldCache) where T : Config
    {
        return LoadFromJsonInternal(configName, jsonText, typeof(T), shouldCache) as T;
    }

    private Config? LoadFromJsonInternal(string configName, string jsonText, Type type, bool shouldCache)
    {
        var result = JsonHelpers.DeserializeSafe(jsonText, type);

        if (result is not Config configInstance)
        {
            return null;
        }

        // Set instance name for convenience
        configInstance.InstanceInfo = configInstance.InstanceInfo with { Name = configName };

        if (shouldCache)
        {
            CacheInstanceId(configInstance);
        }

        return configInstance;
    }

    private void CacheInstanceId(Config config)
    {
        var instanceId = config.InstanceInfo.InstanceId;
        if (_instances.TryGetValue(instanceId, out var foundInstance))
        {
            if (config != foundInstance)
            {
                LogError(
                    $"InstanceId collision! {config.InstanceInfo.Name} is claiming ID [{instanceId}], which overwrites {foundInstance}.");
            }
        }

        _instances[instanceId] = config;
    }

    private static void LogError(string message)
    {
        MessageLogged?.Invoke(LogType.Error, message);
    }

    public static event Action<LogType, string>? MessageLogged;

    /// <summary>
    ///     Scan all loaded assemblies for config types
    /// </summary>
    private void ForceScanForConfigTypes()
    {
        if (!_hasNewAssemblyLoadedSinceLastScan)
        {
            // no work to do!
            return;
        }

        foreach (var incomingType in Reflection.GetAllTypesThatDeriveFrom<Config>())
        {
            if (incomingType.IsAbstract)
            {
                continue;
            }

            var id = SerializedTypeIdAttribute.CalculateTypeId(incomingType);
            var alreadyHasId = _typeIds.ContainsValue(id);

            if (alreadyHasId)
            {
                var existingType = _typeIds.GetKeyFromValue(id);
                if (existingType == incomingType)
                {
                    // We already have this type, skip it
                    continue;
                }

                throw new Exception(
                    $"TypeId collision! {incomingType} and {existingType} both think they should have id {id}");
            }

            _typeIds.AddEntry(incomingType, id);
        }

        _hasNewAssemblyLoadedSinceLastScan = false;
    }

    public IEnumerable<string> AllTypeIds()
    {
        return _typeIds.Values();
    }

    /// <summary>
    ///     Gets (or generates) a type ID from a type
    /// </summary>
    /// <returns></returns>
    public string TypeIdFromType(Type type)
    {
        var foundValue = _typeIds.GetValueFromKey(type);

        if (foundValue == null)
        {
            var calculatedValue = SerializedTypeIdAttribute.CalculateTypeId(type);
            _typeIds.AddEntry(type, calculatedValue);
            return calculatedValue;
        }

        return foundValue;
    }

    /// <summary>
    ///     Gets a type from a type ID
    /// </summary>
    /// <param name="typeId"></param>
    public Type? TypeFromId(string typeId)
    {
        var type = _typeIds.GetKeyFromValue(typeId);
        if (type == null)
        {
            ForceScanForConfigTypes();
            type = _typeIds.GetKeyFromValue(typeId);
        }

        if (type == null)
        {
            return null;
        }

        return type;
    }

    public string CreateFileName(Type type, string simpleName)
    {
        return $"{TypeIdFromType(type)}_{simpleName}.json";
    }

    /// <summary>
    ///     Creates an instance that can be used in-memory or written to disk
    /// </summary>
    public Config? CreateInstance(Type type, string simpleName, string? path)
    {
        if (Activator.CreateInstance(type) is not Config configInstance)
        {
            return null;
        }

        var fileName = CreateFileName(type, simpleName);
        if (!string.IsNullOrEmpty(path))
        {
            fileName = $"{path}/{fileName}";
        }

        configInstance.InstanceInfo = configInstance.InstanceInfo with { Name = fileName };

        CacheInstanceId(configInstance);
        return configInstance;
    }

    public Config? CreateInstance(string typeId, string simpleName, string? path)
    {
        var type = TypeFromId(typeId);

        if (type == null)
        {
            Console.Error.WriteLine($"Type ID {typeId} did not resolve to a known type");
            return null;
        }

        return CreateInstance(type, simpleName, path);
    }

    public bool AreSame(Config a, Config b)
    {
        return a.Uid() == b.Uid();
    }

    public T? CreateInstance<T>(string instanceName) where T : Config
    {
        return CreateInstance(typeof(T), instanceName, null) as T;
    }

    public uint GenerateInstanceId()
    {
        // Pretty terrible hash, but good enough. If a collision happens we'll get an obvious error message and we can just manually tweak the id
        return (uint)(Hashing.Hash64(_random.NextUInt()) + Hashing.Hash64((uint)TimeUtilities.TimeNowMilliseconds()));
    }

    /// <summary>
    ///     WARNING: SLOW!!
    ///     This is only meant to be used in-editor when duplicating configs
    /// </summary>
    public T? Duplicate<T>(T original, string newName) where T : Config
    {
        var type = TypeFromId(original.InstanceInfo.TypeId);

        if (type == null)
        {
            return null;
        }

        var clone = JsonHelpers.DeserializeSafe(original.Serialize(string.Empty).JsonContent, type) as T;

        if (clone == null)
        {
            return null;
        }

        var directory = original.InstanceInfo.Directory();

        var fileName = CreateFileName(type, newName);
        if (!string.IsNullOrEmpty(directory))
        {
            fileName = $"{directory}/{fileName}";
        }
        
        clone.InstanceInfo = clone.InstanceInfo with
        {
            InstanceId = GenerateInstanceId(), Name = fileName
        };

        CacheInstanceId(clone);

        return clone;
    }

    /// <summary>
    ///     WARNING: SLOW!!
    ///     This is only meant to be used in-editor when duplicating configs
    /// </summary>
    public Config? DuplicateUntyped(Config original)
    {
        var type = TypeFromId(original.InstanceInfo.TypeId);

        if (type == null)
        {
            return null;
        }

        var clone = JsonHelpers.DeserializeSafe(original.Serialize(string.Empty).JsonContent, type) as Config;
        if (clone == null)
        {
            return null;
        }

        clone.InstanceInfo = clone.InstanceInfo with { InstanceId = GenerateInstanceId() };
        return clone;
    }

    /// <summary>
    ///     Forget all config data (needs to be manually reloaded)
    /// </summary>
    public static void Clear()
    {
        Instance = new ConfigServer();
        Instance.ForceScanForConfigTypes();
    }

    public void WriteAllConfigsTxt(IFileSystem files)
    {
        var instanceNames = GetAllInstances().Where(a => a.Origin.IsBaseGameConfig()).Select(a => a.InstanceInfo.Name)
            .ToList();
        instanceNames.Sort();
        
        files.WriteToFile("AllConfigs.txt", instanceNames.ToArray());
    }

    public string CodeGenerateEnumsFile()
    {
        var enumFileContent = new StringBuilder();

        var enumTypeNameToConfigType = new Dictionary<string, Type>();

        // Write file header
        enumFileContent.AppendLine(GeneratedEnum<uint>.GenerateSourceCodeHeader());

        // Create enum for each config type
        foreach (var typeId in AllTypeIds())
        {
            var name = typeId + "Enum";
            var configTypeEnum = new GeneratedEnum<uint>(name);
            var configType = TypeFromId(typeId);

            if (configType == null)
            {
                continue;
            }

            enumTypeNameToConfigType.Add(name, configType);
            var fullTypeName = configType.FullName;

            CodeGenerateExtensionMethodsAndAddNoneEntry(configTypeEnum, fullTypeName, false);

            foreach (var config in GetAllInstances())
            {
                if (config.InstanceInfo.TypeId == typeId)
                {
                    configTypeEnum.AddEntry(config.InstanceInfo.ShortName(), config.InstanceInfo.InstanceId);
                }
            }

            enumFileContent.AppendLine(configTypeEnum.GenerateSourceCodeBody());
        }

        foreach (var instance in GetAllInstances())
        {
            var instanceEnum = instance.CodeGenerateEnum();
            if (instanceEnum != null)
            {
                enumFileContent.AppendLine(instanceEnum.GenerateSourceCodeBody());
            }
        }

        // Create enum for ConfigTypes (one enum value for every type of config there this)
        var allConfigTypesEnumName = "ConfigTypeEnum";
        var allConfigTypesEnum = new GeneratedEnum<uint>(allConfigTypesEnumName);
        allConfigTypesEnum.AddEntry("None", 0);

        var typeIdsToHashes = new Dictionary<uint, string>();

        foreach (var typeId in AllTypeIds())
        {
            var id = (uint)ExNoise.SeedFromString(typeId);
            if (!typeIdsToHashes.TryAdd(id, typeId))
            {
                throw new Exception(
                    $"HASH COLLISION: Tried to assign unique ID {id} to config type {typeId} but {typeId} already has that ID");
            }

            allConfigTypesEnum.AddEntry(typeId, id);
        }

        enumFileContent.AppendLine(allConfigTypesEnum.GenerateSourceCodeBody());

        enumFileContent.AppendLine($"public static class {allConfigTypesEnumName}Extensions");
        enumFileContent.AppendLine("{");
        enumFileContent.AppendLine($"    public static Type? ReadType(this {allConfigTypesEnumName} configTypeEnum)");
        enumFileContent.AppendLine("    {");
        foreach (var (hash, typeId) in typeIdsToHashes)
        {
            enumFileContent.AppendLine(
                $"        if (configTypeEnum == {allConfigTypesEnumName}.{typeId}) {{ return {typeof(ConfigServer).FullName}.Instance.{nameof(TypeFromId)}(\"{typeId}\"); }}");
        }

        enumFileContent.AppendLine("        return null;");
        enumFileContent.AppendLine("    }");
        enumFileContent.AppendLine("}");
        enumFileContent.AppendLine();

        // Create enum for Any config (one big enum that has every id)
        var anyConfigEnumName = "AnyConfigEnum";
        enumTypeNameToConfigType.Add(anyConfigEnumName, typeof(Config));
        var allConfigEnum = new GeneratedEnum<uint>(anyConfigEnumName);
        CodeGenerateExtensionMethodsAndAddNoneEntry(allConfigEnum, typeof(Config).FullName, true);
        foreach (var config in GetAllInstances())
        {
            allConfigEnum.AddEntry(config.InstanceInfo.TypeId + config.InstanceInfo.ShortName(),
                config.InstanceInfo.InstanceId);
        }


        enumFileContent.AppendLine(allConfigEnum.GenerateSourceCodeBody());

        enumFileContent.AppendLine("public static class ConfigEnumTypeChecker");
        enumFileContent.AppendLine("{");
        enumFileContent.AppendLine("    private static bool HasGeneratedEnumTypeCache;");
        enumFileContent.AppendLine(
            "    private static readonly System.Collections.Generic.Dictionary<System.Type, System.Type> EnumTypeCache = new();");
        enumFileContent.AppendLine("    public static Type? GetConfigTypeFromEnumType(Type enumType)");
        enumFileContent.AppendLine("    {");
        enumFileContent.AppendLine("        return EnumTypeCache.GetValueOrDefault(enumType);");
        enumFileContent.AppendLine("    }");
        enumFileContent.AppendLine("    public static bool IsConfigEnumType(Type type)");
        enumFileContent.AppendLine("    {");
        enumFileContent.AppendLine(
            "        if (HasGeneratedEnumTypeCache) { return EnumTypeCache.ContainsKey(type); }");

        foreach (var (enumTypeName, configType) in enumTypeNameToConfigType)
        {
            enumFileContent.AppendLine(
                $"        EnumTypeCache.Add(typeof({enumTypeName}), typeof({configType.FullName}));");
        }

        enumFileContent.AppendLine("        HasGeneratedEnumTypeCache = true;");
        enumFileContent.AppendLine("        return EnumTypeCache.ContainsKey(type);");
        enumFileContent.AppendLine("    }");
        enumFileContent.AppendLine("}");

        return enumFileContent.ToString();
    }

    private static void CodeGenerateExtensionMethodsAndAddNoneEntry(GeneratedEnum<uint> generatedEnum,
        string? fullTypeName,
        bool skipReadOrDefault)
    {
        generatedEnum.AddEntry("None", 0);

        generatedEnum.AddExtensionMethod(builder =>
        {
            var getConfigMethodName =
                $"{typeof(ConfigServer).FullName}.{nameof(Instance)}.{nameof(GetInstance)}";
            builder.AppendLine($"    public static {fullTypeName}? Read(this {generatedEnum.EnumTypeName} self)");
            builder.AppendLine("    {");
            builder.AppendLine($"        var valueAsInt = ({generatedEnum.BackingIntType.FullName}) self;");
            builder.AppendLine($"        return {getConfigMethodName}<{fullTypeName}>(valueAsInt);");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine(
                $"    public static bool HasValue(this {generatedEnum.EnumTypeName} self)");
            builder.AppendLine("    {");
            builder.AppendLine($"        var valueAsInt = ({generatedEnum.BackingIntType.FullName}) self;");
            builder.AppendLine($"        var value = {getConfigMethodName}<{fullTypeName}>(valueAsInt);");
            builder.AppendLine("        if (value == null)");
            builder.AppendLine("        {");
            builder.AppendLine("            return false;");
            builder.AppendLine("        }");
            builder.AppendLine("        return true;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine(
                $"    public static {generatedEnum.EnumTypeName} UidAsEnum(this {fullTypeName} self)");
            builder.AppendLine("    {");
            builder.AppendLine($"        return ({generatedEnum.EnumTypeName}) self.Uid();");
            builder.AppendLine("    }");
            builder.AppendLine();

            if (!skipReadOrDefault)
            {
                builder.AppendLine(
                    $"    public static {fullTypeName} ReadOrDefault(this {generatedEnum.EnumTypeName} self)");
                builder.AppendLine("    {");
                builder.AppendLine($"        var valueAsInt = ({generatedEnum.BackingIntType.FullName}) self;");
                builder.AppendLine($"        var value = {getConfigMethodName}<{fullTypeName}>(valueAsInt);");
                builder.AppendLine("        if (value == null)");
                builder.AppendLine("        {");
                builder.AppendLine($"            var result = new {fullTypeName}();");
                builder.AppendLine(
                    $"            result.{nameof(Config.InstanceInfo)} = result.{nameof(Config.InstanceInfo)} with {{ {nameof(ConfigInstanceInfo.InstanceId)} = 0 }};");
                builder.AppendLine("            return result;");
                builder.AppendLine("        }");
                builder.AppendLine("        return value;");
                builder.AppendLine("    }");
            }
        });
    }

    public void CodeWriteAllConfigsEnum(IFileSystem dataProjectFiles)
    {
        const string enumFilePath = "Generated/ConfigEnums.cs";
        dataProjectFiles.DeleteFile(enumFilePath);
        dataProjectFiles.WriteToFile(enumFilePath, CodeGenerateEnumsFile());
    }

    public void WriteAllConfigs(IFileSystem gameDirectoryFiles)
    {
        foreach (var config in GetAllInstances())
        {
            var fileSystem = config.Origin.SourceFileSystem ?? gameDirectoryFiles;
            WriteConfig(fileSystem, config);
        }
    }

    public void WriteConfig(IFileSystem gameDirectoryFiles, Config config)
    {
        var serialized = config.Serialize(config.InstanceInfo.Name);
        serialized.WriteToFile(gameDirectoryFiles);
    }

    public void WriteCatalogue(RealFileSystem dataProjectFiles, IFileSystem gameDirectoryFiles)
    {
        CodeWriteAllConfigsEnum(dataProjectFiles);
        WriteAllConfigsTxt(gameDirectoryFiles);
    }

    public bool IsNameAvailable(string fileName)
    {
        foreach (var instance in _instances.Values)
        {
            if (instance == null)
            {
                continue;
            }

            if (instance.InstanceInfo.Name == fileName)
            {
                return false;
            }
        }

        return true;
    }

    public IEnumerable<T> SearchForInstance<T>(string query) where T : Config
    {
        return FuzzyUtilities.Rank(query, GetAllInstancesOfType<T>(), a => a.InstanceInfo.Name);
    }

    public T? SearchForInstanceFirstOrNull<T>(string query) where T : Config
    {
        return SearchForInstance<T>(query).FirstOrDefault();
    }

    public static string Slugify(string name)
    {
        return name.Replace(" ", "_").Replace("-", "_").ToLower();
    }

    public void DoPreload()
    {
        foreach (var configWithPreload in Instance.GetAllInstancesOfType<IHasPreloadStep>())
        {
            configWithPreload.PreloadStep();
        }
    }

    public bool TryRenameInstance(Config instanceToRename, string newSimpleName, IFileSystem gameDirectoryFiles)
    {
        var destinationNameIsValid = Instance.GetAllInstances().All(a => a.InstanceInfo.Name != newSimpleName);
        if (!destinationNameIsValid)
        {
            return false;
        }

        var oldFileName = instanceToRename.InstanceInfo.Name;
        var path = instanceToRename.InstanceInfo.Directory();

        if (!string.IsNullOrEmpty(path))
        {
            path += "/";
        }

        var newFileName = path + CreateFileName(instanceToRename.GetType(), newSimpleName);
        
        instanceToRename.InstanceInfo = instanceToRename.InstanceInfo with { Name = newFileName };

        instanceToRename.Serialize(newFileName).WriteToFile(gameDirectoryFiles);
        gameDirectoryFiles.DeleteFile(oldFileName);

        EditorConfigChanged?.Invoke(instanceToRename.InstanceInfo.InstanceId);

        return true;
    }

    public void DeleteInstance(Config instanceToRemove, IFileSystem gameDirectoryFiles)
    {
        var oldFileName = instanceToRemove.InstanceInfo.Name;
        gameDirectoryFiles.DeleteFile(oldFileName);

        EditorConfigChanged?.Invoke(instanceToRemove.InstanceInfo.InstanceId);
    }

    /// <summary>
    ///     Fires when a Config is deleted or renamed. Only relevant in an editor context.
    /// </summary>
    public event Action<uint>? EditorConfigChanged;

    /// <summary>
    ///     Returns true if the otherType can be assigned to the desired type (aka: the desired type is a parent of other type)
    /// </summary>
    public bool IsTypeIdAssignableTo(string desiredTypeName, string otherTypeName)
    {
        if (desiredTypeName == otherTypeName)
        {
            return true;
        }

        var hash = HashCode.Combine(desiredTypeName, otherTypeName);

        if (_assignabilityTable.Contains(hash))
        {
            return true;
        }

        var desiredType = _typeIds.GetKeyFromValue(desiredTypeName);
        var otherType = _typeIds.GetKeyFromValue(otherTypeName);

        if (otherType != null && otherType.IsAssignableTo(desiredType))
        {
            // cache this answer for later
            _assignabilityTable.Add(hash);
            return true;
        }

        return false;
    }
}