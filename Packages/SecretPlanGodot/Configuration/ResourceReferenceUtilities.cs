using Godot;

namespace SecretPlanGodot.Configuration;

public static class ResourceReferenceUtilities
{
    private static Dictionary<string, Type>? _underlyingExtensionToType;

    /// <summary>
    ///     Map of extensions (.png, .ogg, etc) to resource types (Texture2D, AudioStream, PackedScene)
    /// </summary>
    private static Dictionary<string, Type> ExtensionToType
    {
        get
        {
            if (_underlyingExtensionToType == null)
            {
                _underlyingExtensionToType = new Dictionary<string, Type>
                {
                    [".png"] = typeof(Texture2D),
                    [".bmp"] = typeof(Texture2D),
                    [".tga"] = typeof(Texture2D),
                    [".ogg"] = typeof(AudioStreamOggVorbis),
                    [".wav"] = typeof(AudioStreamWav),
                    [".tscn"] = typeof(PackedScene)
                };

                foreach (var key in _underlyingExtensionToType.Keys.ToArray())
                {
                    var upperKey = key.ToUpperInvariant();
                    
                    // Allow "PNG" as well as "png" (why does windows do this)
                    _underlyingExtensionToType[upperKey] = _underlyingExtensionToType[key];
                }
            }

            return _underlyingExtensionToType;
        }
    }

    public static IResourceReference CreateResourceReference(Type resourceReferenceType, string path)
    {
        if (Activator.CreateInstance(resourceReferenceType, path) is not IResourceReference instance)
        {
            throw new Exception($"Could not build {nameof(IResourceReference)} from {resourceReferenceType.FullName}");
        }

        return instance;
    }

    public static Type GetResourceTypeFromExtension(string extensionWithDot)
    {
        if (ExtensionToType.TryGetValue(extensionWithDot, out var value))
        {
            return value;
        }

        return typeof(Resource);
    }

    public static bool PathLooksLikeResource<T>(string path) where T : Resource
    {
        return GetResourceTypeFromExtension($".{path.GetExtension()}") == typeof(T);
    }

    public static IEnumerable<string> GetSupportedFileExtensions(Type resourceReferenceType)
    {
        var resourceType = CreateResourceReference(resourceReferenceType, "").ResourceType;

        if (!resourceType.IsAssignableTo(typeof(Resource)))
        {
            yield break;
        }

        var hasFoundValidResourceType = false;

        foreach (var (extension, typeAssociatedWithExtension) in ExtensionToType)
        {
            if (typeAssociatedWithExtension.IsAssignableTo(resourceType))
            {
                yield return extension;
                hasFoundValidResourceType = true;
            }
        }

        if (!hasFoundValidResourceType)
        {
            yield return ".tres";
        }
    }
}