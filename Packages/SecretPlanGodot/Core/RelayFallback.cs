using Godot;

namespace SecretPlanGodot.Core;

/// <summary>
///     Used by code generated from [Relay] when no provider ancestor is found.
/// </summary>
public static class RelayFallback
{
    // Log and hand back throwaway state.
    // This is the single place to change if you'd rather cache the fallback or throw.
    public static T Create<T>(Node self) where T : new()
    {
        string selfName;
        if (!self.IsInsideTree())
        {
            selfName = self.Name + " " + self.GetScript();
        }
        else
        {
            selfName = self.GetPath();
        }

        LocalClient.Error($"{selfName} is creating its own {typeof(T).Name}");

        return new T();
    }
}