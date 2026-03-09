namespace DiskScout.Rules;

public class PythonCacheRule : ISpaceWasterRule
{
    public string Category => "Python Cache";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase);

    public string GetDescription(TreeNode node) => "Python bytecode cache";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Delete __pycache__ directories",
        totalSize,
        "Get-ChildItem -Recurse -Directory -Filter __pycache__ | Remove-Item -Recurse");
}
