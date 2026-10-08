using Godot;

namespace SecretPlanGodot.BindingsGenerator;

public class BoundClassGenerator
{
    private readonly StringName _className;
    private readonly HashSet<string> _reservedNames = new();
    private readonly GodotObject _sample;
    private readonly HashSet<string> _usedTypes = new();
    private readonly bool _isScript;
    protected bool IsStatic;

    public BoundClassGenerator(StringName className, GodotObject sampleObject)
    {
        _className = className;
        _sample = sampleObject;
        _isScript = sampleObject.GetScript().AsGodotObject() is Script;

        // For native classes, GetClass() is the class we're binding, so reserve everything from its parent.
        // For scripted objects (eg: a GDScript autoload), GetClass() is the native base (eg: Node), so reserve all of it.
        StringName reservedClassName = _isScript ? sampleObject.GetClass() : ClassDB.GetParentClass(sampleObject.GetClass());
        foreach (var methodName in ClassDB.ClassGetMethodList(reservedClassName).Select(a => a["name"].AsString()))
        {
            _reservedNames.Add(methodName);
        }

        foreach (var propertyName in ClassDB.ClassGetPropertyList(reservedClassName).Select(a => a["name"].AsString()))
        {
            _reservedNames.Add(propertyName);
        }

        // magic strings
        _reservedNames.Add("script");
        // Object pseudo-method that isn't in ClassDB's method list but shows up in GetMethodList()
        _reservedNames.Add("free");

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

            if (IsHidden(method.RawName))
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

            if (IsHidden(property.RawName))
            {
                continue;
            }

            yield return property;
        }
    }

    private bool IsHidden(string rawName)
    {
        if (_reservedNames.Contains(rawName))
        {
            return true;
        }

        // In GDScript, a leading underscore means private by convention. This also catches engine virtuals
        // (_ready, _process, _init, etc.) which aren't in ClassDB's method list.
        return _isScript && rawName.StartsWith('_');
    }

    private IStatement PropertyName(GodotProperty property)
    {
        return new GetPropertyOrFieldOnStatement(new Raw("PropertyNames"), property.PascalCaseName());
    }

    private IStatement MethodName(GodotMethod method)
    {
        return new GetPropertyOrFieldOnStatement(new Raw("MethodNames"), method.PascalCaseName());
    }

    public IEnumerable<IStatement> GenerateSourceCode(DistinctQueue<string> distinctQueue)
    {
        var parentClass = nameof(BoundObject);
        yield return new DeclareClass(AccessLevel.Public, IsStatic, _className, IsStatic ? null : parentClass);
        yield return new NewLine();
        yield return new OpenScope();
        yield return new NewLine();

        // default constructor
        if (!IsStatic)
        {
            yield return new DeclareConstructor(_className, [new StatementParameter("GodotObject", "underlyingObject")],
                ["underlyingObject"]);
            yield return new OpenScope();
            yield return new CloseScope();
            yield return new NewLine();
        }
        else
        {
            yield return new DeclareField(
                new Prefix(AccessLevel.Private, true),
                new TypeAndIdentifier("GodotObject?", "_instance"));

            yield return new DeclareProperty(
                new Prefix(AccessLevel.Private, true),
                new TypeAndIdentifier("GodotObject", "Instance"),
                new Raw("_instance ??= Engine.GetSingleton(StringNameCache.FmodServer);"),
                null
            );
        }

        // make MethodNames and PropertyNames classes

        yield return new DeclareClass(AccessLevel.Private, true, "MethodNames");
        yield return new OpenScope();
        yield return new NewLine();
        foreach (var method in GetMethods())
        {
            yield return new DeclareField(new Prefix(AccessLevel.Public, true, true),
                new TypeAndIdentifier(nameof(StringName), method.PascalCaseName()),
                new WrappedInQuotes(method.RawName));
            yield return new NewLine();
        }

        yield return new CloseScope();
        yield return new NewLine();

        var properties = GetProperties().ToList();

        if (properties.Count > 0)
        {
            yield return new DeclareClass(AccessLevel.Private, true, "PropertyNames");
            yield return new OpenScope();
            yield return new NewLine();
            foreach (var property in properties)
            {
                yield return new DeclareField(new Prefix(AccessLevel.Public, true, true),
                    new TypeAndIdentifier(nameof(StringName), property.PascalCaseName()),
                    new WrappedInQuotes(property.RawName));
                yield return new NewLine();
            }

            yield return new CloseScope();
            yield return new NewLine();
        }

        foreach (var property in properties)
        {
            IStatement? getter;
            IStatement? setter;

            if (IsStatic)
            {
                var assignGodotObjectVariable = new AssignVariable("godotObject",
                    new CallMethodOnStatement(new Raw("Instance"), "Get", [PropertyName(property)]));

                if (property.IsObject())
                {
                    getter = new MultiStatement([
                        assignGodotObjectVariable,
                        new EndStatement(),
                        new CallMethodOnStatement(new Raw("godotObject"), nameof(Variant.AsGodotObject)),
                        new EndStatement()
                    ]);
                }
                else
                {
                    getter = new MultiStatement([
                        assignGodotObjectVariable,
                        new EndStatement(),
                        new CallMethodOnStatement(new Raw("godotObject"), $"As<{property.BestGuessTypeString()}>"),
                        new EndStatement()
                    ]);
                }

                setter = new MultiStatement([
                    new CallMethodOnStatement(new Raw("Instance"), "Set", [
                        PropertyName(property),
                        new Raw("value")
                    ]),
                    new EndStatement()
                ]);
            }
            else
            {
                setter =
                    new MultiStatement(
                    [
                        new CallLocalMethod(nameof(BoundObject.PropertySet),
                        [
                            PropertyName(property),
                            new Raw("value")
                        ]),
                        new EndStatement()
                    ])
                    ;

                getter =
                    new MultiStatement([
                            new CallLocalMethod($"{nameof(BoundObject.PropertyGet)}<{property.BestGuessTypeString()}>",
                            [
                                PropertyName(property)
                            ]),
                            new EndStatement()
                        ]
                    )
                    ;
            }

            yield return new DeclareProperty(new Prefix(AccessLevel.Public, IsStatic),
                new TypeAndIdentifier(property.BestGuessTypeString(), property.PascalCaseName()),
                getter, setter);
        }

        foreach (var method in GetMethods())
        {
            foreach (var typeName in method.InvolvedTypeNames())
            {
                distinctQueue.Enqueue(typeName);
            }

            yield return new DeclareMethod(new Prefix(AccessLevel.Public, IsStatic),
                new TypeAndIdentifier(method.Return.BestGuessTypeString(), method.PascalCaseName()),
                method.Args.Select(arg => new TypeAndIdentifier(arg.BestGuessTypeString(), arg.SafeIdentifier())).ToArray());

            yield return new OpenScope();

            List<IStatement> callArgs = new();

            callArgs.Add(MethodName(method));

            foreach (var arg in method.Args)
            {
                var str = arg.SafeIdentifier();
                if (arg.IsObject())
                {
                    str += ".UnderlyingObject";
                }

                callArgs.Add(new Raw(str));
            }

            if (IsStatic)
            {
                var staticCall = new CallMethodOnStatement(new Raw("Instance"), "Call", callArgs.ToArray());

                if (method.Return.IsVoid())
                {
                    yield return staticCall;
                    yield return new EndStatement();
                }
                else
                {
                    if (method.Return.IsObject())
                    {
                        yield return new ReturnValue(new ConstructObject(method.Return.BestGuessTypeString(),
                            [new CallMethodOnStatement(staticCall, "AsGodotObject")]));
                    }
                    else
                    {
                        yield return new ReturnValue(new CallMethodOnStatement(staticCall,
                            $"As<{method.Return.BestGuessTypeString()}>"));
                    }
                }
            }
            else
            {
                if (method.Return.IsVoid())
                {
                    yield return new CallLocalMethod(nameof(BoundObject.CallBoundMethodVoid), callArgs.ToArray());
                    yield return new EndStatement();
                }
                else
                {
                    if (method.Return.IsObject())
                    {
                        yield return new ReturnValue(new ConstructObject(
                            method.Return.BestGuessTypeString(), [
                                new CallLocalMethod($"{nameof(BoundObject.CallBoundMethod)}<GodotObject>",
                                    callArgs.ToArray())
                            ]));
                    }
                    else
                    {
                        yield return new ReturnValue(
                            new CallLocalMethod(
                                $"{nameof(BoundObject.CallBoundMethod)}<{method.Return.BestGuessTypeString()}>",
                                callArgs.ToArray())
                        );
                    }
                }
            }

            yield return new CloseScope();
            yield return new NewLine();
        }

        // Script classes aren't in ClassDB, so there are no enums/constants to find here
        var classDbEnums = _isScript ? System.Array.Empty<string>() : ClassDB.ClassGetEnumList(_className, true);
        var classDbConstants = _isScript ? System.Array.Empty<string>() : ClassDB.ClassGetIntegerConstantList(_className);

        foreach (var enumClassName in classDbEnums)
        {
            var enumNames = ClassDB.ClassGetEnumConstants(_className, enumClassName, true);
            var enumValues = enumNames.Select(enumName =>
                ClassDB.ClassGetIntegerConstant(_className, enumName)).ToArray();

            var tuples = new Tuple<string, long>[enumNames.Length];
            for (var i = 0; i < tuples.Length; i++)
            {
                tuples[i] = new Tuple<string, long>(enumNames[i], enumValues[i]);
            }

            yield return new DeclareEnumWithValues(enumClassName, tuples);
        }

        foreach(var integerConstantName in classDbConstants)
        {
            var value = ClassDB.ClassGetIntegerConstant(_className, integerConstantName);

            yield return new DeclareConst(new("long", integerConstantName), value.ToString());
        }
        
        yield return new CloseScope();
        yield return new NewLine();
        
    }
}