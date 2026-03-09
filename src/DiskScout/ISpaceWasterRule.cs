namespace DiskScout;

public interface ISpaceWasterRule
{
    string Category { get; }
    bool Matches(TreeNode node);
    string GetDescription(TreeNode node);
    CleanupSuggestion CreateSuggestion(long totalSize);
}
