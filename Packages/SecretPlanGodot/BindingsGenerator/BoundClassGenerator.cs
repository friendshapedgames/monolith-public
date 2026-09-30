using System.Text;
using Godot;

namespace SecretPlanGodot.BindingsGenerator;

public class BoundClassGenerator
{
    private readonly StringName _className;
    private readonly GodotObject _sample;
    private readonly HashSet<string> _usedTypes = new();
    protected bool IsStatic;
    private readonly HashSet<string> _reservedNames = new();

    public BoundClassGenerator(StringName className, GodotObject sampleObject)
    {
        _className = className;
        _sample = sampleObject;

        var parentClassName = ClassDB.GetParentClass(sampleObject.GetClass());
        foreach (var methodName in ClassDB.ClassGetMethodList(parentClassName).Select(a => a["name"].AsString()))
        {
            _reservedNames.Add(methodName);
        }

        foreach (var propertyName in ClassDB.ClassGetPropertyList(parentClassName).Select(a => a["name"].AsString()))
        {
            _reservedNames.Add(propertyName);
        }

        // magic string
        _reservedNames.Add("script");

        foreach (var method in GetMethods())
        {
            foreach (var typeName in method.InvolvedTypeNames())
            {
                _usedTypes.Add(typeName);
            }
        }

        foreach (var property in GetProperties())
        {
            var typeName = property.MeaningfulTypeIfApplicable();
            if (typeName != null)
            {
                _usedTypes.Add(typeName);
            }
        }
    }

    public IEnumerable<string> GetUsedTypes()
    {
        return _usedTypes;
    }

    public IEnumerable<GodotMethod> GetMethods()
    {
        foreach (var methodDictionary in _sample.GetMethodList())
        {
            var method = GodotMethod.FromDictionary(methodDictionary);

            if (_reservedNames.Contains(method.Name))
            {
                continue;
            }

            yield return method;
        }
    }

    public IEnumerable<GodotProperty> GetProperties()
    {
        foreach (var propertyDictionary in _sample.GetPropertyList())
        {
            var property = GodotProperty.FromDictionary(propertyDictionary);

            if ((property.Usage & PropertyUsageFlags.Category) != 0)
            {
                continue;
            }

            if (_reservedNames.Contains(property.Name))
            {
                continue;
            }

            yield return property;
        }
    }

    public IEnumerable<string> GenerateSourceCode(DistinctQueue<string> distinctQueue)
    {
        var prefix = "public ";
        var inherit = $": {nameof(BoundObject)}";

        if (IsStatic)
        {
            prefix += "static ";
            inherit = string.Empty;
        }

        var lines = new List<string>();
        lines.Add($"{prefix}class {_className} {inherit}");
        lines.Add("{");

        // default constructor
        if (!IsStatic)
        {
            lines.Add($"{prefix}{_className}(GodotObject underlyingObject) : base(underlyingObject) {{ }}");
        }
        else
        {
            lines.Add("private static GodotObject? _instance;");
            lines.Add(
                "private static GodotObject Instance => _instance ??= Engine.GetSingleton(StringNameCache.FmodServer);");
        }

        foreach (var property in GetProperties())
        {
            var builder = new StringBuilder();
            builder.Append(prefix);
            builder.Append(property.BestGuessTypeString());
            builder.Append(" ");
            builder.Append(property.CSharpName());
            builder.Append(" { ");
            if (IsStatic)
            {
                builder.Append("get => ");
                if (property.IsObject())
                {
                    builder.Append(
                        $"new {property.ClassName}(Instance.Get({property.Name})).AsGodotObject());");
                }
                else
                {
                    builder.Append(
                        $"Instance.Get({property.Name}).As<{property.BestGuessTypeString()}>();");
                }

                builder.Append("set => ");
                builder.Append(
                    $"Instance.Set({property.Name}, value); ");
            }
            else
            {
                builder.Append("set => ");
                builder.Append(
                    $"{nameof(BoundObject.PropertySet)}(\"{property.Name}\" ,value); ");
                builder.Append("get => ");
                builder.Append(
                    $"{nameof(BoundObject.PropertyGet)}<{property.BestGuessTypeString()}>(\"{property.Name}\");");
            }

            builder.Append(" }");

            lines.Add(builder.ToString());
        }

        foreach (var method in GetMethods())
        {
            foreach (var typeName in method.InvolvedTypeNames())
            {
                distinctQueue.Enqueue(typeName);
            }

            var argsSuffix = string.Empty;

            if (method.Args.Length > 0)
            {
                argsSuffix = $", {method.CodeGenArgs(false)}";
            }

            var builder = new StringBuilder();

            builder.Append(prefix);
            builder.Append(method.CodeGenSignature());
            builder.Append(" { ");

            var callArgs = $"\"{method.Name}\"{argsSuffix}";

            if (IsStatic)
            {
                if (method.Return.IsVoid())
                {
                    builder.Append(
                        $"Instance.Call({callArgs});");
                }
                else
                {
                    builder.Append("return ");

                    if (method.Return.IsObject())
                    {
                        builder.Append(
                            $"new {method.Return.ClassName}(Instance.Call({callArgs}).AsGodotObject());");
                    }
                    else
                    {
                        builder.Append(
                            $"Instance.Call({callArgs}).As<{method.Return.BestGuessTypeString()}>();");
                    }
                }
            }
            else
            {
                if (method.Return.IsVoid())
                {
                    builder.Append(
                        $"{nameof(BoundObject.CallBoundMethodVoid)}({callArgs});");
                }
                else
                {
                    builder.Append("return ");

                    if (method.Return.IsObject())
                    {
                        builder.Append(
                            $"new {method.Return.ClassName}({nameof(BoundObject.CallBoundMethod)}<GodotObject>({callArgs}));");
                    }
                    else
                    {
                        builder.Append(
                            $"{nameof(BoundObject.CallBoundMethod)}<{method.Return.BestGuessTypeString()}>({callArgs});");
                    }
                }
            }

            builder.Append(" } ");
            lines.Add(builder.ToString());
        }

        foreach (var enumClassName in ClassDB.ClassGetEnumList(_className, true))
        {
            lines.Add($"public enum {enumClassName}");
            lines.Add("{");
            lines.Add(string.Join(",",
                ClassDB.ClassGetEnumConstants(_className, enumClassName, true).Select(enumName =>
                    enumName + " = " + ClassDB.ClassGetIntegerConstant(_className, enumName))));
            lines.Add("}");
        }

        lines.Add("}");
        return lines;
    }
}