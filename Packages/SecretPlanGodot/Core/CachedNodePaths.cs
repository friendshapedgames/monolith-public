using System.Collections.Concurrent;
using Godot;

namespace SecretPlanGodot.Core;

internal static class CachedNodePaths
{
    // NodePath is finalizable, so share one instance per distinct path instead of one per owner
    private static readonly ConcurrentDictionary<string, NodePath> _cache = new();

    public static NodePath Get(string path)
    {
        return _cache.GetOrAdd(path, static p => new NodePath(p));
    }
}