namespace DiskScout.Tests;

public class RendererTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.00 GB")]
    [InlineData(1099511627776, "1.00 TB")]
    [InlineData(-1, "???")]
    public void FormatSize_FormatsCorrectly(long bytes, string expected)
    {
        Assert.Equal(expected, Renderer.FormatSize(bytes));
    }

    [Fact]
    public void FormatSize_LargeGigabytes()
    {
        long size = (long)(5.5 * 1024 * 1024 * 1024);
        Assert.Equal("5.50 GB", Renderer.FormatSize(size));
    }

    private static string CaptureConsole(Action action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static TreeNode CreateTestTree()
    {
        var root = new TreeNode
        {
            Name = "C:\\test",
            FullPath = "C:\\test",
            IsDirectory = true,
            OwnSize = 1000,
            OwnFileCount = 2,
            Children =
            [
                new TreeNode
                {
                    Name = "large-dir",
                    FullPath = "C:\\test\\large-dir",
                    IsDirectory = true,
                    OwnSize = 500_000_000,
                    OwnFileCount = 10
                },
                new TreeNode
                {
                    Name = "small-dir",
                    FullPath = "C:\\test\\small-dir",
                    IsDirectory = true,
                    OwnSize = 5_000_000,
                    OwnFileCount = 5
                }
            ]
        };
        root.ComputeTotals();
        root.SortBySize();
        return root;
    }

    [Fact]
    public void RenderTree_ShowsDirectoryNames()
    {
        var root = CreateTestTree();
        var renderer = new Renderer(new RenderOptions(NoColor: true));

        var output = CaptureConsole(() => renderer.RenderTree(root));

        Assert.Contains("large-dir/", output);
        Assert.Contains("small-dir/", output);
    }

    [Fact]
    public void RenderTree_ShowsSizes()
    {
        var root = CreateTestTree();
        var renderer = new Renderer(new RenderOptions(NoColor: true));

        var output = CaptureConsole(() => renderer.RenderTree(root));

        Assert.Contains("MB", output);
    }

    [Fact]
    public void RenderTree_ShowsPercentages()
    {
        var root = CreateTestTree();
        var renderer = new Renderer(new RenderOptions(NoColor: true));

        var output = CaptureConsole(() => renderer.RenderTree(root));

        Assert.Contains("%", output);
    }

    [Fact]
    public void RenderTree_RespectsMinimumSize()
    {
        var root = CreateTestTree();
        // Set min size to 100MB - should hide small-dir
        var renderer = new Renderer(new RenderOptions(MinimumSize: 100_000_000, NoColor: true));

        var output = CaptureConsole(() => renderer.RenderTree(root));

        Assert.Contains("large-dir/", output);
        Assert.DoesNotContain("small-dir/", output);
    }

    [Fact]
    public void RenderHeader_ShowsTotalAndPath()
    {
        var root = CreateTestTree();
        var renderer = new Renderer(new RenderOptions(NoColor: true));

        var output = CaptureConsole(() => renderer.RenderHeader("C:\\test", root));

        Assert.Contains("C:\\test", output);
        Assert.Contains("Disk Usage Analysis", output);
        Assert.Contains("directories", output);
    }

    [Fact]
    public void RenderLargestFiles_ShowsFileInfo()
    {
        var files = new List<LargestFile>
        {
            new("C:\\test\\big.zip", 2L * 1024 * 1024 * 1024),
            new("C:\\test\\med.iso", 500_000_000)
        };

        var renderer = new Renderer(new RenderOptions(NoColor: true));
        var output = CaptureConsole(() => renderer.RenderLargestFiles(files));

        Assert.Contains("big.zip", output);
        Assert.Contains("med.iso", output);
        Assert.Contains("GB", output);
    }

    [Fact]
    public void RenderFileTypeDistribution_ShowsExtensions()
    {
        var dist = new Dictionary<string, long>
        {
            [".dll"] = 5_000_000_000,
            [".txt"] = 1_000_000
        };

        var renderer = new Renderer(new RenderOptions(NoColor: true));
        var output = CaptureConsole(() => renderer.RenderFileTypeDistribution(dist));

        Assert.Contains(".dll", output);
        Assert.Contains(".txt", output);
    }

    [Fact]
    public void RenderSpaceWasters_GroupsByCategory()
    {
        var wasters = new List<SpaceWaster>
        {
            new("node_modules", "C:\\a\\node_modules", 500_000_000, "deps"),
            new("node_modules", "C:\\b\\node_modules", 300_000_000, "deps"),
            new(".NET Build Output", "C:\\c\\bin", 200_000_000, "build")
        };

        var renderer = new Renderer(new RenderOptions(NoColor: true));
        var output = CaptureConsole(() => renderer.RenderSpaceWasters(wasters));

        Assert.Contains("node_modules", output);
        Assert.Contains(".NET Build Output", output);
        Assert.Contains("2 locations", output);
    }

    [Fact]
    public void RenderSuggestions_ShowsCommands()
    {
        var suggestions = new List<CleanupSuggestion>
        {
            new("node_modules", "Delete them", 500_000_000, "npx npkill")
        };

        var renderer = new Renderer(new RenderOptions(NoColor: true));
        var output = CaptureConsole(() => renderer.RenderSuggestions(suggestions));

        Assert.Contains("Delete them", output);
        Assert.Contains("npx npkill", output);
        Assert.Contains("Potential savings", output);
    }

    [Fact]
    public void RenderTree_EmptyChildren_ShowsMessage()
    {
        var root = new TreeNode
        {
            Name = "empty",
            FullPath = "C:\\empty",
            IsDirectory = true
        };
        root.ComputeTotals();

        var renderer = new Renderer(new RenderOptions(NoColor: true));
        var output = CaptureConsole(() => renderer.RenderTree(root));

        Assert.Contains("No items above minimum size threshold", output);
    }
}
