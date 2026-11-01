using System.Diagnostics.Contracts;
using ExTween;
using Godot;

namespace SecretPlanGodot.Tweenables;

public static class Node3DExtensions
{
    [Pure]
    public static TweenableVector3 TweenableGlobalPosition(this Node3D node)
    {
        return new TweenableVector3(node.GetGlobalPosition, node.SetGlobalPosition);
    }

    [Pure]
    public static TweenableVector3 TweenableLocalPosition(this Node3D node)
    {
        return new TweenableVector3(node.GetPosition, node.SetPosition);
    }

    [Pure]
    public static TweenableFloat TweenableUniformScale(this Node3D node)
    {
        return new TweenableFloat(() => node.Scale.X, x => node.Scale = new Vector3(x, x, x));
    }

    [Pure]
    public static TweenableVector3 TweenableScale(this Node3D node)
    {
        return new TweenableVector3(node.GetScale, node.SetScale);
    }

    [Pure]
    public static TweenableVector3 TweenableGlobalRotationRadians(this Node3D node)
    {
        return new TweenableVector3(node.GetGlobalRotation, node.SetGlobalRotation);
    }

    [Pure]
    public static TweenableVector3 TweenableLocalRotationRadians(this Node3D node)
    {
        return new TweenableVector3(node.GetRotation, node.SetRotation);
    }

    [Pure]
    public static TweenableQuaternion TweenableGlobalRotationQuaternion(this Node3D node)
    {
        // Lambda (not a method group) so GlobalBasis is re-read on every get instead of captured once
        return new TweenableQuaternion(() => node.GlobalBasis.GetRotationQuaternion(), quaternion =>
        {
            var scale = node.Scale;
            node.GlobalBasis = new Basis(quaternion.Normalized());
            node.Scale = scale;
        });
    }

    [Pure]
    public static TweenableQuaternion TweenableLocalRotationQuaternion(this Node3D node)
    {
        return new TweenableQuaternion(node.GetQuaternion, node.SetQuaternion);
    }
}
