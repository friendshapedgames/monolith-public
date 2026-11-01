using System.Globalization;
using System.Reflection;

namespace SecretPlanCore.ArgumentParsing;

public static class CommandLineParameters
{
    public static T Parse<T>(Action<object[]> error, params string[] args) where T : new()
    {
        var result = new T();
        var properties = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = property.GetCustomAttribute<CommandLineParameterAttribute>();
            if (attribute != null && property.CanWrite)
            {
                properties[attribute.Id] = property;
            }
        }

        foreach (var arg in args)
        {
            if (!arg.StartsWith("--"))
            {
                continue;
            }

            var body = arg.Substring(2);
            var equalsIndex = body.IndexOf('=');
            var key = equalsIndex >= 0 ? body.Substring(0, equalsIndex) : body;
            var rawValue = equalsIndex >= 0 ? StripQuotes(body.Substring(equalsIndex + 1)) : null;

            // Unknown flags are ignored, other systems (eg: LocalClient) read from the same args
            if (!properties.TryGetValue(key, out var property))
            {
                continue;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (rawValue == null)
            {
                if (propertyType == typeof(bool))
                {
                    property.SetValue(result, true);
                }
                else
                {
                    error([$"Flag '--{key}' expects a value, eg: --{key}=<{propertyType.Name}>"]);
                }

                continue;
            }

            if (TryConvert(rawValue, propertyType, out var value))
            {
                property.SetValue(result, value);
            }
            else
            {
                error([$"Flag '--{key}' could not parse '{rawValue}' as {propertyType.Name}"]);
            }
        }

        return result;
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private static bool TryConvert(string rawValue, Type type, out object? value)
    {
        value = null;

        if (type == typeof(string))
        {
            value = rawValue;
            return true;
        }

        if (type.IsEnum)
        {
            return Enum.TryParse(type, rawValue, true, out value);
        }

        try
        {
            value = Convert.ChangeType(rawValue, type, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }
}