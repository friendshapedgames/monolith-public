using Godot;

namespace SecretPlanGodot.Core;

/// <summary>
/// Same as CachedAncestor but guarantees that T is a node (ie: not an Interface) and therefore we can check if it's Valid.
/// </summary>
public class CachedAncestorNode<T> : CachedAncestor<T> where T : Node
{
    public CachedAncestorNode(int? maxStepCount) : base(maxStepCount)
    {
    }
    
    public T? GetOrNullCheckValid(Node node)
    {
        if (!Cached.IsValidExactly())
        {
            var result = node.ClimbAncestorsUntilFindType<T>();

            if (result.IsValidExactly())
            {
                Cached = result;
            }
        }

        return Cached;
    }
}