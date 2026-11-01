using System.Collections.Generic;
using Godot;

namespace SecretPlanGodot.Core;

public static class Rect2Extensions
{
    public static bool Contains(this Rect2 rect, Vector2 point)
    {
        return point.X >= rect.Position.X && point.X <= rect.End.X &&
               point.Y >= rect.Position.Y && point.Y <= rect.End.Y;
    }

    public static Rect2 MovedTo(this Rect2 rect, Vector2 newPosition)
    {
        var result = rect;
        result.Position = newPosition;
        return result;
    }

    public static Rect2 MovedToOrigin(this Rect2 rect)
    {
        return rect.MovedTo(Vector2.Zero);
    }

    public static Vector2 TopLeft(this Rect2 rect)
    {
        return rect.Position;
    }

    public static Vector2 TopRight(this Rect2 rect)
    {
        return new Vector2(rect.End.X, rect.Position.Y);
    }

    public static Vector2 BottomRight(this Rect2 rect)
    {
        return rect.End;
    }

    public static Vector2 BottomLeft(this Rect2 rect)
    {
        return new Vector2(rect.Position.X, rect.End.Y);
    }

    public static Vector2 PercentAlongPerimeter(this Rect2 rect, float percent)
    {
        if (percent < 0 || percent > 1f)
        {
            return rect.Position;
        }

        if (percent <= 0.25f)
        {
            return rect.Position.Lerp(rect.TopRight(), percent * 4f);
        }

        if (percent <= 0.5f)
        {
            return rect.TopRight().Lerp(rect.End, (percent - 0.25f) * 4f);
        }

        if (percent <= 0.75f)
        {
            return rect.End.Lerp(rect.BottomLeft(), (percent - 0.5f) * 4f);
        }

        if (percent <= 1f)
        {
            return rect.BottomLeft().Lerp(rect.Position, (percent - 0.75f) * 4f);
        }

        // This will never get reached
        return rect.Position;
    }

    public static Vector2[] GetCornersBetweenPercentagesAlongPerimeter(this Rect2 rect, float startPercent, float endPercent)
    {
        // Corners sit at 0, 0.25, 0.5, 0.75 (and 1 == 0 again), clockwise from TopLeft.
        // A corner is included if it's in [start, end), so start == 0 picks up TopLeft but end == 1 doesn't double it.
        var corners = new[] { rect.TopLeft(), rect.TopRight(), rect.BottomRight(), rect.BottomLeft() };

        // If the range wraps past the top-left corner, unroll it so end > start (eg: 0.8 -> 0.1 becomes 0.8 -> 1.1)
        if (endPercent < startPercent)
        {
            endPercent += 1f;
        }

        var result = new List<Vector2>();

        // Walk corner indices starting at the first corner at-or-after start; index 4 wraps back to TopLeft
        for (var cornerIndex = (int) Mathf.Ceil(startPercent * 4f); cornerIndex * 0.25f < endPercent; cornerIndex++)
        {
            result.Add(corners[cornerIndex % 4]);
        }

        return result.ToArray();
    }

    public static Rect2 ToRect2(this Rect2I rect2I)
    {
        return new Rect2(rect2I.Position, rect2I.Size);
    }
}