using System.Text;
using Godot;
using SecretPlanGodot.Configuration;
using SecretPlanGodot.Serialization;
using Array = Godot.Collections.Array;

namespace SecretPlanGodot.Core;

[Flags]
public enum ResourcePathType
{
    /// <summary>
    ///     Path does not start with res:// or user:// or mods:// (assumed to be a global path)
    /// </summary>
    Global = 0,

    /// <summary>
    ///     Path starts with res://
    /// </summary>
    GodotRes = 1,

    /// <summary>
    ///     Path starts with user://
    /// </summary>
    GodotUser = 2,
    
    /// <summary>
    ///     Path starts with mods://, this is inherently ambiguous
    /// </summary>
    ModsAmbiguous = 4
}

public static class SecretResourceLoader
{
    public const string ModsPrefix = "mods://";
    public const string UserModsFolder = "user://Mods/";
    private static readonly Dictionary<string, Resource> _cache = new();

    private static readonly List<ModRedirect> _modRedirects = [ModRedirect.Create(UserModsFolder)];

    public static IEnumerable<string> GetAllModFoldersGlobalized()
    {
        return _modRedirects.Select(redirect => GlobalizePath(redirect.PrefixPath, redirect.PathType));
    }

    public static void InvalidateCache()
    {
        _cache.Clear();
    }

    private static IEnumerable<ResourcePath> GetModPathsIfApplicable(string pathWithModPrefix)
    {
        if (!IsModdingEnabledAndStartsWithModPrefix(pathWithModPrefix))
        {
            yield break;
        }

        foreach (var folderPath in _modRedirects)
        {
            yield return folderPath.ConvertedPath(pathWithModPrefix);
        }
    }

    public static void AddModRedirect(string path)
    {
        _modRedirects.Add(ModRedirect.Create(path));
    }

    public static void RemoveModRedirect(string path)
    {
        _modRedirects.Remove(ModRedirect.Create(path));
    }

    public static Resource? LoadTypeless(string path)
    {
        if (_cache.TryGetValue(path, out var cachedTypeless))
        {
            return cachedTypeless;
        }

        var result = LoadTypelessInternal(path);

        if (result != null)
        {
            _cache[path] = result;
        }

        return result;
    }

    private static Resource? LoadTypelessInternal(string path)
    {
        if (ResourceLoader.Exists(path))
        {
            return ResourceLoader.Load(path);
        }

        foreach (var moddedPath in GetModPathsIfApplicable(path))
        {
            var maybeResource = moddedPath.LoadOrNull();
            if (maybeResource != null)
            {
                return maybeResource;
            }
        }

        return null;
    }

    private static bool IsModdingEnabledAndStartsWithModPrefix(string path)
    {
        return LocalClient.IsModdingEnabled && path.StartsWith(ModsPrefix);
    }

    /// <summary>
    ///     Loads a resource from a file, guessing the file's type based on its extension
    /// </summary>
    /// <returns></returns>
    private static Resource? GuessResourceTypeAndLoadFromFile(string path)
    {
        if (ResourceReferenceUtilities.PathLooksLikeResource<Texture2D>(path))
        {
            var image = Image.LoadFromFile(path);
            return new ImageTexture { Image = image };
        }

        if (ResourceReferenceUtilities.PathLooksLikeResource<AudioStreamOggVorbis>(path))
        {
            return AudioStreamOggVorbis.LoadFromFile(path);
        }

        if (ResourceReferenceUtilities.PathLooksLikeResource<AudioStreamWav>(path))
        {
            return AudioStreamWav.LoadFromFile(path);
        }

        return null;
    }

    public static T? LoadTyped<T>(string path) where T : Resource
    {
        return LoadTypeless(path) as T;
    }

    public static T LoadTypedConfident<T>(string path) where T : Resource
    {
        if (!ExistsInAnyContext(path))
        {
            throw new Exception($"Failed to find resource {path}");
        }

        return LoadTyped<T>(path)!;
    }

    public static IEnumerable<string> ListDirectoryRecursive(string path, bool ignoreDotGodot = true)
    {
        foreach (var entry in ResourceLoader.ListDirectory(path))
        {
            if (ignoreDotGodot && entry == "res://.godot/")
            {
                continue;
            }

            var fullPathStringBuilder = new StringBuilder();
            if (path.EndsWith('/'))
            {
                fullPathStringBuilder
                    .Append(path)
                    .Append(entry);
            }
            else
            {
                fullPathStringBuilder
                    .Append(path)
                    .Append('/')
                    .Append(entry);
            }

            var fullPath = fullPathStringBuilder.ToString();

            if (entry.EndsWith('/'))
            {
                foreach (var file in ListDirectoryRecursive(fullPath.TrimEnd('/')))
                {
                    yield return file;
                }
            }
            else
            {
                yield return fullPath;
            }
        }
    }

    [Obsolete("Use ExistsInAnyContext()")]
    public static bool Exists(string path)
    {
        return ExistsInAnyContext(path);
    }

