namespace DiskScout;

public record ScanOptions(
    int MaxDepth = int.MaxValue,
    long MinimumSize = 0,
    HashSet<string>? ExcludeNames = null);

public class Scanner
{
    private const long LargeFileThreshold = 100 * 1024 * 1024; // 100 MB

    // Linux virtual filesystems that report fake sizes
    private static readonly HashSet<string> VirtualFsPaths = new(StringComparer.Ordinal)
    {
        "/proc", "/sys", "/dev", "/run", "/snap"
    };

    private readonly ScanOptions _options;
    private long _directoriesScanned;
    private long _lastProgressTicks;
    private readonly ParallelOptions _parallelOptions;

    public long DirectoriesScanned => Interlocked.Read(ref _directoriesScanned);

    public event Action<long, string>? Progress;

    public Scanner(ScanOptions? options = null)
    {
        _options = options ?? new ScanOptions();
        _parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount * 2, 32)
        };
    }

    public Task<TreeNode> ScanAsync(string rootPath, CancellationToken ct = default)
    {
        rootPath = Path.GetFullPath(rootPath);
        _parallelOptions.CancellationToken = ct;

        var root = new TreeNode
        {
            Name = rootPath,
            FullPath = rootPath,
            IsDirectory = true
        };

        ScanDirectoryRecursive(root, 0, ct);

        // Post-processing: compute totals and sort
        root.ComputeTotals();
        root.SortBySize();

        return Task.FromResult(root);
    }

    private void ScanDirectoryRecursive(TreeNode node, int depth, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        DirectoryInfo dirInfo;
        try
        {
            dirInfo = new DirectoryInfo(node.FullPath);
            if (!dirInfo.Exists)
            {
                node.AccessDenied = true;
                return;
            }
        }
        catch
        {
            node.AccessDenied = true;
            return;
        }

        // Collect subdirectory TreeNodes paired with their DirectoryInfo for recursion
        List<(TreeNode Node, DirectoryInfo Dir)> subdirs = [];

        try
        {
            foreach (var entry in dirInfo.EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = 0,
                ReturnSpecialDirectories = false
            }))
            {
                if (ct.IsCancellationRequested) return;

                try
                {
                    if (entry is DirectoryInfo subDir)
                    {
                        // Check for reparse points (junctions, symlinks)
                        bool isSymlink = (subDir.Attributes & FileAttributes.ReparsePoint) != 0;

                        // Skip Linux virtual filesystems that report fake sizes
                        bool isVirtualFs = VirtualFsPaths.Contains(subDir.FullName);

                        // Check exclusion list
                        bool excluded = isVirtualFs ||
                            (_options.ExcludeNames != null &&
                            _options.ExcludeNames.Contains(subDir.Name));

                        var child = new TreeNode
                        {
                            Name = subDir.Name,
                            FullPath = subDir.FullName,
                            IsDirectory = true,
                            IsSymlink = isSymlink
                        };

                        lock (node.Children) { node.Children.Add(child); }

                        if (!isSymlink && !excluded && depth + 1 < _options.MaxDepth)
                        {
                            subdirs.Add((child, subDir));
                        }
                    }
                    else if (entry is FileInfo file)
                    {
                        long size;
                        try { size = file.Length; } catch { continue; }

                        Interlocked.Add(ref node.OwnSizeBacking, size);
                        Interlocked.Increment(ref node.OwnFileCountBacking);

                        var ext = file.Extension.ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext)) ext = "(no extension)";

                        lock (node.FilesByExtension)
                        {
                            if (node.FilesByExtension.TryGetValue(ext, out var existing))
                                node.FilesByExtension[ext] = existing + size;
                            else
                                node.FilesByExtension[ext] = size;
                        }

                        // Track large individual files as children for drill-down
                        if (size >= LargeFileThreshold)
                        {
                            var fileNode = new TreeNode
                            {
                                Name = file.Name,
                                FullPath = file.FullName,
                                IsDirectory = false,
                                OwnSize = size,
                                TotalSize = size
                            };
                            lock (node.Children) { node.Children.Add(fileNode); }
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
        catch (UnauthorizedAccessException)
        {
            node.AccessDenied = true;
            return;
        }
        catch (IOException)
        {
            node.AccessDenied = true;
            return;
        }

        var count = Interlocked.Increment(ref _directoriesScanned);
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastProgressTicks);
        if (now - last > 100 && Interlocked.CompareExchange(ref _lastProgressTicks, now, last) == last)
        {
            Progress?.Invoke(count, node.FullPath);
        }

        // Recurse into subdirectories in parallel
        if (subdirs.Count > 0)
        {
            Parallel.ForEach(subdirs, _parallelOptions, item =>
            {
                ScanDirectoryRecursive(item.Node, depth + 1, ct);
            });
        }
    }

    public static IEnumerable<string> GetFixedDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName);
    }
}
