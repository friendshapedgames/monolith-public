using System.Diagnostics.Contracts;
using ExTween;
using Godot;

namespace SecretPlanGodot.Tweenables;

public static class Node2DExtensions
{
    [Pure]
    public static TweenableVector2 TweenableGlobalPosition(this Node2D node)
    {
        return new TweenableVector2(node.GetGlobalPosition, x => node.SetGlobalPosition(x));
    }

    [Pure]
    public static TweenableVector2 TweenableLocalPosition(this Node2D node)
    {
        return new TweenableVector2(node.GetPosition, x => node.SetPosition(x));
    }

    [Pure]
    public static TweenableFloat TweenableGlobalRotationRadians(this Node2D node)
    {
        return new TweenableFloat(node.GetGlobalRotation, node.SetGlobalRotation);
    }

    [Pure]
    public static TweenableFloat TweenableLocalRotationRadians(this Node2D node)
    {
        return new TweenableFloat(node.GetRotation, node.SetRotation);
    }

    [Pure]
    public static TweenableFloat TweenableUniformScale(this Node2D node)
    {
        return new TweenableFloat(() => node.Scale.X, x => node.Scale = new Vector2(x, x));
    }

    [Pure]
    public static TweenableVector2 TweenableScale(this Node2D node)
    {
        return new TweenableVector2(() => node.Scale, x => node.Scale = x);
    }

    [Pure]
    public static TweenableVector2 TweenableGlobalScale(this Node2D node)
    {
        return new TweenableVector2(() => node.GlobalScale, x => node.GlobalScale = x);
    }

    [Pure]
    public static TweenableFloat TweenableSkewRadians(this Node2D node)
    {
        return new TweenableFloat(node.GetSkew, node.SetSkew);
    }
}
