using Godot;
using Array = Godot.Collections.Array;

namespace SecretPlanGodot.Core;

public class CachedResource<T> where T : Resource
{
    public readonly string Path;
    private T? _cache;

    public CachedResource(string path)
    {
        Path = path;
    }

    public bool IsValid()
    {
        return SecretResourceLoader.ExistsInAnyContext(Path);
    }

    public T? GetOrLoadOrNull()
    {
        return _cache ??= SecretResourceLoader.LoadTyped<T>(Path);
    }

    public T GetOrLoad()
    {
        return _cache ??= SecretResourceLoader.LoadTypedConfident<T>(Path);
    }

    public T GetOrLoadDuplicate(bool isDeep = false)
    {
        var duplicate = GetOrLoad().Duplicate(isDeep);

        if (duplicate is not T casted)
        {
            throw new Exception($"Failed to cast duplicate ({duplicate}) as {typeof(T).Name}");
        }

        return casted;
    }

    public LoadingHandle? LoadAsync(Action<T>? onLoadFinished)
    {
        return new LoadingHandle(Path, StartAsyncLoad, GetAsyncLoadStatus, () => { onLoadFinished?.Invoke(FinishAsyncLoad()); });
    }

    protected Error StartAsyncLoad()
    {
        var error = SecretResourceLoader.LoadThreadedRequest(Path);
        if (error != Error.Ok)
        {
            throw new Exception($"Failed to find resource {Path}");
        }

        return error;
    }

    protected (ResourceLoader.ThreadLoadStatus, float) GetAsyncLoadStatus()
    {
        return SecretResourceLoader.LoadThreadedGetStatus(Path);
    }

    protected T FinishAsyncLoad()
    {
        var loadedResult = SecretResourceLoader.LoadThreadedGet(Path);
        return loadedResult as T ??
               throw new Exception($"Could not cast {loadedResult?.GetType().Name} to {typeof(T).Name}");
    }
}