namespace DiskScout.Rules;

public class NodeModulesRule : ISpaceWasterRule
{
    public string Category => "node_modules";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);

    public string GetDescription(TreeNode node) => "Node.js dependency folder";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Delete node_modules folders and reinstall when needed",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Run in PowerShell: npx npkill  (interactive node_modules cleaner)"
            : "Run in terminal: npx npkill  (interactive node_modules cleaner)");
}
