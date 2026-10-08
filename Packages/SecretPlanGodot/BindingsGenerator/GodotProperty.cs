using System.Text;
using Godot;

namespace SecretPlanGodot.BindingsGenerator;

public readonly record struct GodotProperty(
    string RawName,
    string ClassName,
    Variant.Type VariantType,
    PropertyHint Hint,
    string HintString,
    PropertyUsageFlags Usage)
{
    public static GodotProperty FromDictionary(Godot.Collections.Dictionary dictionary)
    {
        return new GodotProperty
        {
            RawName = dictionary["name"].AsString(),
            ClassName = dictionary["class_name"].AsString(),
            Hint = dictionary["hint"].As<PropertyHint>(),
            HintString = dictionary["hint_string"].AsString(),
            VariantType = dictionary["type"].As<Variant.Type>(),
            Usage = dictionary["usage"].As<PropertyUsageFlags>()
        };
    }

    public string BestGuessTypeString()
    {
        if (!string.IsNullOrEmpty(ClassName))
        {
            if (ClassName == "Object")
            {
                return "GodotObject";
            }

            // Script-owned types (GDScript class_name classes, script enums) have no C# counterpart,
            // so fall through to the underlying variant type (GodotObject, int, etc.)
            if (!IsScriptTypeName(ClassName))
            {
                return ClassName;
            }
        }

        // Untyped GDScript params/returns (and native Variant ones) come through as Nil + NilIsVariant
        if (IsVariant())
        {
            return "Variant";
        }

        return VariantType switch
        {
            Variant.Type.Nil => "void",
            Variant.Type.Bool => "bool",
            Variant.Type.Int => "int",
            Variant.Type.Float => "float",
            Variant.Type.String => "string",
            Variant.Type.Object => "GodotObject",
            Variant.Type.Dictionary => "Godot.Collections.Dictionary",
            Variant.Type.Array => "Godot.Collections.Array",
            Variant.Type.PackedByteArray => "byte[]",
            Variant.Type.PackedInt32Array => "int[]",
            Variant.Type.PackedInt64Array => "long[]",
            Variant.Type.PackedFloat32Array => "float[]",
            Variant.Type.PackedFloat64Array => "double[]",
            Variant.Type.PackedStringArray => "string[]",
            Variant.Type.PackedVector2Array => "Vector2[]",
            Variant.Type.PackedVector3Array => "Vector3[]",
            Variant.Type.PackedColorArray => "Color[]",
            Variant.Type.PackedVector4Array => "Vector4[]",
            _ => VariantType.ToString()
        };
    }

    public string? MeaningfulTypeIfApplicable()
    {
        if (!string.IsNullOrEmpty(ClassName) && ClassDB.ClassExists(ClassName) && ClassDB.CanInstantiate(ClassName))
        {
            // Engine classes (Texture, InputEvent, etc.) already have real C# bindings, only wrap the ones that don't
            // (eg: GDExtension classes like FmodEvent)
            if (typeof(GodotObject).Assembly.GetType($"Godot.{ClassName}") != null)
            {
                return null;
            }

            if (ClassName == "Node")
            {
                return null;
            }

            if (ClassName == "Resource")
            {
                return null;
            }

            if (ClassName == "Object")
            {
                return null;
            }

            return ClassName;
        }

        return null;
    }

    public bool IsVoid()
    {
        return VariantType == Variant.Type.Nil && !IsVariant();
    }

    public bool IsVariant()
    {
        return VariantType == Variant.Type.Nil && (Usage & PropertyUsageFlags.NilIsVariant) != 0;
    }

    /// <summary>
    ///     Script enums show up as "res://path/to/script.gd.EnumName" (unnamed scripts) or "GlobalClassName.EnumName",
    ///     script classes show up as "GlobalClassName". None of these exist in ClassDB.
    /// </summary>
    public static bool IsScriptTypeName(string className)
    {
        if (className.StartsWith("res://") || className.StartsWith("uid://"))
        {
            return true;
        }

        var owner = className.Split('.')[0];
        return !ClassDB.ClassExists(owner) && GlobalScriptClassNames.Value.Contains(owner);
    }

    private static readonly Lazy<HashSet<string>> GlobalScriptClassNames = new(() =>
        ProjectSettings.GetGlobalClassList().Select(a => a["class"].AsString()).ToHashSet());

    /// <summary>
    ///     Identifier that's safe to use in C# (eg: GDScript happily names parameters "event")
    /// </summary>
    public string SafeIdentifier()
    {
        return CSharpKeywords.Contains(RawName) ? "@" + RawName : RawName;
    }

    private static readonly HashSet<string> CSharpKeywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    ];

    public bool IsObject()
    {
        return MeaningfulTypeIfApplicable() != null;
    }

    public string PascalCaseName()
    {
        return string.Join("",RawName.Split("_").Select(a =>
        {
            var stringBuilder = new StringBuilder();
            for (var index = 0; index < a.Length; index++)
            {
                stringBuilder.Append(index == 0 ? char.ToUpper(a[0]) : a[index]);
            }

            return stringBuilder.ToString();
        }));
    }
}