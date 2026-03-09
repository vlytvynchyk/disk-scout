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
        "Use shallow clones, run git gc, or use Git LFS for large files",
        totalSize,
        "git gc --aggressive --prune=now");
}
