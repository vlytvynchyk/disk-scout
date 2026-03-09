using DiskScout.Rules;

namespace DiskScout;

public record SpaceWaster(string Category, string Path, long Size, string Description);
public record CleanupSuggestion(string Category, string Action, long PotentialSavings, string Command);
public record LargestFile(string Path, long Size);

public record AnalysisResult(
    List<SpaceWaster> SpaceWasters,
    List<LargestFile> LargestFiles,
    Dictionary<string, long> FileTypeDistribution,
    List<CleanupSuggestion> Suggestions);

public class SpaceAnalyzer
{
    private const long SuggestionMinThreshold = 50 * 1024 * 1024; // 50 MB

    private readonly int _topFilesCount;
    private readonly IReadOnlyList<ISpaceWasterRule> _rules;

    private readonly PriorityQueue<LargestFile, long> _largestFiles = new();
    private readonly List<SpaceWaster> _wasters = [];
    private readonly Dictionary<string, long> _categoryTotals = new(StringComparer.OrdinalIgnoreCase);

    public SpaceAnalyzer(int topFilesCount = 20, IReadOnlyList<ISpaceWasterRule>? rules = null)
    {
        _topFilesCount = topFilesCount;
        _rules = rules ?? DefaultRules;
    }

    public static IReadOnlyList<ISpaceWasterRule> DefaultRules { get; } =
    [
        new NodeModulesRule(),
        new GitRepositoryRule(),
        new DotNetBuildOutputRule(),
        new NuGetCacheRule(),
        new PythonCacheRule(),
        new PythonVenvRule(),
        new RustBuildOutputRule(),
        new BrowserCacheRule(),
        new RecycleBinRule(),
        new TempFilesRule(),
        new DockerRule(),
        new WslRule(),
    ];

    public AnalysisResult Analyze(TreeNode root)
    {
        WalkTree(root);

        var largest = new List<LargestFile>();
        while (_largestFiles.Count > 0)
            largest.Add(_largestFiles.Dequeue());
        largest.Reverse();

        var suggestions = BuildSuggestions();

        var typeDist = root.FilesByExtension
            .OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return new AnalysisResult(_wasters, largest, typeDist, suggestions);
    }

    private void WalkTree(TreeNode node)
    {
        if (!node.IsDirectory)
        {
            TrackLargeFile(node);
            return;
        }

        if (node.IsSymlink || node.AccessDenied) return;

        CheckRules(node);

        foreach (var child in node.Children)
        {
            if (!child.IsDirectory)
                TrackLargeFile(child);
        }

        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
                WalkTree(child);
        }
    }

    private void CheckRules(TreeNode node)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(node))
                AddWaster(rule, node);
        }
    }

    private void AddWaster(ISpaceWasterRule rule, TreeNode node)
    {
        _wasters.Add(new SpaceWaster(rule.Category, node.FullPath, node.TotalSize, rule.GetDescription(node)));

        if (_categoryTotals.TryGetValue(rule.Category, out var existing))
            _categoryTotals[rule.Category] = existing + node.TotalSize;
        else
            _categoryTotals[rule.Category] = node.TotalSize;
    }

    private void TrackLargeFile(TreeNode node)
    {
        if (node.TotalSize <= 0) return;

        var file = new LargestFile(node.FullPath, node.TotalSize);

        if (_largestFiles.Count < _topFilesCount)
        {
            _largestFiles.Enqueue(file, node.TotalSize);
        }
        else if (_largestFiles.TryPeek(out _, out long smallest) && node.TotalSize > smallest)
        {
            _largestFiles.DequeueEnqueue(file, node.TotalSize);
        }
    }

    private List<CleanupSuggestion> BuildSuggestions()
    {
        var rulesByCategory = new Dictionary<string, ISpaceWasterRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in _rules)
            rulesByCategory.TryAdd(rule.Category, rule);

        return _categoryTotals
            .Where(kv => kv.Value >= SuggestionMinThreshold)
            .OrderByDescending(kv => kv.Value)
            .Where(kv => rulesByCategory.ContainsKey(kv.Key))
            .Select(kv => rulesByCategory[kv.Key].CreateSuggestion(kv.Value))
            .ToList();
    }
}
