using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PathFinding.Shared.Helpers;

public static class Extensions
{
    public static IEnumerable<T> TraceCount<T>(this IEnumerable<T> enumerable, string somethingToSay)
    {

        Trace.WriteLine($"{somethingToSay}: {enumerable.Count()}");
        return enumerable;
    }

    public static List<T> RemoveItems<T>(this List<T> enumerable, IEnumerable<T> toRemoves)
    {
        foreach (var item in toRemoves) { enumerable.Remove(item); }
        return enumerable;
    }

    public static IEnumerable<T> ModifyItems<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var item in enumerable) { action(item); }
        return enumerable;
    }

    public static string OutputCoordinates<T>(this IEnumerable<T> enumerable, string seprator = ", ")
    {
        return string.Join(seprator, enumerable.Select(x => $"{x}"));
    }


}