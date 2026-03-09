namespace DiskScout.Rules;

public class RecycleBinRule : ISpaceWasterRule
{
    public string Category => "Recycle Bin";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase);

    public string GetDescription(TreeNode node) => "Deleted files in Recycle Bin";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Empty the Recycle Bin",
        totalSize,
        "Clear-RecycleBin -Force");
}
