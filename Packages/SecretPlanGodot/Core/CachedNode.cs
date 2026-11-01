using System.Diagnostics.Contracts;
using Godot;

namespace SecretPlanGodot.Core;

public class CachedNode<T> where T : Node
{
    private T? _cache;
    private readonly Func<T>? _createFunction;

    public CachedNode(string nodePath, Func<T>? createFunction = null)
        : this(CachedNodePaths.Get(nodePath), createFunction)
    {
    }

    public CachedNode(NodePath nodePath, Func<T>? createFunction = null)
    {
        NodePath = nodePath;
        _createFunction = createFunction;
    }

    public NodePath NodePath { get; }

    public void ClearCache()
    {
        _cache = null;
    }

    [Pure]
    public T? GetOrNull(Node parentNode)
    {
        ClearCacheIfInvalid();
        return _cache ??= parentNode.GetNodeOrNull<T>(NodePath);
    }

    [Pure]
    public T Get(Node parentNode)
    {
        ClearCacheIfInvalid();
        return _cache ??= FindNodeConfident(parentNode);
    }

    private void ClearCacheIfInvalid()
    {
        if (!GodotObject.IsInstanceValid(_cache))
        {
            _cache = null;
        }

        if (_cache?.IsQueuedForDeletion() == true)
        {
            _cache = null;
        }
    }


    private T FindNodeConfident(Node parentNode)
    {
        var maybeNode = parentNode.GetNodeOrNull<T>(NodePath);

        if (maybeNode == null)
        {
            var typeless = parentNode.GetNodeOrNull(NodePath);

            if (typeless != null)
            {
                throw new Exception(
                    $"WRONG SCRIPT - Child at path {NodePath} is a {typeless.GetType().Name}, expected {typeof(T).Name}");
            }

            if (_createFunction != null)
            {
                var newNode = _createFunction();
                parentNode.AddChild(newNode);
                return newNode;
            }

            throw new Exception($"MISSING NODE - Child at path {NodePath} does not exist");
        }

        return maybeNode;
    }
}