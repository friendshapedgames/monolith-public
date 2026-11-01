using Godot;
using SecretPlanCore.Core;

namespace FriendShapedDistributable;

public static class GameConstants
{
    public static readonly bool IsEditor =
#if TOOLS
        true;
#else
        false;
#endif

    public static RealFileSystem EditorProjectRoot
    {
        get
        {
#if !TOOLS
            SecretPlanGodot.Core.LocalClient.Error("EditorProjectRoot was requested outside of editor context, this won't work.");
#endif

            return new RealFileSystem(ProjectSettings.GlobalizePath("res://"));
        }
    }
}