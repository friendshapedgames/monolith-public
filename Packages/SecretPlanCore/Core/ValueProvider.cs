namespace SecretPlanCore.Core;

public class ValueProvider<T>
{
    private T _startingValue;
    private Func<T>? _providerFunction;

    public ValueProvider(T startingValue)
    {
        _startingValue = startingValue;
    }

    public ValueProvider(Func<T> providerFunction)
    {
        _providerFunction = providerFunction;
        _startingValue = default!;
    }

    public T Value => _providerFunction != null ? _providerFunction() : _startingValue;

    public void SetProvider(Func<T> func)
    {
        _providerFunction = func;
    }

    public void SetStaticValue(T value)
    {
        _startingValue = value;
    }

    public static implicit operator T(ValueProvider<T> self)
    {
        return self.Value;
    }
}