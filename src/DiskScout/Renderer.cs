namespace DiskScout;

public record RenderOptions(int Depth = 3, long MinimumSize = 1024 * 1024, bool NoColor = false);

public class Renderer
{
    private readonly RenderOptions _options;
    private int _consoleWidth;

    private const string FullBlock = "\u2588";
    private const string LightBlock = "\u2591";

    public Renderer(RenderOptions? options = null)
    {
        _options = options ?? new RenderOptions();
        try { _consoleWidth = Console.WindowWidth; }
        catch { _consoleWidth = 120; }
        if (_consoleWidth < 60) _consoleWidth = 60;
    }

    // ANSI color helpers
    private string Red(string s) => _options.NoColor ? s : $"\x1b[91m{s}\x1b[0m";
    private string Yellow(string s) => _options.NoColor ? s : $"\x1b[93m{s}\x1b[0m";
    private string Green(string s) => _options.NoColor ? s : $"\x1b[32m{s}\x1b[0m";
    private string Cyan(string s) => _options.NoColor ? s : $"\x1b[36m{s}\x1b[0m";
    private string Gray(string s) => _options.NoColor ? s : $"\x1b[90m{s}\x1b[0m";
    private string Bold(string s) => _options.NoColor ? s : $"\x1b[1m{s}\x1b[0m";
    private string DimWhite(string s) => _options.NoColor ? s : $"\x1b[37m{s}\x1b[0m";

    private string Colorize(string s, long size)
    {
        if (size >= 1L * 1024 * 1024 * 1024) return Red(s);
        if (size >= 100L * 1024 * 1024) return Yellow(s);
        if (size >= 10L * 1024 * 1024) return DimWhite(s);
        return Green(s);
    }

