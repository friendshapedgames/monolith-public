namespace SecretPlanCore.ArgumentParsing;

[AttributeUsage(AttributeTargets.Property)]
public class CommandLineParameterAttribute : Attribute
{
    public CommandLineParameterAttribute(string id)
    {
        Id = id;
    }

    public string Id { get; }
}