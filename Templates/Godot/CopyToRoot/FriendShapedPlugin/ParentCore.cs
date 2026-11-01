using FriendShapedDistributable;
using Godot;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin;

public interface ICoreStateProvider
{
    public CoreState CoreState { get; }
}

public class ParentCore : CachedAncestor<ICoreStateProvider>
{
    public CoreState State(Node self)
    {
        var result = GetOrNull(self);
        if (result != null)
        {
            return result.CoreState;
        }

        LocalClient.Error($"{self.GetPath()} is creating its own CoreState");
        return new CoreState();
    }
}