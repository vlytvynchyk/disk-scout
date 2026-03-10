namespace DiskScout.Rules;

public class RecycleBinRule : ISpaceWasterRule
{
    public string Category => "Recycle Bin";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
        node.Name.Equals(".Trash", StringComparison.Ordinal) ||
        node.Name.Equals(".Trash-1000", StringComparison.Ordinal);

    public string GetDescription(TreeNode node) => "Deleted files in Recycle Bin / Trash";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        OperatingSystem.IsWindows() ? "Empty the Recycle Bin" : "Empty the Trash",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Clear-RecycleBin -Force"
            : "rm -rf ~/.local/share/Trash/*");
}
