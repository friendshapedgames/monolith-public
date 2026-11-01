namespace SecretPlanCore.Core;

public class FloatProvider : ValueProvider<float>
{
    public FloatProvider(float startingValue) : base(startingValue)
    {
    }

    public FloatProvider(Func<float> providerFunction) : base(providerFunction)
    {
    }

    public FloatProvider() : base(0)
    {
    }
}