using Godot;
using SecretPlanCore.Core;
using SecretPlanGodot.Configuration;
using SecretPlanGodot.Serialization;
using FileAccess = Godot.FileAccess;

namespace SecretPlanGodot.Core;

public static class GodotUtilities
{
    /// <summary>
    /// Cached list of the names of all resources
    /// </summary>
    private static string[]? _cachedResourceList;

    public static RealFileSystem EditorProjectRoot => new(ProjectSettings.GlobalizePath("res://"));

    private static IEnumerable<string> GetCachedResourceFiles(Func<string, bool>? filter)
    {
        return _cachedResourceList ??= LoadCachedAllResourcesFiltered(filter).ToArray();
    }

    public static void InvalidateResourceCache()
    {
        _cachedResourceList = null;
        SecretResourceLoader.InvalidateCache();
    }


    public static IEnumerable<string> GetAndCacheAllResFiles(Func<string, bool>? shouldAllow)
    {
        return GetCachedResourceFiles(shouldAllow);
    }

    /// <summary>
    ///     This expects Config to have already been loaded because we will check your DLC status
    /// </summary>
    private static IEnumerable<string> LoadCachedAllResourcesFiltered(Func<string, bool>? shouldAllow)
    {
        foreach (var resPath in SecretResourceLoader.ListDirectoryRecursive("res://"))
        {
            if (resPath.StartsWith("res://.godot"))
            {
                continue;
            }

            if (resPath.EndsWith(".import"))
            {
                continue;
            }

            if (resPath.EndsWith(".uid"))
            {
                continue;
            }

            if (shouldAllow != null && !shouldAllow(resPath))
            {
                continue;
            }

            yield return resPath;
        }

        foreach (var modFolder in SecretResourceLoader.GetAllModFoldersGlobalized())
        {
            var modFiles = new RealFileSystem(modFolder);
            foreach (var filePath in modFiles.GetFilesAt("."))
            {
                yield return $"mods://{filePath}";
            }
        }
    }

    public static string[] ReadTextResourceFileLines(string path)
    {
        return ReadTextResourceFile(path).SplitLines();
    }

    public static string ReadTextResourceFile(string path)
    {
        using var fileAccess = FileAccess.Open(path, FileAccess.ModeFlags.Read);

        if (fileAccess == null)
        {
            LocalClient.Error($"Could not read file: {path}");
            return string.Empty;
        }

        return fileAccess.GetAsText();
    }

    public static Color RandomBrightColor(NoiseBasedRng random)
    {
        var r = random.NextByte();
        var g = random.NextByte();
        var b = random.NextByte();
        const double minBrightness = 0.5;
        var brightness = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        if (brightness < minBrightness)
        {
            var factor = minBrightness / brightness;
            r = (byte)Math.Min(255, r * factor);
            g = (byte)Math.Min(255, g * factor);
            b = (byte)Math.Min(255, b * factor);
        }

        return new Color(r / 255f, g / 255f, b / 255f);
    }

    public static void ShellShowFile(string globalizedPath)
    {
        OS.ShellShowInFileManager(globalizedPath);
    }

    public static void ShellOpen(string globalizedPath)
    {
        OS.ShellOpen(globalizedPath);
    }

    public static IEnumerable<SerializedColorRgb> GetMostCommonColorsFromTexture(
        ResourceReference<Texture2D> textureReference)
    {
        var texture = textureReference.CachedResource.GetOrLoadOrNull();
        var image = texture?.GetImage();

        if (image == null)
        {
            yield break;
        }

        var decompress = image.Decompress();
        if (decompress != Error.Ok)
        {
            yield break;
        }

        var colorLookup = new Dictionary<Color, int>();
        for (var x = 0; x < image.GetWidth(); x++)
        {
            for (var y = 0; y < image.GetHeight(); y++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.A8 >= 240)
                {
                    colorLookup.TryAdd(pixel, 0);
                    colorLookup[pixel]++;
                }
            }
        }

        var sorted = colorLookup.OrderByDescending(keyValue => keyValue.Value).ToList();

        if (sorted.Count == 0)
        {
            yield break;
        }

        var numberOfColorsToTake = 3;
        while (numberOfColorsToTake > 0 && sorted.Count > 0)
        {
            yield return SerializedColorRgb.FromColor(sorted.First().Key);
            sorted.RemoveAt(0);
            numberOfColorsToTake--;
        }
    }


    public static T InstantiateScene<T>(string path) where T : Node
    {
        return new CachedPackedScene<T>(path).GetOrLoad().Instantiate<T>();
    }

    public static T? LoadResource<T>(string path) where T : Resource
    {
        return new CachedResource<T>(path).GetOrLoadOrNull();
    }
}