namespace DiskScout.Rules;

public class GitRepositoryRule : ISpaceWasterRule
{
    private const long LargeGitThreshold = 50 * 1024 * 1024; // 50 MB

    public string Category => "Git Repositories";

    public bool Matches(TreeNode node) =>
        node.Name.Equals(".git", StringComparison.OrdinalIgnoreCase) &&
        node.TotalSize > LargeGitThreshold;

    public string GetDescription(TreeNode node) => "Git repository data";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Run git gc in each repo, use shallow clones, or Git LFS for large files",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Run in PowerShell (inside repo): git gc --aggressive --prune=now"
            : "Run in terminal (inside repo): git gc --aggressive --prune=now");
}
