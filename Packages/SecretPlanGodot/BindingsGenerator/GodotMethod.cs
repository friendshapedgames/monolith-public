using System.Text;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;

namespace SecretPlanGodot.BindingsGenerator;

public readonly record struct GodotMethod(
    string Name,
    GodotProperty[] Args,
    Array DefaultArgs,
    int Id,
    MethodFlags Flags,
    GodotProperty Return)
{
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
    
    public static GodotMethod FromDictionary(Dictionary dictionary)
    {
        return new GodotMethod
        {
            Name = dictionary["name"].AsString(),
            Id = dictionary["id"].AsInt32(),
            Flags = dictionary["flags"].As<MethodFlags>(),
            Return = GodotProperty.FromDictionary(dictionary["return"].AsGodotDictionary()),
            Args = dictionary["args"].AsGodotArray()
                .Select(arg => GodotProperty.FromDictionary(arg.AsGodotDictionary())).ToArray(),
            DefaultArgs = dictionary["default_args"].AsGodotArray()
        };
    }

    public string CodeGenSignature()
    {
        return $"{Return.BestGuessTypeString()} {CSharpName()}({CodeGenArgs(true)})";
    }

    /// <summary>
    /// </summary>
    /// <param name="isParameterList">Includes type names eg: "int foo" rather than "foo"</param>
    /// <returns></returns>
    public string CodeGenArgs(bool isParameterList)
    {
        var argsArray = new List<string>();

        foreach (var arg in Args)
        {
            if (isParameterList)
            {
                argsArray.Add($"{arg.BestGuessTypeString()} {arg.Name}");
            }
            else
            {
                var cast = string.Empty;
                var isEnum = (arg.Usage & PropertyUsageFlags.ClassIsEnum) != 0;

                if (isEnum)
                {
                    cast = "(long)";
                }

                var suffix = string.Empty;

                if (arg.IsObject())
                {
                    suffix = $".{nameof(BoundObject.UnderlyingObject)}";
                }

                argsArray.Add(cast + arg.Name + suffix);
            }
        }

        var join = string.Join(", ", argsArray);
        return join;
    }

    public IEnumerable<string> InvolvedTypeNames()
    {
        var returnType = Return.MeaningfulTypeIfApplicable();

        if (returnType != null)
        {
            yield return returnType;
        }

        foreach (var arg in Args)
        {
            var argType = arg.MeaningfulTypeIfApplicable();
            if (argType != null)
            {
                yield return argType;
            }
        }
    }
}