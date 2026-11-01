namespace ControlRoomLib.Core;

public enum MergeMainBehavior
{
    /// <summary>
    ///     Safe default
    /// </summary>
    AskBeforeMergingMain,

    /// <summary>
    ///     Playtests want this because they want the latest and greatest stuff
    /// </summary>
    AlwaysMergeMain,

    /// <summary>
    ///     Typically demos don't want main once the fullgame is under development
    /// </summary>
    NeverMergeMain
}

public readonly record struct SteamBuildInfo(
    long AppId,
    string AppExeName,
    string BranchToForkFrom,
    MergeMainBehavior MergeMainBehavior,
    SteamDepotInfo[] Depots)
{
    public IEnumerable<SteamUploadSku> UploadSkus()
    {
        foreach (var depot in Depots)
        {
            yield return new SteamUploadSku(AppId, depot.DepotId, depot.OperatingSystem);
        }
    }
}

public readonly record struct SteamDepotInfo(long DepotId, BuildTarget OperatingSystem);

public readonly record struct SteamUploadSku(long AppId, long DepotId, BuildTarget OperatingSystem)
{
    public override string ToString()
    {
        return $"Depot: {DepotId} for {OperatingSystem}";
    }
}