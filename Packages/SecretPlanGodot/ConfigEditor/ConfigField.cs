using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using SecretPlanCore.Configuration;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;

namespace SecretPlanGodot.ConfigEditor;

public class ConfigField
{
    private readonly ConfigFieldChangeNotifier _changeNotifier;
    private readonly Func<object?> _getValue;
    private readonly Action<object> _setValue;

    private ConfigField(
        ConfigFieldChangeNotifier changeNotifier,
        object? parent,
        Type associatedType,
        bool isArray,
        string humanReadableName,
        string? memberName,
        bool isReadonly,
        Action<object> setValue,
        Func<object?> getValue
    )
    {
        _changeNotifier = changeNotifier;
        ParentInstance = parent;
        AssociatedType = associatedType;
        IsArray = isArray;
        HumanReadableName = humanReadableName;
        _setValue = setValue;
        _getValue = getValue;
        RealMemberName = memberName;
        IsReadonly = isReadonly;
    }

    public bool IsReadonly { get; }

    /// <summary>
    ///     Name of the actual member of this field, should only be compared with a `nameof()`
    /// </summary>
    public string? RealMemberName { get; }

    /// <summary>
    ///     The parent instance that this config field came from. If this config field is an array element, this gives us the
    ///     array, if this config field is an object field, it gives us the object.
    /// </summary>
    public object? ParentInstance { get; }

    public Type AssociatedType { get; }
    public bool IsArray { get; }
    public string HumanReadableName { get; }

    public object? GetValue()
    {
        return _getValue();
    }

    public void SetValue(object value)
    {
        _setValue(value);
        ValueAssigned?.Invoke(value);
        _changeNotifier.NotifyChanged();
    }

    public void ForceNotifyChanged()
    {
        _changeNotifier.NotifyChanged();
    }

    public override string ToString()
    {
        return $"{HumanReadableName} of type {AssociatedType.Name} (value: {GetValue()})";
    }

    /// <summary>
    ///     Attempts to get the value, if the current value is null, we return a new() of the value.
    /// </summary>
    public T? GetValueAs<T>() where T : class, new()
    {
        var rawValue = GetValue();

        if (rawValue == null)
        {
            return new T();
        }

        var result = rawValue as T;

        if (result == null)
        {
            LocalClient.Error(
                $"{HumanReadableName} has value {rawValue} which could not be interpreted as {typeof(T).Name}");
            return null;
        }

        return result;
    }

    /// <summary>
    ///     Gets the value if the type is a valid match, otherwise null
    /// </summary>
    public T? GetValueOrDefaultAs<T>()
    {
        var rawValue = GetValue();

        if (rawValue is T rawValueAsT)
        {
            return rawValueAsT;
        }

        return default;
    }

    /// <summary>
    ///     If this thing is an array, this creates a field info that represents its elements
    /// </summary>
    public ConfigField CreateConfigFieldOfArrayElement(int index)
    {
        var elementType = GetAssociatedType(AssociatedType);
        return new ConfigField(
            _changeNotifier,
            GetValue(),
            elementType,
            AssociatedType.IsArray,
            $"Element [{index}] of {HumanReadableName}",
            null,
            IsReadonly,
            value =>
            {
                var parentValue = GetValue();
                if (parentValue is Array array)
                {
                    var maxIndex = array.Length - 1;
                    if (index > maxIndex)
                    {
                        var newLength = index + 1;
                        var newArray = Array.CreateInstance(elementType, newLength);
                        for (var i = 0; i < newArray.Length; i++)
                        {
                            if (array.IsValidIndex(i))
                            {
                                newArray.SetValue(array.GetValue(i), i);
                            }
                            else
                            {
                                newArray.SetValue(CreateEmpty(elementType), i);
                            }
                        }

                        // re-assign the new array
                        SetValue(newArray);

                        // re-evaluate the array local so we get the new size
                        array = (GetValue() as Array)!;
                    }

                    // set array value at that index
                    array.SetValue(value, index);
                }
            },
            () =>
            {
                var parentValue = GetValue();
                if (parentValue is Array array)
                {
                    if (!array.IsValidIndex(index))
                    {
                        return CreateEmpty(elementType);
                    }

                    return array.GetValue(index);
                }

                return null;
            });
    }

    private object CreateEmpty(Type elementType)
    {
        if (elementType == typeof(string))
        {
            // special case for strings in particular
            return string.Empty;
        }

        return Activator.CreateInstance(elementType)!;
    }

