using System.CommandLine;
using DiskScout;

// Enable ANSI escape codes on Windows
EnableVirtualTerminal();

var pathOption = new Option<string?>(
    ["--path", "-p"],
    "Path to scan (default: current drive)");

var depthOption = new Option<int>(
    ["--depth", "-d"],
    () => 50,
    "Maximum scan depth");

var minSizeOption = new Option<string>(
    ["--min-size", "-m"],
    () => "1MB",
    "Minimum size to display (e.g., 500KB, 10MB, 1GB)");

var topFilesOption = new Option<int>(
    ["--top-files", "-t"],
    () => 20,
    "Number of largest files to show");

var suggestOption = new Option<bool>(
    ["--suggest", "-s"],
    () => true,
    "Show cleanup suggestions");

var noInteractiveOption = new Option<bool>(
    "--no-interactive",
    "Disable interactive drill-down mode");

var noColorOption = new Option<bool>(
    "--no-color",
    "Disable color output");

var outputOption = new Option<string?>(
    ["--output", "-o"],
    "Export results to file (use .json or .csv extension)");

var excludeOption = new Option<string?>(
    ["--exclude", "-e"],
    "Comma-separated directory names to skip (e.g., node_modules,.git)");

var rootCommand = new RootCommand("Disk Usage Analyzer - Find what's eating your disk space")
{
    pathOption, depthOption, minSizeOption, topFilesOption,
    suggestOption, noInteractiveOption, noColorOption, outputOption, excludeOption
};

rootCommand.SetHandler(async (context) =>
{
    var path = context.ParseResult.GetValueForOption(pathOption);
    var depth = context.ParseResult.GetValueForOption(depthOption);
    var minSizeStr = context.ParseResult.GetValueForOption(minSizeOption) ?? "1MB";
    var topFiles = context.ParseResult.GetValueForOption(topFilesOption);
    var suggest = context.ParseResult.GetValueForOption(suggestOption);
    var noInteractive = context.ParseResult.GetValueForOption(noInteractiveOption);
    var noColor = context.ParseResult.GetValueForOption(noColorOption);
    var outputFile = context.ParseResult.GetValueForOption(outputOption);
    var excludeStr = context.ParseResult.GetValueForOption(excludeOption);

    var exportFormat = ExportFormat.None;
    if (!string.IsNullOrEmpty(outputFile))
    {
        exportFormat = Path.GetExtension(outputFile).ToLowerInvariant() switch
        {
            ".json" => ExportFormat.Json,
            ".csv" => ExportFormat.Csv,
            _ => ExportFormat.Json
        };
    }

    var minSize = SizeParser.Parse(minSizeStr);
    var ct = context.GetCancellationToken();

    // Handle Ctrl+C
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("\n  Scan cancelled. Showing partial results...\n");
    };

    // Determine what to scan
    var scanPaths = new List<string>();
    if (!string.IsNullOrEmpty(path))
    {
        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Error: Path '{path}' does not exist.");
            context.ExitCode = 1;
            return;
        }
        scanPaths.Add(Path.GetFullPath(path));
    }
    else
    {
        // Default to current drive
        var currentDrive = Path.GetPathRoot(Environment.CurrentDirectory);
        if (currentDrive != null)
            scanPaths.Add(currentDrive);
        else
            scanPaths.AddRange(Scanner.GetFixedDrives());
    }

    var renderOptions = new RenderOptions(depth, minSize, noColor);
    var renderer = new Renderer(renderOptions);

    foreach (var scanPath in scanPaths)
    {
        Console.WriteLine();
        Console.WriteLine($"  Scanning {scanPath} ...");

        HashSet<string>? excludeNames = null;
        if (!string.IsNullOrEmpty(excludeStr))
        {
            excludeNames = new HashSet<string>(
                excludeStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        var scanner = new Scanner(new ScanOptions(depth, ExcludeNames: excludeNames));
        var lastUpdate = DateTime.UtcNow;

        scanner.Progress += (count, currentDir) =>
        {
            var now = DateTime.UtcNow;
            if ((now - lastUpdate).TotalMilliseconds > 100)
            {
                lastUpdate = now;
                renderer.RenderProgress(count, currentDir);
            }
        };

        TreeNode root;
        try
        {
            root = await scanner.ScanAsync(scanPath, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Scan was cancelled.");
            continue;
        }

        renderer.ClearProgress();
        Console.WriteLine($"  Scanned {scanner.DirectoriesScanned:N0} directories.");

        // Analysis
        var analyzer = new SpaceAnalyzer(topFiles);
        var result = analyzer.Analyze(root);

        // Render results
        renderer.RenderHeader(scanPath, root);
        renderer.RenderTree(root);

        if (result.FileTypeDistribution.Count > 0)
            renderer.RenderFileTypeDistribution(result.FileTypeDistribution);

        if (result.LargestFiles.Count > 0)
            renderer.RenderLargestFiles(result.LargestFiles);

        if (suggest)
        {
            renderer.RenderSpaceWasters(result.SpaceWasters);
            renderer.RenderSuggestions(result.Suggestions);
        }

        // Export if requested
        if (exportFormat != ExportFormat.None && !string.IsNullOrEmpty(outputFile))
        {
            using var writer = new StreamWriter(outputFile);
            Exporter.Export(exportFormat, writer, root, result);
            Console.WriteLine($"  Results exported to {outputFile}");
        }

        // Interactive drill-down
        if (!noInteractive && !cts.Token.IsCancellationRequested)
        {
            var navigationStack = new Stack<TreeNode>();
            var current = root;

            while (true)
            {
                int choice = renderer.PromptDrillDown(current);

                if (choice == -2) break; // quit

                if (choice == -1) // back
                {
                    if (navigationStack.Count > 0)
                    {
                        current = navigationStack.Pop();
                        Console.WriteLine();
                        renderer.RenderHeader(current.FullPath, current);
                        renderer.RenderTree(current);
                    }
                    else
                    {
                        break;
                    }
                    continue;
                }

                if (choice >= 0 && choice < current.Children.Count)
                {
                    var selected = current.Children[choice];
                    if (selected.IsDirectory)
                    {
                        navigationStack.Push(current);
                        current = selected;
                        Console.WriteLine();
                        renderer.RenderHeader(current.FullPath, current);
                        renderer.RenderTree(current);
                    }
                }
            }
        }
    }
});

return await rootCommand.InvokeAsync(args);


static void EnableVirtualTerminal()
{
    if (!OperatingSystem.IsWindows()) return;

    try
    {
        // .NET 8 on Windows Terminal / modern conhost supports ANSI by default.
        // Write a test escape sequence to verify.
        Console.Write("\x1b[0m");
    }
    catch
    {
        // Silently ignore - colors just won't work
    }
}
