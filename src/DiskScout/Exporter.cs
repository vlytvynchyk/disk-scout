using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiskScout;

public enum ExportFormat { None, Json, Csv }

public static class Exporter
{
    public static void Export(ExportFormat format, TextWriter writer, TreeNode root, AnalysisResult analysis)
    {
        switch (format)
        {
            case ExportFormat.Json:
                ExportJson(writer, root, analysis);
                break;
            case ExportFormat.Csv:
                ExportCsv(writer, root, analysis);
                break;
        }
    }

    private static void ExportJson(TextWriter writer, TreeNode root, AnalysisResult analysis)
    {
        var report = new JsonReport
        {
            Path = root.FullPath,
            TotalSize = root.TotalSize,
            FileCount = root.FileCount,
            DirectoryCount = root.DirectoryCount,
            TopDirectories = root.Children
                .Where(c => c.IsDirectory)
                .Take(50)
                .Select(c => new JsonEntry { Path = c.FullPath, Size = c.TotalSize })
                .ToList(),
            LargestFiles = analysis.LargestFiles
                .Select(f => new JsonEntry { Path = f.Path, Size = f.Size })
                .ToList(),
            FileTypes = analysis.FileTypeDistribution
                .Take(30)
                .Select(kv => new JsonFileType { Extension = kv.Key, Size = kv.Value })
                .ToList(),
            SpaceWasters = analysis.SpaceWasters
                .Select(w => new JsonSpaceWaster
                {
                    Category = w.Category,
                    Path = w.Path,
                    Size = w.Size
                })
                .ToList(),
            Suggestions = analysis.Suggestions
                .Select(s => new JsonSuggestion
                {
                    Category = s.Category,
                    Action = s.Action,
                    PotentialSavings = s.PotentialSavings,
                    Command = s.Command
                })
                .ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        writer.Write(JsonSerializer.Serialize(report, options));
    }

    private static void ExportCsv(TextWriter writer, TreeNode root, AnalysisResult analysis)
    {
        writer.WriteLine("Type,Path,Size,Category");

        foreach (var child in root.Children.Where(c => c.IsDirectory).Take(100))
        {
            writer.WriteLine($"directory,{CsvEscape(child.FullPath)},{child.TotalSize},");
        }

        foreach (var file in analysis.LargestFiles)
        {
            writer.WriteLine($"file,{CsvEscape(file.Path)},{file.Size},");
        }

        foreach (var waster in analysis.SpaceWasters)
        {
            writer.WriteLine($"waster,{CsvEscape(waster.Path)},{waster.Size},{CsvEscape(waster.Category)}");
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

// JSON DTOs
file record JsonReport
{
    public string Path { get; init; } = "";
    public long TotalSize { get; init; }
    public int FileCount { get; init; }
    public int DirectoryCount { get; init; }
    public List<JsonEntry> TopDirectories { get; init; } = [];
    public List<JsonEntry> LargestFiles { get; init; } = [];
    public List<JsonFileType> FileTypes { get; init; } = [];
    public List<JsonSpaceWaster> SpaceWasters { get; init; } = [];
    public List<JsonSuggestion> Suggestions { get; init; } = [];
}

file record JsonEntry
{
    public string Path { get; init; } = "";
    public long Size { get; init; }
}

file record JsonFileType
{
    public string Extension { get; init; } = "";
    public long Size { get; init; }
}

file record JsonSpaceWaster
{
    public string Category { get; init; } = "";
    public string Path { get; init; } = "";
    public long Size { get; init; }
}

file record JsonSuggestion
{
    public string Category { get; init; } = "";
    public string Action { get; init; } = "";
    public long PotentialSavings { get; init; }
    public string Command { get; init; } = "";
}
