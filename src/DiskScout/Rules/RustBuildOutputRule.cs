namespace DiskScout.Rules;

public class RustBuildOutputRule : ISpaceWasterRule
{
    public string Category => "Rust Build Output";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("target", StringComparison.OrdinalIgnoreCase) &&
        HasCargoTomlSibling(node);

    public string GetDescription(TreeNode node) => "Rust/Cargo build output";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Clean Rust build artifacts",
        totalSize,
        "cargo clean  (in each project directory)");

    private static bool HasCargoTomlSibling(TreeNode node)
    {
        try
        {
            var parent = Path.GetDirectoryName(node.FullPath);
            if (parent == null) return false;
            return File.Exists(Path.Combine(parent, "Cargo.toml"));
        }
        catch { return false; }
    }
}
