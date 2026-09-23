using Newtonsoft.Json;

namespace SecretPlanCore.Configuration;

public abstract class Config
{
    protected Config()
    {
        InstanceInfo = GenerateInstanceInfo();
    }

    [JsonProperty("config_info")]
    public ConfigInstanceInfo InstanceInfo { get; set; }
    /// <summary>
    ///     Runtime property that describes where this config came from originally
    /// </summary>
    [JsonIgnore]
    public ConfigOrigin Origin { get; set; }

    private ConfigInstanceInfo GenerateInstanceInfo()
    {
        return new ConfigInstanceInfo(
            ConfigServer.Instance.GenerateInstanceId(),
            ConfigServer.Instance.TypeIdFromType(GetType()));
    }

    public override string ToString()
    {
        return InstanceInfo.ToString();
    }

    public SerializedConfig Serialize(string filePath)
    {
        return new SerializedConfig(filePath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public uint Uid()
    {
        return InstanceInfo.InstanceId;
    }

    public virtual GeneratedEnum<uint>? CodeGenerateEnum()
    {
        return null;
    }
}