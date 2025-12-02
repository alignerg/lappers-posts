using FluentAssertions;
using WhatsAppArchiver.Infrastructure.Parsers;

namespace WhatsAppArchiver.Infrastructure.Tests.Parsers;

public class WhatsAppTextFileParserTests
{
    private readonly WhatsAppTextFileParser _parser;
    private readonly string _sampleDataPath;

    public WhatsAppTextFileParserTests()
    {
        _parser = new WhatsAppTextFileParser();
        _sampleDataPath = GetSampleDataPath();
    }

    [Fact(DisplayName = "ParseAsync with DD/MM/YYYY format returns complete export")]
    public async Task ParseChatFileAsync_ValidFileWithDDMMYYYYFormat_ReturnsCompleteExport()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(10);
        result.Metadata.SourceFileName.Should().Be("sample-dd-mm-yyyy.txt");
        result.Metadata.TotalLines.Should().Be(10);
        result.Metadata.ParsedMessageCount.Should().Be(10);
        result.Metadata.FailedLineCount.Should().Be(0);

        // Verify first message
        var firstMessage = result.Messages.First();
        firstMessage.Sender.Should().Be("John Smith");
        firstMessage.Content.Should().Be("Good morning everyone! 🎄");
        firstMessage.Timestamp.Day.Should().Be(25);
        firstMessage.Timestamp.Month.Should().Be(12);
        firstMessage.Timestamp.Year.Should().Be(2024);
    }

    [Fact(DisplayName = "ParseAsync with M/D/YY format returns complete export")]
    public async Task ParseChatFileAsync_ValidFileWithMDYYFormat_ReturnsCompleteExport()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-m-d-yy.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(10);
        result.Metadata.SourceFileName.Should().Be("sample-m-d-yy.txt");
        result.Metadata.TotalLines.Should().Be(10);
        result.Metadata.ParsedMessageCount.Should().Be(10);
        result.Metadata.FailedLineCount.Should().Be(0);

        // Verify first message
        var firstMessage = result.Messages.First();
        firstMessage.Sender.Should().Be("Sarah Wilson");
        firstMessage.Content.Should().Be("Hey, are you coming to the meeting?");
        firstMessage.Timestamp.Month.Should().Be(1);
        firstMessage.Timestamp.Day.Should().Be(5);
        firstMessage.Timestamp.Year.Should().Be(2024);
    }

    [Fact(DisplayName = "ParseAsync with multi-line messages parses correctly")]
    public async Task ParseChatFileAsync_MultiLineMessages_ParsesCorrectly()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-edge-cases.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();

        // Find the multi-line message from Emma Thompson
        var multiLineMessage = result.Messages
            .FirstOrDefault(m => m.Content.Contains("Here's a long message that spans"));

        multiLineMessage.Should().NotBeNull();
        multiLineMessage!.Content.Should().Contain("multiple lines");
        multiLineMessage.Content.Should().Contain("to say about this topic");
        multiLineMessage.Content.Should().Contain(Environment.NewLine);

        // Find David Lee's multi-line message
        var davidMultiLine = result.Messages
            .FirstOrDefault(m => m.Content.Contains("I'm back! Had connection issues"));

        davidMultiLine.Should().NotBeNull();
        davidMultiLine!.Content.Should().Contain("The app crashed for a moment");
        davidMultiLine.Content.Should().Contain("but everything is fine now");
    }

    [Fact(DisplayName = "ParseAsync with unparseable lines tracks in metadata")]
    public async Task ParseChatFileAsync_UnparseableLines_TracksInMetadata()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-edge-cases.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();

        // System messages like encryption notice, "created group", "left", "added" should fail
        // because they don't have the sender: content format
        result.Metadata.FailedLineCount.Should().BeGreaterThan(0);

        // The total lines minus continuation lines should match parsed + failed
        result.Metadata.TotalLines.Should().Be(28);
    }

    [Fact(DisplayName = "ParseAsync with empty file returns empty export")]
    public async Task ParseChatFileAsync_EmptyFile_ReturnsEmptyExport()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, string.Empty);

            var result = await _parser.ParseAsync(tempFile);

            result.Should().NotBeNull();
            result.Messages.Should().BeEmpty();
            result.Metadata.TotalLines.Should().Be(0);
            result.Metadata.ParsedMessageCount.Should().Be(0);
            result.Metadata.FailedLineCount.Should().Be(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact(DisplayName = "ParseAsync with emoji messages handles correctly")]
    public async Task ParseChatFileAsync_MessagesWithEmoji_HandlesCorrectly()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-edge-cases.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();

        // Find the emoji-only message
        var emojiMessage = result.Messages
            .FirstOrDefault(m => m.Sender == "David Lee" && m.Content.Contains("🎉🎊🥳"));

        emojiMessage.Should().NotBeNull();
        emojiMessage!.Content.Should().Contain("🎉🎊🥳🎁🎄🎃🎆🎇✨🌟");

        // Check the greeting with emoji
        var greetingMessage = result.Messages
            .FirstOrDefault(m => m.Content.Contains("Hello everyone! This is a new group 👋"));

        greetingMessage.Should().NotBeNull();
        greetingMessage!.Content.Should().Contain("👋");
    }

    [Fact(DisplayName = "ParseAsync validates message ordering maintains chronological order")]
    public async Task ParseChatFileAsync_ValidatesMessageOrdering_MaintainsChronologicalOrder()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();
        result.Messages.Should().HaveCountGreaterThan(1);

        // Verify messages are in chronological order
        for (var i = 1; i < result.Messages.Count; i++)
        {
            result.Messages[i].Timestamp.Should()
                .BeOnOrAfter(result.Messages[i - 1].Timestamp,
                    $"Message at index {i} should be on or after message at index {i - 1}");
        }
    }

    [Fact(DisplayName = "ParseAsync with null file path throws ArgumentException")]
    public async Task ParseAsync_NullFilePath_ThrowsArgumentException()
    {
        Func<Task> act = () => _parser.ParseAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath");
    }

    [Fact(DisplayName = "ParseAsync with empty file path throws ArgumentException")]
    public async Task ParseAsync_EmptyFilePath_ThrowsArgumentException()
    {
        Func<Task> act = () => _parser.ParseAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath");
    }

    [Fact(DisplayName = "ParseAsync with non-existent file throws FileNotFoundException")]
    public async Task ParseAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        Func<Task> act = () => _parser.ParseAsync("/non/existent/path/file.txt");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact(DisplayName = "ParseAsync handles phone number senders correctly")]
    public async Task ParseAsync_PhoneNumberSenders_HandlesCorrectly()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-edge-cases.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();

        // Find messages from phone number sender
        var phoneMessages = result.Messages
            .Where(m => m.Sender.StartsWith("+44"))
            .ToList();

        phoneMessages.Should().NotBeEmpty();
        phoneMessages.First().Sender.Should().Be("+44 7911 123456");
    }

    [Fact(DisplayName = "ParseAsync handles special characters in content correctly")]
    public async Task ParseAsync_SpecialCharactersInContent_HandlesCorrectly()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-edge-cases.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();

        // Find message with special characters
        var specialCharsMessage = result.Messages
            .FirstOrDefault(m => m.Content.Contains("@#$%^&*()"));

        specialCharsMessage.Should().NotBeNull();
        specialCharsMessage!.Content.Should().Contain("@#$%^&*()[]{}|;:'\"");
    }

    private static string GetSampleDataPath()
    {
        // Search for the SampleData directory by walking up from the current directory
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDir is not null)
        {
            var testsDir = Path.Combine(currentDir.FullName, "tests", "SampleData");
            if (Directory.Exists(testsDir))
            {
                return testsDir;
            }

            var sampleDataDir = Path.Combine(currentDir.FullName, "SampleData");
            if (Directory.Exists(sampleDataDir))
            {
                return sampleDataDir;
            }

            currentDir = currentDir.Parent;
        }

        // Fallback: try relative paths from test output directory
        var outputDir = Directory.GetCurrentDirectory();
        var relativePaths = new[]
        {
            Path.Combine(outputDir, "..", "..", "..", "..", "..", "tests", "SampleData"),
            Path.Combine(outputDir, "..", "..", "..", "..", "SampleData")
        };

        foreach (var path in relativePaths)
        {
            if (Directory.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find SampleData directory. Searched from: " + Directory.GetCurrentDirectory());
    }
}
