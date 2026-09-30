using System.Diagnostics.CodeAnalysis;

namespace SecretPlanGodot.BindingsGenerator;

public class DistinctQueue<T>
{
    private readonly Queue<T> _queue = new();
    private readonly HashSet<T> _set = new();


    public bool TryDequeue([MaybeNullWhen(false)] out T o)
    {
        return _queue.TryDequeue(out o);
    }

    public void Enqueue(T o)
    {
        if (_set.Add(o))
        {
            _queue.Enqueue(o);
        }
    }
}