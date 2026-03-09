namespace DiskScout.Tests;

public class SpaceAnalyzerTests
{
    [Fact]
    public void Analyze_DetectsNodeModules()
    {
        var root = new TreeNode
        {
            Name = "project",
            FullPath = "/project",
            IsDirectory = true,
            Children =
            [
                new TreeNode
                {
                    Name = "node_modules",
                    FullPath = "/project/node_modules",
                    IsDirectory = true,
                    TotalSize = 500_000_000
                }
            ]
        };
        root.ComputeTotals();

        var analyzer = new SpaceAnalyzer();
        var result = analyzer.Analyze(root);

        Assert.Contains(result.SpaceWasters, w => w.Category == "node_modules");
    }

    [Fact]
    public void Analyze_DetectsPythonVenv()
    {
        var root = new TreeNode
        {
            Name = "project",
            FullPath = "/project",
            IsDirectory = true,
            Children =
            [
                new TreeNode
                {
                    Name = ".venv",
                    FullPath = "/project/.venv",
                    IsDirectory = true,
                    TotalSize = 200_000_000
                }
            ]
        };
        root.ComputeTotals();

        var analyzer = new SpaceAnalyzer();
        var result = analyzer.Analyze(root);

        Assert.Contains(result.SpaceWasters, w => w.Category == "Python Virtual Envs");
    }

    [Fact]
    public void Analyze_TracksLargestFiles()
    {
        var root = new TreeNode
        {
            Name = "root",
            FullPath = "/root",
            IsDirectory = true,
            Children =
            [
                new TreeNode { Name = "big.zip", FullPath = "/root/big.zip", TotalSize = 1_000_000_000 },
                new TreeNode { Name = "small.txt", FullPath = "/root/small.txt", TotalSize = 100 },
                new TreeNode { Name = "medium.iso", FullPath = "/root/medium.iso", TotalSize = 500_000_000 }
            ]
        };
        root.ComputeTotals();

        var analyzer = new SpaceAnalyzer(topFilesCount: 2);
        var result = analyzer.Analyze(root);

        Assert.Equal(2, result.LargestFiles.Count);
        Assert.Equal("big.zip", Path.GetFileName(result.LargestFiles[0].Path));
        Assert.Equal("medium.iso", Path.GetFileName(result.LargestFiles[1].Path));
    }

    [Fact]
    public void Analyze_FileTypeDistribution_FromRoot()
    {
        var root = new TreeNode
        {
            Name = "root",
            FullPath = "/root",
            IsDirectory = true,
            FilesByExtension = new(StringComparer.OrdinalIgnoreCase)
            {
                [".dll"] = 5_000_000_000,
                [".txt"] = 1_000_000
            }
        };

        var analyzer = new SpaceAnalyzer();
        var result = analyzer.Analyze(root);

        Assert.True(result.FileTypeDistribution.Count >= 2);
        Assert.True(result.FileTypeDistribution.First().Value >= result.FileTypeDistribution.Last().Value);
    }

    [Fact]
    public void Analyze_GeneratesSuggestions_AboveThreshold()
    {
        var nodeModules = new TreeNode
        {
            Name = "node_modules",
            FullPath = "/project/node_modules",
            IsDirectory = true,
            OwnSize = 500_000_000
        };
        var root = new TreeNode
        {
            Name = "project",
            FullPath = "/project",
            IsDirectory = true,
            Children = [nodeModules]
        };
        root.ComputeTotals();

        var analyzer = new SpaceAnalyzer();
        var result = analyzer.Analyze(root);

        Assert.Contains(result.Suggestions, s => s.Category == "node_modules");
        Assert.All(result.Suggestions, s => Assert.True(s.PotentialSavings >= 50 * 1024 * 1024));
    }

    [Fact]
    public void Analyze_DetectsWsl()
    {
        // Create a real temp directory with a .vhdx file so HasVhdxFile finds it
        var tempDir = Path.Combine(Path.GetTempPath(), "diskscout-wsl-test-" + Guid.NewGuid().ToString("N")[..8]);
        var packagesDir = Path.Combine(tempDir, "Packages", "Ubuntu", "LocalState");
        Directory.CreateDirectory(packagesDir);
        File.WriteAllBytes(Path.Combine(packagesDir, "ext4.vhdx"), new byte[1024]);

        try
        {
            var localState = new TreeNode
            {
                Name = "LocalState",
                FullPath = packagesDir,
                IsDirectory = true,
                OwnSize = 120_000_000_000 // 120 GB
            };
            var root = new TreeNode
            {
                Name = "Packages",
                FullPath = Path.Combine(tempDir, "Packages"),
                IsDirectory = true,
                Children =
                [
                    new TreeNode
                    {
                        Name = "Ubuntu",
                        FullPath = Path.Combine(tempDir, "Packages", "Ubuntu"),
                        IsDirectory = true,
                        Children = [localState]
                    }
                ]
            };
            root.ComputeTotals();

            var analyzer = new SpaceAnalyzer();
            var result = analyzer.Analyze(root);

            Assert.Contains(result.SpaceWasters, w => w.Category == "WSL");
            Assert.Contains(result.Suggestions, s => s.Category == "WSL" && s.Command.Contains("compact vdisk"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Analyze_CustomRules_OnlyAppliesProvided()
    {
        var root = new TreeNode
        {
            Name = "project",
            FullPath = "/project",
            IsDirectory = true,
            Children =
            [
                new TreeNode
                {
                    Name = "node_modules",
                    FullPath = "/project/node_modules",
                    IsDirectory = true,
                    OwnSize = 500_000_000
                },
                new TreeNode
                {
                    Name = ".venv",
                    FullPath = "/project/.venv",
                    IsDirectory = true,
                    OwnSize = 200_000_000
                }
            ]
        };
        root.ComputeTotals();

        // Only inject the venv rule — node_modules should be ignored
        var analyzer = new SpaceAnalyzer(rules: [new Rules.PythonVenvRule()]);
        var result = analyzer.Analyze(root);

        Assert.Single(result.SpaceWasters);
        Assert.Equal("Python Virtual Envs", result.SpaceWasters[0].Category);
    }

    [Fact]
    public void Analyze_SkipsAccessDeniedNodes()
    {
        var root = new TreeNode
        {
            Name = "root",
            FullPath = "/root",
            IsDirectory = true,
            Children =
            [
                new TreeNode
                {
                    Name = "restricted",
                    FullPath = "/root/restricted",
                    IsDirectory = true,
                    AccessDenied = true,
                    TotalSize = 0
                }
            ]
        };
        root.ComputeTotals();

        var analyzer = new SpaceAnalyzer();
        var result = analyzer.Analyze(root);

        Assert.Empty(result.SpaceWasters);
    }
}
