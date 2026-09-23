using System.Collections;
using System.Diagnostics.Contracts;

namespace SecretPlanCore.Core;

public static class ListExtensions
{
    [Pure]
    public static bool IsValidIndex(this IList list, int i)
    {
        return i >= 0 && i < list.Count;
    }


    /// <summary>
    ///     Might be dangerous for covarient arrays (object[] = new string[10])
    /// </summary>
    [Pure]
    public static int ArrayIndexOf<T>(this T[] array, T element)
    {
        return Array.IndexOf(array, element, 0, array.Length);
    }

    public static void MoveElementUp(this IList list, int index)
    {
        list.SwapElement(index, index - 1);
    }
    
    public static void MoveElementDown(this IList list, int index)
    {
        list.SwapElement(index, index + 1);
    }

    public static void SwapElement(this IList list, int a, int b)
    {
        if (list.IsValidIndex(a) && list.IsValidIndex(b))
        {
            (list[a], list[b]) = (list[b], list[a]);
        }
    }
}