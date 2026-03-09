namespace DiskScout.Rules;

public class WslRule : ISpaceWasterRule
{
    public string Category => "WSL";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("LocalState", StringComparison.OrdinalIgnoreCase) &&
        node.FullPath.Contains("Packages", StringComparison.OrdinalIgnoreCase) &&
        HasVhdxFile(node);

    public string GetDescription(TreeNode node) => "WSL virtual disk image";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Compact WSL virtual disk to reclaim unused space",
        totalSize,
        "wsl --shutdown && diskpart (select vdisk file=<path>\\ext4.vhdx, attach vdisk readonly, compact vdisk, detach vdisk)");

    private static bool HasVhdxFile(TreeNode node)
    {
        try
        {
            return Directory.EnumerateFiles(node.FullPath, "*.vhdx").Any();
        }
        catch { return false; }
    }
}
