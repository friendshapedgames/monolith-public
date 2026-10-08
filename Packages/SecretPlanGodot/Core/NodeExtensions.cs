using System.Diagnostics.CodeAnalysis;
using Godot;

namespace SecretPlanGodot.Core;

public static class NodeExtensions
{
    /// <summary>
    ///     Climbs every ancestor, checking (assuming a given ancestor is type T) for a specific predicate about that ancestor.
    ///     If the predicate is wrong, or the ancestor is not type T, it is skipped and we check the next one up
    /// </summary>
    /// <returns></returns>
    public static T? ClimbAncestorsUntil<T>(this Node node, Func<T, bool> check) where T : Node
    {
        var parent = node.GetParent();
        while (parent != null)
        {
            if (parent is T desired && check(desired))
            {
                return desired;
            }

            parent = parent.GetParent();
        }

        return null;
    }

    public static T? ClimbAncestorsUntilFindType<T>(this Node node, int? maxStepCount = null)
    {
        var stepCount = 0;
        var parent = node.GetParent();
        while (parent != null)
        {
            if (parent is T desired)
            {
                return desired;
            }

            if (stepCount >= maxStepCount)
            {
                return default;
            }
            
            parent = parent.GetParent();
            stepCount++;
        }

        return default;
    }

    public static IEnumerable<Node> GetAllDescendants(this Node node, bool includeInternal = false)
    {
        foreach (var child in node.GetChildren(includeInternal))
        {
            yield return child;
            foreach (var descendant in child.GetAllDescendants(includeInternal))
            {
                yield return descendant;
            }
        }
    }

    public static T? FindInDescendants<T>(this Node node)
    {
        foreach (var item in node.GetAllDescendants())
        {
            if (item is T itemAsT)
            {
                return itemAsT;
            }
        }

        return default;
    }

    /// <summary>
    ///     Equivalent to IsMultiplayerAuthority but returns false if there is no peer.
    /// </summary>
    public static bool IsMultiplayerAuthoritySafe(this Node node)
    {
        if (node.Multiplayer == null)
        {
            return false;
        }

        if (!node.Multiplayer.HasMultiplayerPeer())
        {
            return false;
        }

        if (!node.Multiplayer.IsMultiplayerActive())
        {
            return false;
        }

        // this should really be the only time we call IsMultiplayerAuthority directly.
        return node.IsMultiplayerAuthority();
    }

    public static int? GetUniqueIdSafe(this MultiplayerApi multiplayerApi)
    {
        if (multiplayerApi.HasMultiplayerPeer())
        {
            return multiplayerApi.GetUniqueId();
        }

        return null;
    }

    public static void QueueFreeAllChildren(this Node node)
    {
        foreach (var child in node.GetChildren())
        {
            child?.QueueFree();
        }
    }

    public static bool IsValidExactly([NotNullWhen(true)] this Node? node)
    {
        return node != null && GodotObject.IsInstanceValid(node);
    }

    public static bool IsValidAndNotQueuedForDeletion([NotNullWhen(true)] this Node? node)
    {
        return node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
    }

    public static bool IsValidAndInsideTree([NotNullWhen(true)] this Node? node)
    {
        return node.IsValidExactly() && node.IsInsideTree();
    }

    public static void RemoveFromTree(this Node node)
    {
        if (node.IsValidAndInsideTree())
        {
            node.GetParent().RemoveChild(node);
        }
        else
        {
            LocalClient.Error(
                $"Attempted to remove {node.GetPath()} from Tree but it's either not in the three or not valid");
        }
    }
}