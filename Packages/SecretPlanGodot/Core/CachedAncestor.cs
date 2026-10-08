using Godot;

namespace SecretPlanGodot.Core;

public class CachedAncestor<T>
{
    protected T? Cached;
    private readonly int? _maxStepCount;

    public CachedAncestor(int? maxStepCount = null)
    {
        _maxStepCount = maxStepCount;
    }

    /// <summary>
    ///     NOTE: this does not consider IsValid
    /// </summary>
    /// <returns></returns>
    public T? GetOrNull(Node node)
    {
        return Cached ??= node.ClimbAncestorsUntilFindType<T>(_maxStepCount);
    }
}