    public void RenderHeader(string path, TreeNode root)
    {
        Console.WriteLine();
        Console.WriteLine(Bold($"  Disk Usage Analysis: {path}"));
        Console.WriteLine(Bold($"  Total: {FormatSize(root.TotalSize)}  |  {root.FileCount:N0} files  |  {root.DirectoryCount:N0} directories"));

        // Drive info
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path)!);
            if (drive.IsReady)
            {
                var used = drive.TotalSize - drive.AvailableFreeSpace;
                var pct = 100.0 * used / drive.TotalSize;
                var barWidth = 40;
                var filled = (int)(barWidth * pct / 100);
                var bar = new string('\u2588', filled) + new string('\u2591', barWidth - filled);
                var color = pct > 90 ? Red(bar) : pct > 75 ? Yellow(bar) : Green(bar);

                Console.WriteLine($"  Drive {drive.Name}  [{color}]  {pct:F1}% used  ({FormatSize(drive.AvailableFreeSpace)} free of {FormatSize(drive.TotalSize)})");
            }
        }
        catch { }
        Console.WriteLine(new string('\u2500', Math.Min(_consoleWidth - 2, 100)));
    }

    public void RenderTree(TreeNode node, int maxItems = 25)
    {
        Console.WriteLine();
        Console.WriteLine(Bold("  Largest directories and files:"));
        Console.WriteLine();

        var children = node.Children
            .Where(c => c.TotalSize >= _options.MinimumSize)
            .Take(maxItems)
            .ToList();

        if (children.Count == 0)
        {
            Console.WriteLine("  No items above minimum size threshold.");
            return;
        }

        long maxSize = children.Max(c => c.TotalSize);
        int barWidth = 24;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var pct = node.TotalSize > 0 ? 100.0 * child.TotalSize / node.TotalSize : 0;
            var filled = maxSize > 0 ? (int)(barWidth * (double)child.TotalSize / maxSize) : 0;
            if (filled < 1 && child.TotalSize > 0) filled = 1;

            var bar = new string('\u2588', filled) + new string('\u2591', barWidth - filled);
            var sizeStr = FormatSize(child.TotalSize).PadLeft(10);
            var pctStr = $"{pct,5:F1}%";

            var nameDisplay = child.Name;
            if (child.IsDirectory) nameDisplay += "/";
            if (child.IsSymlink) nameDisplay += " -> (link)";
            if (child.AccessDenied) nameDisplay += " (access denied)";

            var indexStr = child.IsDirectory && !child.Name.StartsWith("[")
                ? $"  [{i + 1,2}]"
                : "      ";

            var coloredBar = Colorize(bar, child.TotalSize);
            var coloredSize = Colorize(sizeStr, child.TotalSize);
            var coloredName = child.IsSymlink ? Cyan(nameDisplay)
                            : child.AccessDenied ? Gray(nameDisplay)
                            : nameDisplay;

            Console.WriteLine($"{indexStr}  {coloredBar}  {coloredSize}  {pctStr}  {coloredName}");
        }

        // Show remainder
        var shownSize = children.Sum(c => c.TotalSize);
        var remainderSize = node.TotalSize - shownSize;
        if (remainderSize > _options.MinimumSize)
        {
            Console.WriteLine($"        {new string(' ', barWidth)}  {FormatSize(remainderSize),10}  {"",5}  {Gray($"[{node.Children.Count - children.Count} more items]")}");
        }

        Console.WriteLine();
    }

    public void RenderLargestFiles(List<LargestFile> files)
    {
        if (files.Count == 0) return;

        Console.WriteLine(Bold("  Largest files:"));
        Console.WriteLine();

        foreach (var file in files)
        {
            var sizeStr = FormatSize(file.Size).PadLeft(10);
            var coloredSize = Colorize(sizeStr, file.Size);
            Console.WriteLine($"    {coloredSize}  {file.Path}");
        }
        Console.WriteLine();
    }

    public void RenderFileTypeDistribution(Dictionary<string, long> distribution, int topN = 15)
    {
        if (distribution.Count == 0) return;

        Console.WriteLine(Bold("  File types by size:"));
        Console.WriteLine();

        var top = distribution.Take(topN).ToList();
        long maxSize = top.Max(kv => kv.Value);
        int barWidth = 20;

        foreach (var (ext, size) in top)
        {
            var filled = (int)(barWidth * (double)size / maxSize);
            if (filled < 1) filled = 1;
            var bar = new string('\u2588', filled) + new string('\u2591', barWidth - filled);
            var coloredBar = Colorize(bar, size);
            Console.WriteLine($"    {ext,-18} {coloredBar}  {FormatSize(size),10}");
        }

        if (distribution.Count > topN)
        {
            var rest = distribution.Skip(topN).Sum(kv => kv.Value);
            Console.WriteLine($"    {"(other)",-18} {new string('\u2591', barWidth)}  {FormatSize(rest),10}");
        }
        Console.WriteLine();
    }

    public void RenderSpaceWasters(List<SpaceWaster> wasters)
    {
        if (wasters.Count == 0) return;

        Console.WriteLine(Bold("  Space wasters found:"));
        Console.WriteLine();

        var grouped = wasters
            .GroupBy(w => w.Category)
            .OrderByDescending(g => g.Sum(w => w.Size));

        foreach (var group in grouped)
        {
            var total = group.Sum(w => w.Size);
            Console.WriteLine($"    {Yellow(group.Key)}  ({Colorize(FormatSize(total), total)} total across {group.Count()} locations)");

            foreach (var w in group.OrderByDescending(w => w.Size).Take(5))
            {
                Console.WriteLine($"      {FormatSize(w.Size),10}  {Gray(w.Path)}");
            }
            if (group.Count() > 5)
                Console.WriteLine($"      {Gray($"... and {group.Count() - 5} more")}");
            Console.WriteLine();
        }
    }

    public void RenderSuggestions(List<CleanupSuggestion> suggestions)
    {
        if (suggestions.Count == 0) return;

        Console.WriteLine(Bold("  Cleanup suggestions:"));
        Console.WriteLine();

        int i = 1;
        foreach (var s in suggestions)
        {
            var savings = Colorize(FormatSize(s.PotentialSavings), s.PotentialSavings);
            Console.WriteLine($"    {i}. {s.Action}");
            Console.WriteLine($"       Potential savings: {savings}");
            if (!string.IsNullOrEmpty(s.Command))
                Console.WriteLine($"       Command: {Cyan(s.Command)}");
            Console.WriteLine();
            i++;
        }
    }

    public int PromptDrillDown(TreeNode node)
    {
        var navigable = node.Children
            .Where(c => c.IsDirectory && !c.IsSymlink && !c.Name.StartsWith("["))
            .ToList();

        if (navigable.Count == 0)
        {
            Console.WriteLine(Gray("  No subdirectories to drill into."));
            return -2;
        }

        while (true)
        {
            Console.Write("  Enter number to drill into a directory, ");
            Console.Write(Cyan("b"));
            Console.Write(" to go back, ");
            Console.Write(Cyan("q"));
            Console.Write(" to quit: ");

            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input) || input.Equals("q", StringComparison.OrdinalIgnoreCase))
                return -2; // quit

            if (input.Equals("b", StringComparison.OrdinalIgnoreCase))
                return -1; // back

            if (int.TryParse(input, out int index))
            {
                var displayChildren = node.Children
                    .Where(c => c.TotalSize >= _options.MinimumSize)
                    .Take(25)
                    .ToList();

                if (index >= 1 && index <= displayChildren.Count)
                {
                    var selected = displayChildren[index - 1];
                    if (selected.IsDirectory && !selected.Name.StartsWith("["))
                        return node.Children.IndexOf(selected);
                }
            }

            Console.WriteLine(Gray("  Invalid selection. Enter a directory number, 'b', or 'q'."));
        }
    }

    public void RenderProgress(long dirs, string currentPath)
    {
        var truncated = TruncatePath(currentPath, _consoleWidth - 35);
        Console.Write($"\r  Scanning... {dirs:N0} directories  {Gray(truncated)}".PadRight(_consoleWidth - 1));
    }

    public void ClearProgress()
    {
        Console.Write("\r" + new string(' ', _consoleWidth - 1) + "\r");
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "???";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes < 1024L * 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        return $"{bytes / (1024.0 * 1024 * 1024 * 1024):F2} TB";
    }

    private static string TruncatePath(string path, int maxLen)
    {
        if (path.Length <= maxLen) return path;
        if (maxLen < 10) return "...";
        return "..." + path[^(maxLen - 3)..];
    }
}
