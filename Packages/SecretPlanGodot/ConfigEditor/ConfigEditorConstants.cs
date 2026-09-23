using SecretPlanCore.Configuration;

namespace SecretPlanGodot.ConfigEditor;

public static class ConfigEditorConstants
{
    public const string EmptyFieldText = "(empty)";

    public static string UidToConfigName(uint uid, bool includeTypeName)
    {
        if (uid == 0)
        {
            return string.Empty;
        }

        var instance = ConfigServer.Instance.GetInstanceUntyped(uid);

        if (instance == null)
        {
            return $"Unknown-{uid}";
        }

        var name = instance.InstanceInfo.NameWithoutPathOrExtensionOrType();

        if (!includeTypeName)
        {
            return name;
        }

        return name + $" ({instance.GetType().Name})";
    }
}