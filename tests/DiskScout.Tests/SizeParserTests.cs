namespace DiskScout.Tests;

public class SizeParserTests
{
    [Theory]
    [InlineData("1KB", 1024)]
    [InlineData("1MB", 1048576)]
    [InlineData("1GB", 1073741824)]
    [InlineData("1TB", 1099511627776)]
    [InlineData("500KB", 512000)]
    [InlineData("1.5MB", 1572864)]
    [InlineData("10B", 10)]
    [InlineData("0MB", 0)]
    public void Parse_ValidSuffixes(string input, long expected)
    {
        Assert.Equal(expected, SizeParser.Parse(input));
    }

    [Theory]
    [InlineData("1kb")]
    [InlineData("1Kb")]
    [InlineData("1KB")]
    public void Parse_CaseInsensitive(string input)
    {
        Assert.Equal(1024, SizeParser.Parse(input));
    }

    [Theory]
    [InlineData(" 1MB ")]
    [InlineData("1 MB")]
    public void Parse_Whitespace(string input)
    {
        Assert.Equal(1048576, SizeParser.Parse(input));
    }

    [Fact]
    public void Parse_RawBytes()
    {
        Assert.Equal(12345, SizeParser.Parse("12345"));
    }

    [Fact]
    public void Parse_Invalid_ReturnsDefault()
    {
        Assert.Equal(1048576, SizeParser.Parse("abc"));
    }

    [Fact]
    public void Parse_Empty_ReturnsDefault()
    {
        Assert.Equal(1048576, SizeParser.Parse(""));
    }

    [Fact]
    public void Parse_CustomDefault()
    {
        Assert.Equal(999, SizeParser.Parse("invalid", 999));
    }
}
