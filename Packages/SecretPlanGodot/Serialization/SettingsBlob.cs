using System.Reflection;
using Newtonsoft.Json;
using SecretPlanCore.Core;

namespace SecretPlanGodot.Serialization;

public abstract record SettingsBlob
{
    public float GetFloat(PropertyInfo? propertyInfo)
    {
        if (propertyInfo?.GetValue(this) is float result)
        {
            return result;
        }

        throw new Exception($"Could not extract float value from {propertyInfo?.Name ?? "null"}");
    }

    public string GetString(PropertyInfo? propertyInfo)
    {
        if (propertyInfo?.GetValue(this) is string result)
        {
            return result;
        }

        throw new Exception($"Could not extract string value from {propertyInfo?.Name ?? "null"}");
    }

    private void SetValue<T>(PropertyInfo? propertyInfo, T value)
    {
        if (propertyInfo == null)
        {
            throw new Exception("Attempted to change nonexistent setting");
        }

        propertyInfo.SetValue(this, value);
        ValueChanged?.Invoke(propertyInfo.Name);
    }

    public event Action<string>? ValueChanged;

    public void SetObject(PropertyInfo? propertyInfo, object value)
    {
        SetValue(propertyInfo, value);
    }
    
    public void SetFloat(PropertyInfo? propertyInfo, float value)
    {
        SetValue(propertyInfo, value);
    }

    public void SetBool(PropertyInfo? propertyInfo, bool value)
    {
        SetValue(propertyInfo, value);
    }

    public void SetEnumInt(PropertyInfo? propertyInfo, int value)
    {
        SetValue(propertyInfo, value);
    }

    public void SetEnumUInt(PropertyInfo? propertyInfo, uint value)
    {
        SetValue(propertyInfo, value);
    }

    public void SetString(PropertyInfo? propertyInfo, string value)
    {
        SetValue(propertyInfo, value);
    }

    public bool GetBool(PropertyInfo? propertyInfo)
    {
        if (propertyInfo?.GetValue(this) is bool result)
        {
            return result;
        }

        throw new Exception($"Could not extract bool value from {propertyInfo?.Name ?? "null"}");
    }


    public int GetEnumValueAsInt(Type type, PropertyInfo? propertyInfo)
    {
        var value = propertyInfo?.GetValue(this);

        if (value is Enum enumValue)
        {
            return Convert.ToInt32(enumValue);
        }

        throw new Exception($"Could not extract {type.Name} value from {propertyInfo?.Name ?? "null"}");
    }

    public uint GetEnumValueAsUInt(Type type, PropertyInfo? propertyInfo)
    {
        var value = propertyInfo?.GetValue(this);

        if (value is Enum enumValue)
        {
            return Convert.ToUInt32(enumValue);
        }

        throw new Exception($"Could not extract {type.Name} value from {propertyInfo?.Name ?? "null"}");
    }

    public T GetEnumValue<T>(PropertyInfo propertyInfo) where T : Enum
    {
        var value = propertyInfo?.GetValue(this);

        if (value is T valueAsT)
        {
            return valueAsT;
        }

        throw new Exception($"Could not extract {typeof(T).Name} value from {propertyInfo?.Name ?? "null"}");
    }

    public void SetEnumValue<T>(PropertyInfo? propertyInfo, T value) where T : Enum
    {
        SetValue(propertyInfo, value);
    }

    public void InvokeValueChangedForSetting(string name)
    {
        ValueChanged?.Invoke(name);
    }

    /// <summary>
    ///     All properties on the concrete settings type that are serialized under an explicit [JsonProperty] name,
    ///     paired with that name.
    /// </summary>
    private IEnumerable<(string JsonName, PropertyInfo Property)> AllJsonProperties()
    {
        // GetType() so we see the derived record's properties, not just SettingsBlob's
        foreach (var property in GetType()
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var jsonName = property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName;
            if (jsonName != null)
            {
                yield return (jsonName, property);
            }
        }
    }

    /// <summary>
    ///     Exact lookup: returns the property whose [JsonProperty] name matches <paramref name="jsonName" />, or null.
    /// </summary>
    public PropertyInfo? GetPropertyByJsonName(string jsonName)
    {
        foreach (var (name, property) in AllJsonProperties())
        {
            if (name == jsonName)
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    ///     Fuzzy lookup: returns the property whose [JsonProperty] name best matches <paramref name="query" />,
    ///     or null if nothing scores above FuzzyUtilities' threshold.
    /// </summary>
    public PropertyInfo? SearchPropertyByJsonName(string query)
    {
        return FuzzyUtilities.Rank(query, AllJsonProperties(), entry => entry.JsonName, minScore: 80)
            .Select(entry => entry.Property)
            .FirstOrDefault();
    }
}