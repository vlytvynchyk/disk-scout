namespace DiskScout.Tests;

public class IntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "diskscout-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private void CreateFile(string relativePath, int sizeBytes)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[sizeBytes]);
    }

    [Fact]
    public async Task Scan_EmptyDirectory_ReturnsZeroSize()
    {
        var scanner = new Scanner();
        var root = await scanner.ScanAsync(_tempDir);

        Assert.Equal(0, root.TotalSize);
        Assert.Equal(0, root.FileCount);
    }

    [Fact]
    public async Task Scan_SingleFile_ReportsCorrectSize()
    {
        CreateFile("test.txt", 1024);

        var scanner = new Scanner();
        var root = await scanner.ScanAsync(_tempDir);

        Assert.Equal(1024, root.TotalSize);
        Assert.Equal(1, root.FileCount);
    }

    [Fact]
    public async Task Scan_NestedDirectories_SumsRecursively()
    {
        CreateFile("a/file1.txt", 100);
        CreateFile("a/b/file2.txt", 200);
        CreateFile("a/b/c/file3.txt", 300);

        var scanner = new Scanner();
        var root = await scanner.ScanAsync(_tempDir);

        Assert.Equal(600, root.TotalSize);
        Assert.Equal(3, root.FileCount);
        Assert.Equal(3, root.DirectoryCount); // a, a/b, a/b/c
    }

    [Fact]
    public async Task Scan_MaxDepth_LimitsRecursion()
    {
        CreateFile("a/b/c/deep.txt", 100);

        var scanner = new Scanner(new ScanOptions(MaxDepth: 2));
        var root = await scanner.ScanAsync(_tempDir);

        // With MaxDepth=2, we scan root (depth 0) and 'a' (depth 1)
        // 'b' is at depth 2 which is not < MaxDepth, so it's not scanned
        // The file deep.txt should not be found
        Assert.Equal(0, root.TotalSize);
    }

    [Fact]
    public async Task Scan_FileExtensions_Tracked()
    {
        CreateFile("doc.txt", 100);
        CreateFile("pic.jpg", 200);
        CreateFile("doc2.txt", 300);

        var scanner = new Scanner();
        var root = await scanner.ScanAsync(_tempDir);

        Assert.Equal(400, root.FilesByExtension[".txt"]);
        Assert.Equal(200, root.FilesByExtension[".jpg"]);
    }

    [Fact]
    public async Task Scan_SortsBySize_LargestFirst()
    {
        CreateFile("small/file.txt", 100);
        CreateFile("large/file.txt", 10000);
        CreateFile("medium/file.txt", 1000);

        var scanner = new Scanner();
        var root = await scanner.ScanAsync(_tempDir);

        var dirs = root.Children.Where(c => c.IsDirectory).ToList();
        Assert.Equal("large", dirs[0].Name);
        Assert.Equal("medium", dirs[1].Name);
        Assert.Equal("small", dirs[2].Name);
    }

    [Fact]
    public async Task Scan_FullPipeline_ScanAnalyzeExport()
    {
        CreateFile("src/app.js", 500);
        CreateFile("node_modules/pkg/index.js", 1000);

        var scanner = new Scanner();
        var root = await scanner.ScanAsync(_tempDir);

        var analyzer = new SpaceAnalyzer(topFilesCount: 5);
        var result = analyzer.Analyze(root);

        // node_modules should be detected as a space waster
        Assert.Contains(result.SpaceWasters, w => w.Category == "node_modules");

        // Export to JSON should work
        using var writer = new StringWriter();
        Exporter.Export(ExportFormat.Json, writer, root, result);
        var json = writer.ToString();
        Assert.Contains("node_modules", json);
    }

    [Fact]
    public async Task Scan_ExcludeDirectories_SkipsThem()
    {
        CreateFile("src/app.js", 500);
        CreateFile("node_modules/pkg/index.js", 1000);
        CreateFile(".git/objects/pack", 2000);

        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "node_modules", ".git" };
        var scanner = new Scanner(new ScanOptions(ExcludeNames: exclude));
        var root = await scanner.ScanAsync(_tempDir);

        // Only src/app.js should be counted
        Assert.Equal(500, root.TotalSize);
    }

    [Fact]
    public async Task Scan_NonExistentPath_MarksAccessDenied()
    {
        var scanner = new Scanner();
        var root = await scanner.ScanAsync(Path.Combine(_tempDir, "nonexistent"));

        Assert.True(root.AccessDenied);
        Assert.Equal(0, root.TotalSize);
    }

    [Fact]
    public async Task Scan_Cancellation_ReturnsPartialResults()
    {
        // Create many directories to ensure scan takes some time
        for (int i = 0; i < 50; i++)
            CreateFile($"dir{i}/file.txt", 100);

        using var cts = new CancellationTokenSource();
        var scanner = new Scanner();

        // Cancel immediately
        cts.Cancel();
        var root = await scanner.ScanAsync(_tempDir, cts.Token);

        // Should return without throwing, tree may be partial
        Assert.NotNull(root);
    }
}
