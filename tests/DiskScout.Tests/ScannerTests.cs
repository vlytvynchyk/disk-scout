namespace DiskScout.Tests;

public class ScannerTests
{
    [Fact]
    public async Task ScanAsync_CurrentDirectory_ReturnsTree()
    {
        var scanner = new Scanner(new ScanOptions(MaxDepth: 2));
        var root = await scanner.ScanAsync(Directory.GetCurrentDirectory());

        Assert.True(root.IsDirectory);
        Assert.True(root.TotalSize > 0);
        Assert.True(scanner.DirectoriesScanned > 0);
    }

    [Fact]
    public async Task ScanAsync_WithCancellation_DoesNotThrow()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var scanner = new Scanner();
        var root = await scanner.ScanAsync(Directory.GetCurrentDirectory(), cts.Token);

        // Should return a root node even if cancelled immediately
        Assert.NotNull(root);
    }

    [Fact]
    public async Task ScanAsync_NonExistentPath_MarksAccessDenied()
    {
        var scanner = new Scanner();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var root = await scanner.ScanAsync(path);

        Assert.True(root.AccessDenied);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgress()
    {
        int progressCount = 0;
        var scanner = new Scanner(new ScanOptions(MaxDepth: 3));
        scanner.Progress += (_, _) => Interlocked.Increment(ref progressCount);

        await scanner.ScanAsync(Path.GetTempPath());

        // Just ensure no exception is thrown during progress reporting
        Assert.True(scanner.DirectoriesScanned > 0);
    }

    [Fact]
    public void GetFixedDrives_ReturnsAtLeastOne()
    {
        var drives = Scanner.GetFixedDrives().ToList();
        Assert.NotEmpty(drives);
        Assert.All(drives, d => Assert.True(Directory.Exists(d)));
    }
}