    public static bool ExistsInAnyContext(string path)
    {
        var basicExists = ResourceLoader.Exists(path);

        if (basicExists)
        {
            return basicExists;
        }

        foreach (var moddedPath in GetModPathsIfApplicable(path))
        {
            if (moddedPath.FileExists())
            {
                return true;
            }
        }

        return false;
    }

    public static Error LoadThreadedRequest(string path)
    {
        foreach (var modPath in GetModPathsIfApplicable(path))
        {
            if (modPath.FileExists())
            {
                return ResourceLoader.LoadThreadedRequest(modPath.Path);
            }
        }

        return ResourceLoader.LoadThreadedRequest(path);
    }

    public static (ResourceLoader.ThreadLoadStatus, float) LoadThreadedGetStatus(string path)
    {
        foreach (var modPath in GetModPathsIfApplicable(path))
        {
            if (modPath.FileExists())
            {
                return LoadThreadedGetStatusInternal(modPath.Path);
            }
        }

        return LoadThreadedGetStatusInternal(path);
    }

    private static (ResourceLoader.ThreadLoadStatus, float) LoadThreadedGetStatusInternal(string path)
    {
        Array outArray = [1];
        var status = ResourceLoader.LoadThreadedGetStatus(path, outArray);
        return (status, outArray[0].As<float>());
    }

    public static Resource LoadThreadedGet(string path)
    {
        foreach (var modPath in GetModPathsIfApplicable(path))
        {
            if (modPath.FileExists())
            {
                return ResourceLoader.LoadThreadedGet(modPath.Path);
            }
        }

        return ResourceLoader.LoadThreadedGet(path);
    }

    public static string GlobalizePath(string path)
    {
        return GlobalizePath(path, GetPathTypeFromPath(path));
    }

    /// <summary>
    /// This will not tolerate res:// paths because globalizing res paths does not work on export
    /// </summary>
    public static string GlobalizePath(string path, ResourcePathType pathType)
    {
        foreach (var modPath in GetModPathsIfApplicable(path))
        {
            return GlobalizePathInternal(modPath.Path, modPath.PathType);
        }

        return GlobalizePathInternal(path, pathType);
    }

    /// <summary>
    /// This will not tolerate res:// paths because globalizing res paths does not work on export
    /// </summary>
    private static string GlobalizePathInternal(string path, ResourcePathType pathType)
    {
        if (pathType == ResourcePathType.GodotUser)
        {
            return ProjectSettings.GlobalizePath(path);
        }

        if (pathType == ResourcePathType.ModsAmbiguous)
        {
            foreach (var modPath in GetModPathsIfApplicable(path))
            {
                if (modPath.FileExists() || modPath.DirectoryExists())
                {
                    return GlobalizePathInternal(modPath.Path, modPath.PathType);
                }
            }
        }

        if (pathType == ResourcePathType.GodotRes)
        {
            // this shouldn't happen, you shouldn't be adding res:// paths to your modding redirects
            throw new Exception("Cannot globalize a res:// path in an exported project");
        }

        return path;
    }

    private static ResourcePathType GetPathTypeFromPath(string path)
    {
        if (path.StartsWith("res://"))
        {
            return ResourcePathType.GodotRes;
        }

        if (path.StartsWith("user://"))
        {
            return ResourcePathType.GodotUser;
        }

        if (path.StartsWith(ModsPrefix))
        {
            return ResourcePathType.ModsAmbiguous;
        }

        return ResourcePathType.Global;
    }

    /// <summary>
    ///     Represents a PrefixPath that will replace mods://
    /// </summary>
    private readonly record struct ModRedirect(string PrefixPath, ResourcePathType PathType)
    {
        public ResourcePath ConvertedPath(string pathWithModPrefix)
        {
            return new ResourcePath(pathWithModPrefix.Replace(ModsPrefix, PrefixPath), PathType);
        }

        public static ModRedirect Create(string path)
        {
            var resultPath = path;
            if (!path.EndsWith("/"))
            {
                resultPath += "/";
            }

            var pathType = GetPathTypeFromPath(path);

            if (pathType == ResourcePathType.ModsAmbiguous)
            {
                throw new Exception($"Mod redirect starts with {ModsPrefix}, this is recursive!");
            }
            
            return new ModRedirect(resultPath, pathType);
        }
    }

    private readonly record struct ResourcePath(string Path, ResourcePathType PathType)
    {
        public Resource? LoadOrNull()
        {
            if (ResourceLoader.Exists(Path))
            {
                // This might return null if it's a .png without the appropriate import metadata
                var basicLoad = ResourceLoader.Load(Path);

                if (basicLoad != null)
                {
                    return basicLoad;
                }
            }

            if (FileExists())
            {
                return GuessResourceTypeAndLoadFromFile(Path);
            }

            return null;
        }

        public bool FileExists()
        {
            if (PathType == ResourcePathType.GodotRes)
            {
                return ResourceLoader.Exists(Path);
            }

            return File.Exists(GlobalizePath(Path, PathType));
        }

        public bool DirectoryExists()
        {
            return Directory.Exists(GlobalizePath(Path, PathType));
        }
    }
}