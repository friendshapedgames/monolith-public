using System.Collections.Generic;
using FriendShapedDistributable;
using Godot;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin;

public partial class SharedRaycast3D : RayCast3D
{
    public override void _EnterTree()
    {
        Enabled = false;
        CollideWithAreas = false;
        CollideWithBodies = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
    }

    /// <summary>
    /// Does a basic raycast
    /// </summary>
    public RaycastResult3D? DoRaycast(Vector3 globalStart, Vector3 relativeEnd, CollisionLayersMaskEnum maskEnum)
    {
        ClearExceptions();
        return DoRaycastIgnoringExceptions(globalStart, relativeEnd, maskEnum);
    }

    /// <summary>
    /// Does a raycast ignoring the current state of the exclusion list (assumes that it was written to intentionally)
    /// </summary>
    public RaycastResult3D? DoRaycastIgnoringExceptions(Vector3 globalStart, Vector3 relativeEnd,
        CollisionLayersMaskEnum maskEnum)
    {
        CollisionMask = (uint)maskEnum;
        GlobalPosition = globalStart;
        TargetPosition = relativeEnd;
        ForceRaycastUpdate();
        if (IsColliding())
        {
            return new RaycastResult3D
            {
                HitPosition = GetCollisionPoint(),
                Rid = GetColliderRid(),
                ShapeIndex = GetColliderShape(),
                FoundBody = GetCollider(),
                FoundSomething = true,
                Normal = GetCollisionNormal(),
                FaceIndex = GetCollisionFaceIndex()
            };
        }

        return null;
    }

    /// <summary>
    /// Does a raycast excluding the provided elements
    /// </summary>
    public RaycastResult3D? DoRaycastExcluding(Vector3 globalStart, Vector3 relativeEnd, CollisionLayersMaskEnum maskEnum, IEnumerable<CollisionObject3D> exceptions)
    {
        ClearExceptions();
        foreach (var exception in exceptions)
        {
            AddException(exception);
        }

        CollisionMask = (uint)maskEnum;
        GlobalPosition = globalStart;
        TargetPosition = relativeEnd;
        ForceRaycastUpdate();
        if (IsColliding())
        {
            return new RaycastResult3D
            {
                HitPosition = GetCollisionPoint(),
                Rid = GetColliderRid(),
                ShapeIndex = GetColliderShape(),
                FoundBody = GetCollider(),
                FoundSomething = true,
                Normal = GetCollisionNormal(),
                FaceIndex = GetCollisionFaceIndex()
            };
        }

        return null;
    }
}