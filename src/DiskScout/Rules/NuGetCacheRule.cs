namespace DiskScout.Rules;

public class NuGetCacheRule : ISpaceWasterRule
{
    public string Category => "NuGet Cache";

    public bool Matches(TreeNode node) =>
        node.Name.Equals("packages", StringComparison.OrdinalIgnoreCase) &&
        node.FullPath.Contains(".nuget", StringComparison.OrdinalIgnoreCase);

    public string GetDescription(TreeNode node) => "NuGet package cache";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Clear NuGet package cache",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Run in PowerShell: dotnet nuget locals all --clear"
            : "Run in terminal: dotnet nuget locals all --clear");
}
