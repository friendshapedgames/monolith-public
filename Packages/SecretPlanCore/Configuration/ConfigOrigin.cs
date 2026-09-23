using SecretPlanCore.Core;

namespace SecretPlanCore.Configuration;

public readonly record struct ConfigOrigin(IFileSystem? SourceFileSystem, bool IsInLocalModsFolder)
{
    public bool IsBaseGameConfig()
    {
        return SourceFileSystem == null;
    }
}