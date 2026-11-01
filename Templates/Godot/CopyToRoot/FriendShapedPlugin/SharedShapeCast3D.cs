using System.Collections.Generic;
using FriendShapedDistributable;
using Godot;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin;

public partial class SharedShapeCast3D : ShapeCast3D
{
    public override void _EnterTree()
    {
        Enabled = false;
        CollideWithAreas = false;
        CollideWithBodies = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
    }
    
    public IEnumerable<ShapeCastResult3D> DoShapeCast(Shape3D shape, Vector3 globalStart, Vector3 relativeEnd,
        CollisionLayersMaskEnum maskEnum)
    {
        Shape = shape;
        CollisionMask = (uint) maskEnum;
        GlobalPosition = globalStart;
        TargetPosition = relativeEnd;
        ForceShapecastUpdate();

        if (IsColliding())
        {
            for (var i = 0; i < GetCollisionCount(); i++)
            {
                yield return new ShapeCastResult3D
                {
                    FoundSomething = true,
                    CollisionPoint = GetCollisionPoint(i),
                    Rid = GetColliderRid(i),
                    ShapeIndex = GetColliderShape(i),
                    FoundBody = GetCollider(i),
                    Normal = GetCollisionNormal(i)
                };
            }
        }
    }
}
