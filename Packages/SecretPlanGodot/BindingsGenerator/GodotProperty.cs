using System.Text;
using Godot;

namespace SecretPlanGodot.BindingsGenerator;

public readonly record struct GodotProperty(
    string Name,
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
            Name = dictionary["name"].AsString(),
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

            return ClassName;
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
        return VariantType == Variant.Type.Nil;
    }

    public bool IsObject()
    {
        return MeaningfulTypeIfApplicable() != null;
    }

    public string CSharpName()
    {
        return string.Join("",Name.Split("_").Select(a =>
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