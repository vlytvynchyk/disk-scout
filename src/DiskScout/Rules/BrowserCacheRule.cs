namespace DiskScout.Rules;

public class BrowserCacheRule : ISpaceWasterRule
{
    private static readonly Lazy<HashSet<string>> KnownCachePaths = new(BuildKnownCachePaths);

    public string Category => "Browser/App Cache";

    public bool Matches(TreeNode node)
    {
        var name = node.Name;
        if (!name.Equals("Cache", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("cache2", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("Code Cache", StringComparison.OrdinalIgnoreCase))
            return false;

        return KnownCachePaths.Value.Any(cp =>
            node.FullPath.StartsWith(cp, StringComparison.OrdinalIgnoreCase));
    }

    public string GetDescription(TreeNode node) => "Application cache";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Clear browser caches from browser settings",
        totalSize,
        "Open your browser > Settings > Privacy > Clear browsing data");

    private static HashSet<string> BuildKnownCachePaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            paths.Add(Path.Combine(localAppData, "Google", "Chrome"));
            paths.Add(Path.Combine(localAppData, "Microsoft", "Edge"));
            paths.Add(Path.Combine(localAppData, "Mozilla", "Firefox"));
            paths.Add(Path.Combine(localAppData, "BraveSoftware"));
            paths.Add(Path.Combine(localAppData, "Packages")); // UWP app caches
        }
        return paths;
    }
}
