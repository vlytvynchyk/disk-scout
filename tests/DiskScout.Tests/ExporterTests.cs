using System.Text.Json;

namespace DiskScout.Tests;

public class ExporterTests
{
    private static (TreeNode Root, AnalysisResult Analysis) CreateTestData()
    {
        var root = new TreeNode
        {
            Name = "C:\\test",
            FullPath = "C:\\test",
            IsDirectory = true,
            OwnSize = 1000,
            Children =
            [
                new TreeNode
                {
                    Name = "docs",
                    FullPath = "C:\\test\\docs",
                    IsDirectory = true,
                    OwnSize = 500_000_000
                },
                new TreeNode
                {
                    Name = "big.zip",
                    FullPath = "C:\\test\\big.zip",
                    IsDirectory = false,
                    OwnSize = 200_000_000,
                    TotalSize = 200_000_000
                }
            ]
        };
        root.ComputeTotals();

        var analysis = new AnalysisResult(
            SpaceWasters: [new SpaceWaster("node_modules", "C:\\test\\node_modules", 300_000_000, "Node.js deps")],
            LargestFiles: [new LargestFile("C:\\test\\big.zip", 200_000_000)],
            FileTypeDistribution: new Dictionary<string, long> { [".zip"] = 200_000_000, [".txt"] = 1000 },
            Suggestions: [new CleanupSuggestion("node_modules", "Delete them", 300_000_000, "npx npkill")]
        );

        return (root, analysis);
    }

    [Fact]
    public void ExportJson_ProducesValidJson()
    {
        var (root, analysis) = CreateTestData();
        using var writer = new StringWriter();

        Exporter.Export(ExportFormat.Json, writer, root, analysis);
        var json = writer.ToString();

        Assert.False(string.IsNullOrWhiteSpace(json));

        // Should be valid JSON
        var doc = JsonDocument.Parse(json);
        var rootEl = doc.RootElement;

        Assert.Equal("C:\\test", rootEl.GetProperty("path").GetString());
        Assert.True(rootEl.GetProperty("totalSize").GetInt64() > 0);
        Assert.True(rootEl.GetProperty("topDirectories").GetArrayLength() > 0);
        Assert.True(rootEl.GetProperty("largestFiles").GetArrayLength() > 0);
        Assert.True(rootEl.GetProperty("fileTypes").GetArrayLength() > 0);
        Assert.True(rootEl.GetProperty("spaceWasters").GetArrayLength() > 0);
        Assert.True(rootEl.GetProperty("suggestions").GetArrayLength() > 0);
    }

    [Fact]
    public void ExportJson_IncludesAllFields()
    {
        var (root, analysis) = CreateTestData();
        using var writer = new StringWriter();

        Exporter.Export(ExportFormat.Json, writer, root, analysis);
        var doc = JsonDocument.Parse(writer.ToString());

        var suggestion = doc.RootElement.GetProperty("suggestions")[0];
        Assert.Equal("node_modules", suggestion.GetProperty("category").GetString());
        Assert.Equal("Delete them", suggestion.GetProperty("action").GetString());
        Assert.Equal(300_000_000, suggestion.GetProperty("potentialSavings").GetInt64());
        Assert.Equal("npx npkill", suggestion.GetProperty("command").GetString());
    }

    [Fact]
    public void ExportCsv_ProducesValidCsv()
    {
        var (root, analysis) = CreateTestData();
        using var writer = new StringWriter();

        Exporter.Export(ExportFormat.Csv, writer, root, analysis);
        var csv = writer.ToString();

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Type,Path,Size,Category", lines[0].TrimEnd('\r'));
        Assert.True(lines.Length > 1);
    }

    [Fact]
    public void ExportCsv_ContainsDirectoriesAndFiles()
    {
        var (root, analysis) = CreateTestData();
        using var writer = new StringWriter();

        Exporter.Export(ExportFormat.Csv, writer, root, analysis);
        var csv = writer.ToString();

        Assert.Contains("directory,", csv);
        Assert.Contains("file,", csv);
        Assert.Contains("waster,", csv);
    }

    [Fact]
    public void ExportCsv_EscapesCommasInPaths()
    {
        var root = new TreeNode
        {
            Name = "root",
            FullPath = "C:\\test,dir",
            IsDirectory = true,
            Children =
            [
                new TreeNode
                {
                    Name = "sub,folder",
                    FullPath = "C:\\test,dir\\sub,folder",
                    IsDirectory = true,
                    OwnSize = 100
                }
            ]
        };
        root.ComputeTotals();

        var analysis = new AnalysisResult([], [], new Dictionary<string, long>(), []);
        using var writer = new StringWriter();

        Exporter.Export(ExportFormat.Csv, writer, root, analysis);
        var csv = writer.ToString();

        // Path with comma should be quoted
        Assert.Contains("\"C:\\test,dir\\sub,folder\"", csv);
    }

    [Fact]
    public void ExportNone_WritesNothing()
    {
        var (root, analysis) = CreateTestData();
        using var writer = new StringWriter();

        Exporter.Export(ExportFormat.None, writer, root, analysis);

        Assert.Equal("", writer.ToString());
    }
}