    public IEnumerable<ConfigField> GetSubfields()
    {
        var currentSubfields = GetFieldsOfObject(GetValueOrEmptyInstance(), () => { }).ToList();

        object Reconstructed()
        {
            var instance = GetValueOrEmptyInstance();
            foreach (var subfield in currentSubfields)
            {
                SetValueOnInstance(subfield, instance);
            }

            return instance;
        }

        foreach (var subfield in currentSubfields)
        {
            subfield.ValueAssigned += value =>
            {
                // Reconstruct the underlying value and then assign it
                var reconstructed = Reconstructed();

                if (subfield.RealMemberName != null)
                {
                    Reflection.SetMemberValue(reconstructed, subfield.RealMemberName, value);
                }

                SetValue(reconstructed);
            };
        }

        return currentSubfields;
    }

    private static void SetValueOnInstance(ConfigField subfield, object instance)
    {
        if (subfield.RealMemberName == null)
        {
            LocalClient.Error(
                $"Could not assign to {subfield.HumanReadableName} of {instance} because {subfield.HumanReadableName} does not have a {nameof(RealMemberName)}");
            return;
        }

        Reflection.SetMemberValue(instance, subfield.RealMemberName, subfield.GetValue());
    }

    private event Action<object>? ValueAssigned;

    public static Type GetAssociatedType(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType();

            if (elementType == null)
            {
                throw new Exception(
                    $"Could not identify type of {type.FullName}, it appears to be an array but does not have an ElementType?");
            }

            return elementType;
        }

        return type;
    }

    public static ConfigField Empty()
    {
        return new ConfigField(new ConfigFieldChangeNotifier(() => { }), null, typeof(void), false,
            ConfigEditorConstants.EmptyFieldText,
            null,
            false,
            _ => { LocalClient.Error("Writing to an empty ConfigField, this is probably not what you want"); },
            () =>
            {
                LocalClient.Error("Reading from an empty ConfigField, this is probably not what you want");
                return null;
            });
    }

    public static IEnumerable<ConfigField> GetFieldsOfObject(object instance, Action onChanged)
    {
        var fieldOwner = new ConfigFieldChangeNotifier(onChanged);
        foreach (var member in Reflection.GetAllMembersInTypeWithAttribute<JsonPropertyAttribute>(instance.GetType()))
        {
            var visibleName =
                BuildHumanReadableName(member.Name,
                    member.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? "???");

            ConfigField? configField = null;

            if (member.GetCustomAttribute<ObsoleteAttribute>() != null)
            {
                continue;
            }

            if (member.GetCustomAttribute<HideFromConfigEditorAttribute>() != null)
            {
                continue;
            }

            var isReadonly = member.GetCustomAttribute<ReadonlyConfigField>() != null;

            if (member is PropertyInfo propertyInfo)
            {
                configField = new ConfigField(
                    fieldOwner,
                    instance,
                    GetAssociatedType(propertyInfo.PropertyType),
                    propertyInfo.PropertyType.IsArray,
                    visibleName,
                    member.Name,
                    isReadonly,
                    value =>
                    {
                        if (propertyInfo.CanWrite)
                        {
                            propertyInfo.SetValue(instance, value);
                        }
                    },
                    () => propertyInfo.GetValue(instance)
                );
            }

            if (member is FieldInfo fieldInfo)
            {
                configField = new ConfigField(
                    fieldOwner,
                    instance,
                    GetAssociatedType(fieldInfo.FieldType),
                    fieldInfo.FieldType.IsArray,
                    visibleName,
                    member.Name,
                    isReadonly,
                    value => fieldInfo.SetValue(instance, value),
                    () => fieldInfo.GetValue(instance)
                );
            }

            if (configField == null)
            {
                continue;
            }

            var shouldHide =
                configField.AssociatedType.GetCustomAttribute(typeof(HideFromConfigEditorAttribute), true) != null;
            if (shouldHide)
            {
                continue;
            }

            yield return configField;
        }
    }

    private static string BuildHumanReadableName(string fieldName, string jsonName)
    {
        var nameBuilder = new StringBuilder();

        var nextCharShouldBeCapital = false;
        foreach (var currentChar in fieldName)
        {
            if (currentChar == '_' || char.IsUpper(currentChar))
            {
                nameBuilder.Append(' ');
                if (currentChar != '_')
                {
                    nameBuilder.Append(currentChar);
                }
                else
                {
                    nextCharShouldBeCapital = true;
                }
            }
            else if (nextCharShouldBeCapital)
            {
                nameBuilder.Append(char.ToUpper(currentChar));
                nextCharShouldBeCapital = false;
            }
            else
            {
                nameBuilder.Append(currentChar);
            }
        }

        nameBuilder.Append(" (");
        nameBuilder.Append(jsonName);
        nameBuilder.Append(")");

        var visibleName = nameBuilder.ToString();
        return visibleName;
    }

    public object GetValueOrEmptyInstance()
    {
        return GetValue() ?? CreateEmptyInstanceOfValue();
    }

    private object CreateEmptyInstanceOfValue()
    {
        var instance = Activator.CreateInstance(AssociatedType);

        if (instance == null)
        {
            throw new Exception($"Could not create empty instance of {AssociatedType}");
        }

        return instance;
    }
}