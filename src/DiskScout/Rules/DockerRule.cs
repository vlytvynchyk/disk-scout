namespace DiskScout.Rules;

public class DockerRule : ISpaceWasterRule
{
    public string Category => "Docker";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("Docker", StringComparison.OrdinalIgnoreCase) &&
        (node.FullPath.Contains("ProgramData", StringComparison.OrdinalIgnoreCase) ||
         node.FullPath.Contains("AppData", StringComparison.OrdinalIgnoreCase));

    public string GetDescription(TreeNode node) => "Docker data";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Remove unused Docker images and containers",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Run in PowerShell: docker system prune -a"
            : "Run in terminal: docker system prune -a");
}
