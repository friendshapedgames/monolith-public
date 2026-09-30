using Godot;

namespace SecretPlanGodot.BindingsGenerator;

public class BoundObject : IDisposable
{
    public BoundObject(GodotObject underlyingObject)
    {
        UnderlyingObject = underlyingObject;
    }

    public GodotObject UnderlyingObject { get; }

    public T CallBoundMethod<T>(StringName name, params Variant[] parameters)
    {
        return UnderlyingObject.Call(name, parameters).As<T>();
    }

    public void CallBoundMethodVoid(StringName name, params Variant[] parameters)
    {
        UnderlyingObject.Call(name, parameters);
    }

    public void PropertySet(StringName name, Variant value)
    {
        UnderlyingObject.Set(name, value);
    }

    public T PropertyGet<T>(StringName name)
    {
        return UnderlyingObject.Get(name).As<T>();
    }

    public void Dispose()
    {
        UnderlyingObject.Dispose();
    }
}