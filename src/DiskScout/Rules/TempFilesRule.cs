namespace DiskScout.Rules;

public class TempFilesRule : ISpaceWasterRule
{
    public string Category => "Temp Files";

    public bool Matches(TreeNode node)
    {
        if (!node.Name.Equals("Temp", StringComparison.OrdinalIgnoreCase) &&
            !node.Name.Equals("tmp", StringComparison.OrdinalIgnoreCase))
            return false;

        if (OperatingSystem.IsWindows())
        {
            return node.FullPath.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase) ||
                   node.FullPath.Equals(
                       Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                       StringComparison.OrdinalIgnoreCase);
        }

        // Linux: /tmp
        return node.FullPath.Equals("/tmp", StringComparison.Ordinal);
    }

    public string GetDescription(TreeNode node) => "Temporary files";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Delete temporary files",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Run in PowerShell: cleanmgr  (Windows Disk Cleanup)"
            : "Run in terminal (requires root): sudo rm -rf /tmp/*");
}
