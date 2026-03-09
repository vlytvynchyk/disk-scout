namespace DiskScout.Rules;

public class TempFilesRule : ISpaceWasterRule
{
    public string Category => "Temp Files";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("Temp", StringComparison.OrdinalIgnoreCase) &&
        (node.FullPath.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase) ||
         node.FullPath.Equals(
             Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
             StringComparison.OrdinalIgnoreCase));

    public string GetDescription(TreeNode node) => "Temporary files";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Delete temporary files",
        totalSize,
        "cleanmgr  (Windows Disk Cleanup)");
}
