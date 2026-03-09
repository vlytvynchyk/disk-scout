namespace DiskScout.Tests;

public class TreeNodeTests
{
    [Fact]
    public void ComputeTotals_SingleDirectory_SumsOwnSize()
    {
        var node = new TreeNode
        {
            Name = "root",
            FullPath = "/root",
            IsDirectory = true,
            OwnSize = 1000
        };

        node.ComputeTotals();

        Assert.Equal(1000, node.TotalSize);
        Assert.Equal(0, node.FileCount);
        Assert.Equal(0, node.DirectoryCount);
    }

    [Fact]
    public void ComputeTotals_WithChildren_SumsRecursively()
    {
        var root = new TreeNode
        {
            Name = "root",
            FullPath = "/root",
            IsDirectory = true,
            OwnSize = 100,
            OwnFileCount = 1 // 1 regular file contributing to OwnSize
        };

        var child1 = new TreeNode
        {
            Name = "child1",
            FullPath = "/root/child1",
            IsDirectory = true,
            OwnSize = 200,
            OwnFileCount = 2
        };

        root.Children.Add(child1);

        root.ComputeTotals();

        Assert.Equal(300, root.TotalSize);
        Assert.Equal(3, root.FileCount); // 1 own + 2 from child
        Assert.Equal(1, root.DirectoryCount);
    }

    [Fact]
    public void ComputeTotals_SkipsSymlinks()
    {
        var root = new TreeNode
        {
            Name = "root",
            FullPath = "/root",
            IsDirectory = true,
            OwnSize = 100
        };

        var symlink = new TreeNode
        {
            Name = "link",
            FullPath = "/root/link",
            IsDirectory = true,
            IsSymlink = true,
            TotalSize = 99999
        };

        root.Children.Add(symlink);
        root.ComputeTotals();

        Assert.Equal(100, root.TotalSize);
    }

    [Fact]
    public void ComputeTotals_MergesFileExtensions()
    {
        var root = new TreeNode
        {
            Name = "root",
            FullPath = "/root",
            IsDirectory = true,
            FilesByExtension = new(StringComparer.OrdinalIgnoreCase) { [".txt"] = 100 }
        };

        var child = new TreeNode
        {
            Name = "sub",
            FullPath = "/root/sub",
            IsDirectory = true,
            FilesByExtension = new(StringComparer.OrdinalIgnoreCase) { [".txt"] = 200, [".log"] = 50 }
        };

        root.Children.Add(child);
        root.ComputeTotals();

        Assert.Equal(300, root.FilesByExtension[".txt"]);
        Assert.Equal(50, root.FilesByExtension[".log"]);
    }

    [Fact]
    public void SortBySize_OrdersDescending()
    {
        var root = new TreeNode { Name = "root", FullPath = "/root", IsDirectory = true };

        root.Children.Add(new TreeNode { Name = "small", TotalSize = 10, IsDirectory = true });
        root.Children.Add(new TreeNode { Name = "large", TotalSize = 1000, IsDirectory = true });
        root.Children.Add(new TreeNode { Name = "medium", TotalSize = 100, IsDirectory = true });

        root.SortBySize();

        Assert.Equal("large", root.Children[0].Name);
        Assert.Equal("medium", root.Children[1].Name);
        Assert.Equal("small", root.Children[2].Name);
    }

    [Fact]
    public void Prune_RemovesSmallChildren_CreatesSyntheticNode()
    {
        var root = new TreeNode { Name = "root", FullPath = "/root", IsDirectory = true };

        root.Children.Add(new TreeNode { Name = "big", TotalSize = 1000, IsDirectory = true });
        root.Children.Add(new TreeNode { Name = "tiny1", TotalSize = 5, IsDirectory = true });
        root.Children.Add(new TreeNode { Name = "tiny2", TotalSize = 3, IsDirectory = true });

        root.Prune(10);

        Assert.Equal(2, root.Children.Count);
        Assert.Equal("big", root.Children[0].Name);
        Assert.Contains("[2 small items]", root.Children[1].Name);
        Assert.Equal(8, root.Children[1].TotalSize);
    }

    [Fact]
    public void ComputeTotals_DeepNesting()
    {
        var root = new TreeNode { Name = "root", FullPath = "/root", IsDirectory = true, OwnSize = 10, OwnFileCount = 1 };
        var level1 = new TreeNode { Name = "l1", FullPath = "/root/l1", IsDirectory = true, OwnSize = 20, OwnFileCount = 1 };
        var level2 = new TreeNode { Name = "l2", FullPath = "/root/l1/l2", IsDirectory = true, OwnSize = 30, OwnFileCount = 1 };

        root.Children.Add(level1);
        level1.Children.Add(level2);

        root.ComputeTotals();

        Assert.Equal(30, level2.TotalSize);
        Assert.Equal(50, level1.TotalSize);
        Assert.Equal(60, root.TotalSize);
        Assert.Equal(2, root.DirectoryCount);
        Assert.Equal(3, root.FileCount); // 1 + 1 + 1
    }
}
