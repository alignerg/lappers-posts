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

    [Fact(DisplayName = "ResolveApplicationPath with null returns null")]
    public void ResolveApplicationPath_Null_ReturnsNull()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - intentionally testing null handling
        var result = PathUtilities.ResolveApplicationPath(null);
#pragma warning restore CS8625

        result.Should().BeNull();
    }

    [Fact(DisplayName = "ResolveApplicationPath with empty string returns empty string")]
    public void ResolveApplicationPath_EmptyString_ReturnsEmptyString()
    {
        var result = PathUtilities.ResolveApplicationPath("");

        result.Should().Be("");
    }

    [Fact(DisplayName = "ResolveApplicationPath with whitespace returns whitespace")]
    public void ResolveApplicationPath_Whitespace_ReturnsWhitespace()
    {
        var input = "   ";

        var result = PathUtilities.ResolveApplicationPath(input);

        result.Should().Be(input);
    }

    [Fact(DisplayName = "ResolveApplicationPath with relative path returns path relative to app base directory")]
    public void ResolveApplicationPath_RelativePath_ReturnsPathRelativeToAppBaseDirectory()
    {
        var relativePath = "./credentials/google-service-account.json";
        var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

        var result = PathUtilities.ResolveApplicationPath(relativePath);

        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ResolveApplicationPath with relative path without dot-slash returns path relative to app base directory")]
    public void ResolveApplicationPath_RelativePathWithoutDotSlash_ReturnsPathRelativeToAppBaseDirectory()
    {
        var relativePath = "credentials/google-service-account.json";
        var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

        var result = PathUtilities.ResolveApplicationPath(relativePath);

        result.Should().Be(expected);
    }

    [Fact(DisplayName = "ResolveApplicationPath with absolute Unix path returns unchanged")]
    public void ResolveApplicationPath_AbsoluteUnixPath_ReturnsUnchanged()
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows, Unix-style paths without drive letters are not considered rooted
            // and will be resolved relative to the app base directory.
            // This test only validates behavior on Unix-like platforms.
            return;
        }

        var path = "/absolute/path/to/file.txt";

        var result = PathUtilities.ResolveApplicationPath(path);

        result.Should().Be(path);
    }

    [Fact(DisplayName = "ResolveApplicationPath with absolute Windows path returns unchanged")]
    public void ResolveApplicationPath_AbsoluteWindowsPath_ReturnsUnchanged()
    {
        if (!OperatingSystem.IsWindows())
        {
            // On non-Windows platforms, Windows paths are not considered absolute
            // and will be resolved relative to the app base directory.
            // This test only validates behavior on Windows.
            return;
        }

        var path = "C:\\absolute\\path\\to\\file.txt";

        var result = PathUtilities.ResolveApplicationPath(path);

        result.Should().Be(path);
    }

    [Fact(DisplayName = "ResolveApplicationPath with rooted path returns unchanged")]
    public void ResolveApplicationPath_RootedPath_ReturnsUnchanged()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "test", "path", "file.txt"));

        var result = PathUtilities.ResolveApplicationPath(path);

        result.Should().Be(path);
    }
}
