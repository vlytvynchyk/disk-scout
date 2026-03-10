namespace DiskScout.Rules;

public class DotNetBuildOutputRule : ISpaceWasterRule
{
    public string Category => ".NET Build Output";

    public bool Matches(TreeNode node)
    {
        var name = node.Name;
        if (!name.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("obj", StringComparison.OrdinalIgnoreCase))
            return false;

        return HasProjectFileSibling(node);
    }

    public string GetDescription(TreeNode node) => $".NET {node.Name}/ folder";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Clean .NET build artifacts",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Run in PowerShell (inside project): dotnet clean"
            : "Run in terminal (inside project): dotnet clean");

    private static bool HasProjectFileSibling(TreeNode node)
    {
        try
        {
            var parent = Path.GetDirectoryName(node.FullPath);
            if (parent == null) return false;
            return Directory.EnumerateFiles(parent, "*.csproj").Any() ||
                   Directory.EnumerateFiles(parent, "*.fsproj").Any();
        }
        catch { return false; }
    }
}
