using Godot;

namespace SecretPlanGodot.BindingsGenerator;

public class BoundClassGeneratorStatic : BoundClassGenerator
{
    public BoundClassGeneratorStatic(StringName className) : base(className,
        Engine.GetSingleton(className) ?? throw new Exception("Singleton does not exist!"))
    {
        IsStatic = true;
    }
}