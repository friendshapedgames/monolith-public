namespace ControlRoomLib.Programs;

public readonly record struct GitBranchInfo(string HeadSha, string BranchName);

public readonly record struct GitForkedBranchInfo(
    GitBranchInfo ForkBranch,
    GitBranchInfo SourceBranch,
    string CommonSha)
{
    public override string ToString()
    {
        return $"[{SourceBranch.BranchName}] -> [{ForkBranch.BranchName}]";
    }
}