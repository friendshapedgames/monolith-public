namespace SecretPlanCore.Core;


public sealed class AllocationScope : IDisposable
{
    private readonly string _name;
    private readonly long _start;
    
    public AllocationScope(string name)
    {
        _name = name;
        _start = GC.GetAllocatedBytesForCurrentThread();
    }

    public void Dispose()
    {
        var allocated =
            GC.GetAllocatedBytesForCurrentThread() - _start;

        if (allocated != 0)
        {
            Print?.Invoke([$"{_name}: {allocated:N0} bytes"]);
        }
    }

    public static event Action<object[]>? Print;
}

public static class PerfClamps
{
    private static string _currentStackString = string.Empty;
    private static readonly Stack<Item> _currentStack = new();

    public static void Start(string name)
    {
        _currentStack.Push(new Item(name, TimeUtilities.TimeNowMilliseconds()));
        _currentStackString = GenerateCurrentStackString();
    }

    public static void End(string targetName)
    {
#if DEBUG
        if (_currentStack.TryPeek(out var topOfStack))
        {
            if (topOfStack.Name != targetName)
            {
                throw new Exception($"Tried to pop {targetName} when {topOfStack.Name} was at the top of the stack");
            }

            var time = TimeUtilities.TimeNowMilliseconds() - topOfStack.StartTimeMilliseconds;
            Print?.Invoke([_currentStackString + $" completed with {time}ms"]);
        }
        else
        {
            throw new Exception("Ended with nothing on the stack");
        }
#endif

        _currentStack.Pop();
        _currentStackString = GenerateCurrentStackString();
    }

    private static string GenerateCurrentStackString()
    {
        return string.Join(".",
            _currentStack.Reverse().Select(a => $"[color={Hashing.HashStringToColor(a.Name)}]" + a.Name + "[/color]"));
    }

    public static event Action<object[]>? Print;

    public static Canary CreateCanary(string name)
    {
        return new Canary(name);
    }

    public readonly record struct Item(string Name, long StartTimeMilliseconds);

    public class Canary : IDisposable
    {
        private readonly string _name;

        public Canary(string name)
        {
            _name = name;
            Start(_name);
        }

        public void Dispose()
        {
            End(_name);
        }
    }
}