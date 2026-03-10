namespace DiskScout.Rules;

public class PythonVenvRule : ISpaceWasterRule
{
    public string Category => "Python Virtual Envs";

    public bool Matches(TreeNode node) =>
        node.Name.Equals(".tox", StringComparison.OrdinalIgnoreCase) ||
        node.Name.Equals(".venv", StringComparison.OrdinalIgnoreCase) ||
        node.Name.Equals("venv", StringComparison.OrdinalIgnoreCase);

    public string GetDescription(TreeNode node) => "Python virtual environment";

    public CleanupSuggestion CreateSuggestion(long totalSize) => new(
        Category,
        "Remove unused virtual environments",
        totalSize,
        OperatingSystem.IsWindows()
            ? "Delete .venv/venv folders in unused projects"
            : "find . -type d \\( -name .venv -o -name venv -o -name .tox \\) -exec rm -rf {} +");
}
