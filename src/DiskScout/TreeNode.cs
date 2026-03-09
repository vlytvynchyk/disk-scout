namespace DiskScout;

public class TreeNode
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long OwnSizeBacking;
    public long OwnSize
    {
        get => Interlocked.Read(ref OwnSizeBacking);
        set => Interlocked.Exchange(ref OwnSizeBacking, value);
    }
    public long TotalSize { get; set; }
    public int OwnFileCountBacking;
    public int OwnFileCount
    {
        get => Interlocked.CompareExchange(ref OwnFileCountBacking, 0, 0);
        set => Interlocked.Exchange(ref OwnFileCountBacking, value);
    }
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public List<TreeNode> Children { get; set; } = [];
    public bool AccessDenied { get; set; }
    public bool IsSymlink { get; set; }
    public Dictionary<string, long> FilesByExtension { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void ComputeTotals()
    {
        foreach (var child in Children)
        {
            if (child.IsDirectory && !child.IsSymlink)
                child.ComputeTotals();
        }

        TotalSize = OwnSize;
        FileCount = OwnFileCount;
        DirectoryCount = 0;

        foreach (var child in Children)
        {
            if (child.IsSymlink) continue;

            TotalSize += child.TotalSize;
            if (child.IsDirectory)
            {
                DirectoryCount += 1 + child.DirectoryCount;
                FileCount += child.FileCount;
            }

            // Merge extension stats upward
            foreach (var (ext, size) in child.FilesByExtension)
            {
                if (FilesByExtension.TryGetValue(ext, out var existing))
                    FilesByExtension[ext] = existing + size;
                else
                    FilesByExtension[ext] = size;
            }
        }
    }

    public void SortBySize()
    {
        Children.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
        foreach (var child in Children)
        {
            if (child.IsDirectory)
                child.SortBySize();
        }
    }

    /// <summary>
    /// Prune children smaller than the threshold to save memory.
    /// Pruned size is kept in a synthetic "[other]" node.
    /// </summary>
    public void Prune(long minSize)
    {
        if (!IsDirectory) return;

        long prunedSize = 0;
        int prunedCount = 0;
        var kept = new List<TreeNode>();

        foreach (var child in Children)
        {
            if (child.TotalSize >= minSize)
            {
                child.Prune(minSize);
                kept.Add(child);
            }
            else
            {
                prunedSize += child.TotalSize;
                prunedCount++;
            }
        }

        if (prunedCount > 0)
        {
            kept.Add(new TreeNode
            {
                Name = $"[{prunedCount} small items]",
                FullPath = FullPath,
                IsDirectory = false,
                TotalSize = prunedSize,
                OwnSize = prunedSize
            });
        }

        Children = kept;
    }
}
