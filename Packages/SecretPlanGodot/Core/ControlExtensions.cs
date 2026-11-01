using Godot;
using Range = Godot.Range;

namespace SecretPlanGodot.Core;

public static class ControlExtensions
{
    public static void SetFocusNeighbors(this Control self, NavigationNeighbor top, NavigationNeighbor right,
        NavigationNeighbor bottom,
        NavigationNeighbor left)
    {
        self.SetFocusNeighbor(Side.Top, top.GetPath(self));
        self.SetFocusNeighbor(Side.Left, left.GetPath(self));
        self.SetFocusNeighbor(Side.Right, right.GetPath(self));
        self.SetFocusNeighbor(Side.Bottom, bottom.GetPath(self));
    }

    public static bool IsInteractive(this Control? self)
    {
        if (!self.IsValidAndInsideTree())
        {
            return false;
        }

        return self is BaseButton or LineEdit or TextEdit or Range;
    }
}