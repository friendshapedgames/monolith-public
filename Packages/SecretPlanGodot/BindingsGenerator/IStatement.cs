using System.Text;

namespace SecretPlanGodot.BindingsGenerator;

public interface IStatement
{
    public string Generate();
}

public record struct Raw(string Content) : IStatement
{
    public string Generate()
    {
        return Content;
    }
}

public record struct UsingStatement(string Content) : IStatement
{
    public string Generate()
    {
        return $"using {Content};\n";
    }
}

public record struct DeclareNamespace(string Content) : IStatement
{
    public string Generate()
    {
        return $"namespace {Content};\n";
    }
}

public enum AccessLevel
{
    Private,
    Public
}

public record struct NewLine() : IStatement
{
    public string Generate()
    {
        return "\n";
    }
}

public record struct OpenScope() : IStatement
{
    public string Generate()
    {
        return "{";
    }
}

public record struct CloseScope() : IStatement
{
    public string Generate()
    {
        return "}";
    }
}

public record struct DeclareClass(AccessLevel AccessLevel, bool IsStatic, string ClassName, string? ParentClassName = null)
    : IStatement
{
    public string Generate()
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.Append(AccessLevel.ToString().ToLower());
        stringBuilder.Append(" ");
        if (IsStatic)
        {
            stringBuilder.Append("static ");
        }

        stringBuilder.Append("class ");

        stringBuilder.Append(ClassName);

        if (ParentClassName != null)
        {
            stringBuilder.Append($" : {ParentClassName}");
        }

        return stringBuilder.ToString();
    }
}

public record struct StatementParameter(string TypeName, string Identifier)
{
    public override string ToString()
    {
        return $"{TypeName} {Identifier}";
    }
}

public record struct DeclareConstructor(string ClassName, StatementParameter[] Parameters, string[]? BaseParameters)
    : IStatement
{
    public string Generate()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append("public ");
        stringBuilder.Append(ClassName);
        stringBuilder.Append("(");
        stringBuilder.Append(string.Join(", ", Parameters.Select(a => a.ToString())));
        stringBuilder.Append(")");

        if (BaseParameters != null)
        {
            stringBuilder.Append(" : base(");
            stringBuilder.Append(string.Join(", ", BaseParameters));
            stringBuilder.Append(")");
        }

        return stringBuilder.ToString();
    }
}

public record struct TypeAndIdentifier(string TypeName, string Identifier)
{
    public string Generate()
    {
        return $"{TypeName} {Identifier}";
    }
}

public record struct Prefix(AccessLevel AccessLevel, bool IsStatic, bool IsReadonly = false)
{
    public string Generate()
    {
        var accessLevel = AccessLevel.ToString().ToLower();

        var builder = new StringBuilder();
        builder.Append(accessLevel);

        if (IsStatic)
        {
            builder.Append(" static");
        }

        if (IsReadonly)
        {
            builder.Append(" readonly");
        }

        return builder.ToString();
    }
}

public record struct DeclareProperty(
    Prefix Prefix,
    TypeAndIdentifier TypeAndIdentifier,
    IStatement? Getter,
    IStatement? Setter) : IStatement
{
    public string Generate()
    {
        var builder = new StringBuilder();

        builder.Append(Prefix.Generate() + " " + TypeAndIdentifier.Generate());
        builder.Append("{");

        if (Getter != null)
        {
            builder.Append("get => ");
            builder.Append(Getter.Generate());
        }

        if (Setter != null)
        {
            builder.Append("set => ");
            builder.Append(Setter.Generate());
        }

        builder.Append("}");

        builder.AppendLine();

        return builder.ToString();
    }
}

public record struct DeclareField(Prefix Prefix, TypeAndIdentifier TypeAndIdentifier, IStatement? RValue = null) : IStatement
{
    public string Generate()
    {
        var builder = new StringBuilder();
        builder.Append($"{Prefix.Generate()} {TypeAndIdentifier.Generate()}");
        if (RValue != null){
            builder.Append($"= {RValue.Generate()}");
        }

        builder.Append(";");
        return builder.ToString();
    }
}

public record struct EndStatement() : IStatement
{
    public string Generate()
    {
        return ";";
    }
}

public record struct MultiStatement(IStatement[] Statements) : IStatement
{
    public string Generate()
    {
        var builder = new StringBuilder();
        foreach (var item in Statements)
        {
            builder.Append(item.Generate());
        }

        return builder.ToString();
    }
}

public record struct AssignVariable(string Identifier, IStatement Rvalue) : IStatement
{
    public string Generate()
    {
        return $"var {Identifier} = {Rvalue.Generate()}";
    }
}

public record struct ConstructObject(string ClassName, IStatement[] Args) : IStatement
{
    public string Generate()
    {
        return $"new {ClassName}({string.Join(", ", Args.Select(a => a.Generate()))})";
    }
}

/// <summary>
///     Identifier.MethodName(Args)
/// </summary>
public record struct CallMethodOnStatement(IStatement LValue, string MethodName, IStatement[]? Args = null) : IStatement
{
    public string Generate()
    {
        var args = Args ?? [];
        return $"{LValue.Generate()}.{MethodName}({string.Join(", ", args.Select(a => a.Generate()))})";
    }
}

public record struct GetPropertyOrFieldOnStatement(IStatement LValue, string PropertyName) : IStatement
{
    public string Generate()
    {
        return $"{LValue.Generate()}.{PropertyName}";
    }
}

public record struct CallLocalMethod(string MethodName, IStatement[]? Args = null) : IStatement
{
    public string Generate()
    {
        var args = Args ?? [];
        return $"{MethodName}({string.Join(", ", args.Select(a => a.Generate()))})";
    }
}

public record struct WrappedInQuotes(string Content) : IStatement
{
    public string Generate()
    {
        return $"\"{Content}\"";
    }
}

public record struct DeclareMethod(Prefix Prefix, TypeAndIdentifier TypeAndIdentifier, TypeAndIdentifier[]? Parameters)
    : IStatement
{
    public string Generate()
    {
        var parameters = Parameters ?? [];
        return
            $"{Prefix.Generate()} {TypeAndIdentifier.Generate()}({string.Join(", ", parameters.Select(a => a.Generate()))})";
    }
}

public record struct ReturnValue(IStatement RValue) : IStatement
{
    public string Generate()
    {
        return $"return {RValue.Generate()};";
    }
}

public record struct DeclareEnumWithValues(string EnumClass, Tuple<string, long>[] Tuples) : IStatement
{
    public string Generate()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"public enum {EnumClass}");
        builder.AppendLine("{");

        var enumerable = Tuples.Select(a =>
        {
            var innerBuilder = new StringBuilder();
            innerBuilder.Append(a.Item1);
            innerBuilder.Append(" = ");
            innerBuilder.Append(a.Item2);
            return innerBuilder.ToString();
        });

        builder.AppendLine(string.Join(",\n", enumerable));
        builder.AppendLine("}");

        return builder.ToString();
    }
}

public record struct DeclareConst(TypeAndIdentifier TypeAndIdentifier, string Value) : IStatement
{
    public string Generate()
    {
        return $"public const {TypeAndIdentifier.Generate()} = {Value};";
    }
}