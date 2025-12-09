using FluentAssertions;

namespace WhatsAppArchiver.Infrastructure.Tests;

public sealed class PathUtilitiesTests
{
    [Fact(DisplayName = "ExpandTildePath with tilde only returns home directory")]
    public void ExpandTildePath_TildeOnly_ReturnsHomeDirectory()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var result = PathUtilities.ExpandTildePath("~");

        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ExpandTildePath with tilde and forward slash path returns expanded path")]
    public void ExpandTildePath_TildeWithForwardSlashPath_ReturnsExpandedPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Use Path.Combine to ensure platform-appropriate separators
        var expected = Path.Combine(homeDir, "docs", "lappers", "file.json");

        var result = PathUtilities.ExpandTildePath("~/docs/lappers/file.json");

        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ExpandTildePath with tilde and backslash path returns expanded path")]
    public void ExpandTildePath_TildeWithBackslashPath_ReturnsExpandedPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // When input has backslashes, Path.Combine preserves them in the substring
        // So ~\docs\lappers\file.json becomes {homeDir}/docs\lappers\file.json on Unix
        var expected = Path.Combine(homeDir, "docs\\lappers\\file.json");

        var result = PathUtilities.ExpandTildePath("~\\docs\\lappers\\file.json");

        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ExpandTildePath with null returns null")]
    public void ExpandTildePath_Null_ReturnsNull()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - intentionally testing null handling
        var result = PathUtilities.ExpandTildePath(null);
#pragma warning restore CS8625

        result.Should().BeNull();
    }

    [Fact(DisplayName = "ExpandTildePath with empty string returns empty string")]
    public void ExpandTildePath_EmptyString_ReturnsEmptyString()
    {
        var result = PathUtilities.ExpandTildePath("");

        result.Should().Be("");
    }

    [Fact(DisplayName = "ExpandTildePath with whitespace returns whitespace")]
    public void ExpandTildePath_Whitespace_ReturnsWhitespace()
    {
        var input = "   ";

        var result = PathUtilities.ExpandTildePath(input);

        result.Should().Be(input);
    }

    [Fact(DisplayName = "ExpandTildePath with absolute Unix path returns unchanged")]
    public void ExpandTildePath_AbsoluteUnixPath_ReturnsUnchanged()
    {
        var path = "/absolute/path/to/file.txt";

        var result = PathUtilities.ExpandTildePath(path);

        result.Should().Be(path);
    }

    [Fact(DisplayName = "ExpandTildePath with absolute Windows path returns unchanged")]
    public void ExpandTildePath_AbsoluteWindowsPath_ReturnsUnchanged()
    {
        var path = "C:\\absolute\\path\\to\\file.txt";

        var result = PathUtilities.ExpandTildePath(path);

        result.Should().Be(path);
    }

    [Fact(DisplayName = "ExpandTildePath with relative path returns unchanged")]
    public void ExpandTildePath_RelativePath_ReturnsUnchanged()
    {
        var path = "relative/path/to/file.txt";

        var result = PathUtilities.ExpandTildePath(path);

        result.Should().Be(path);
    }

    [Fact(DisplayName = "ExpandTildePath with tilde in middle returns unchanged")]
    public void ExpandTildePath_TildeInMiddle_ReturnsUnchanged()
    {
        var path = "/path/with~tilde/file.txt";

        var result = PathUtilities.ExpandTildePath(path);

        result.Should().Be(path);
    }

    [Fact(DisplayName = "ExpandTildePath with single character file after tilde returns expanded path")]
    public void ExpandTildePath_SingleCharacterFileAfterTilde_ReturnsExpandedPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(homeDir, "a");

        var result = PathUtilities.ExpandTildePath("~/a");

        result.Should().Be(expected);
    }
}
