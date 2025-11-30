using WhatsAppArchiver.Infrastructure.Parsers;

namespace WhatsAppArchiver.Infrastructure.Tests.Parsers;

public class WhatsAppTextFileParserTests : IDisposable
{
    private readonly string _testDataDirectory;
    private readonly List<string> _tempFiles = [];

    public WhatsAppTextFileParserTests()
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "WhatsAppParserTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataDirectory);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            File.Delete(file);
        }

        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateTestFile(string content, string fileName = "test.txt")
    {
        var filePath = Path.Combine(_testDataDirectory, fileName);
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);

        return filePath;
    }

    [Fact(DisplayName = "ParseAsync with valid single message parses correctly")]
    public async Task ParseAsync_ValidSingleMessage_ParsesCorrectly()
    {
        var content = "25/12/2024 14:30 - John Doe: Hello, World!";
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Single(result.Messages);
        Assert.Equal("John Doe", result.Messages[0].Sender);
        Assert.Equal("Hello, World!", result.Messages[0].Content);
        Assert.Equal(2024, result.Messages[0].Timestamp.Year);
        Assert.Equal(12, result.Messages[0].Timestamp.Month);
        Assert.Equal(25, result.Messages[0].Timestamp.Day);
        Assert.Equal(14, result.Messages[0].Timestamp.Hour);
        Assert.Equal(30, result.Messages[0].Timestamp.Minute);
    }

    [Fact(DisplayName = "ParseAsync with multiple messages parses all messages")]
    public async Task ParseAsync_MultipleMessages_ParsesAllMessages()
    {
        var content = """
            25/12/2024 14:30 - John: Hello
            25/12/2024 14:31 - Jane: Hi there
            25/12/2024 14:32 - John: How are you?
            """;
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Equal(3, result.Messages.Count);
        Assert.Equal("John", result.Messages[0].Sender);
        Assert.Equal("Jane", result.Messages[1].Sender);
        Assert.Equal("John", result.Messages[2].Sender);
    }

    [Fact(DisplayName = "ParseAsync with multi-line message concatenates content")]
    public async Task ParseAsync_MultiLineMessage_ConcatenatesContent()
    {
        var content = """
            25/12/2024 14:30 - John: First line
            Second line of the message
            Third line
            25/12/2024 14:31 - Jane: Single line
            """;
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Equal(2, result.Messages.Count);
        Assert.Contains("First line", result.Messages[0].Content);
        Assert.Contains("Second line of the message", result.Messages[0].Content);
        Assert.Contains("Third line", result.Messages[0].Content);
        Assert.Equal("Single line", result.Messages[1].Content);
    }

    [Fact(DisplayName = "ParseAsync with empty lines ignores them")]
    public async Task ParseAsync_EmptyLines_IgnoresThem()
    {
        var content = """
            25/12/2024 14:30 - John: Hello

            25/12/2024 14:31 - Jane: Hi
            """;
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Equal(2, result.Messages.Count);
    }

    [Fact(DisplayName = "ParseAsync sets metadata correctly")]
    public async Task ParseAsync_ValidFile_SetsMetadataCorrectly()
    {
        var content = """
            25/12/2024 14:30 - John: Hello
            25/12/2024 14:31 - Jane: Hi
            """;
        var fileName = "chat_export.txt";
        var filePath = CreateTestFile(content, fileName);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Equal(fileName, result.Metadata.SourceFileName);
        Assert.Equal(2, result.Metadata.ParsedMessageCount);
        Assert.True(result.Metadata.ParsedAt <= DateTimeOffset.UtcNow);
    }

    [Fact(DisplayName = "ParseAsync with null file path throws ArgumentException")]
    public async Task ParseAsync_NullFilePath_ThrowsArgumentException()
    {
        var parser = new WhatsAppTextFileParser();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => parser.ParseAsync(null!));

        Assert.Equal("filePath", exception.ParamName);
    }

    [Fact(DisplayName = "ParseAsync with whitespace file path throws ArgumentException")]
    public async Task ParseAsync_WhitespaceFilePath_ThrowsArgumentException()
    {
        var parser = new WhatsAppTextFileParser();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => parser.ParseAsync("   "));

        Assert.Equal("filePath", exception.ParamName);
    }

    [Fact(DisplayName = "ParseAsync with non-existent file throws FileNotFoundException")]
    public async Task ParseAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        var parser = new WhatsAppTextFileParser();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            parser.ParseAsync("/nonexistent/path/file.txt"));
    }

    [Fact(DisplayName = "ParseAsync with single digit date and time parses correctly")]
    public async Task ParseAsync_SingleDigitDateTime_ParsesCorrectly()
    {
        var content = "5/1/2024 9:05 - John: Morning message";
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Single(result.Messages);
        Assert.Equal(2024, result.Messages[0].Timestamp.Year);
        Assert.Equal(1, result.Messages[0].Timestamp.Month);
        Assert.Equal(5, result.Messages[0].Timestamp.Day);
    }

    [Fact(DisplayName = "ParseAsync with message containing colons parses correctly")]
    public async Task ParseAsync_MessageWithColons_ParsesCorrectly()
    {
        var content = "25/12/2024 14:30 - John: Time is: 14:30:00";
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Single(result.Messages);
        Assert.Equal("Time is: 14:30:00", result.Messages[0].Content);
    }

    [Fact(DisplayName = "ParseAsync with sender containing special characters parses correctly")]
    public async Task ParseAsync_SenderWithSpecialCharacters_ParsesCorrectly()
    {
        var content = "25/12/2024 14:30 - João da Silva: Olá!";
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Single(result.Messages);
        Assert.Equal("João da Silva", result.Messages[0].Sender);
        Assert.Equal("Olá!", result.Messages[0].Content);
    }

    [Fact(DisplayName = "ParseAsync with empty file returns empty messages")]
    public async Task ParseAsync_EmptyFile_ReturnsEmptyMessages()
    {
        var filePath = CreateTestFile(string.Empty);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Empty(result.Messages);
        Assert.Equal(0, result.Metadata.ParsedMessageCount);
    }

    [Fact(DisplayName = "ParseAsync generates unique message IDs")]
    public async Task ParseAsync_MultipleMessages_GeneratesUniqueMessageIds()
    {
        var content = """
            25/12/2024 14:30 - John: Hello
            25/12/2024 14:31 - Jane: World
            """;
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        Assert.Equal(2, result.Messages.Count);
        Assert.NotEqual(result.Messages[0].Id, result.Messages[1].Id);
    }

    [Fact(DisplayName = "ParseAsync respects cancellation token")]
    public async Task ParseAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var content = "25/12/2024 14:30 - John: Hello";
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            parser.ParseAsync(filePath, cts.Token));
    }

    [Fact(DisplayName = "ParseAsync with real-world WhatsApp export parses correctly")]
    public async Task ParseAsync_RealWorldExport_ParsesCorrectly()
    {
        var content = """
            01/01/2024 00:01 - Messages and calls are end-to-end encrypted. No one outside of this chat, not even WhatsApp, can read or listen to them. Tap to learn more.
            01/01/2024 10:30 - Alice: Happy New Year! 🎉
            01/01/2024 10:31 - Bob: Happy New Year to you too!
            Hope 2024 is amazing!
            01/01/2024 10:32 - Alice: Thank you! 💕
            """;
        var filePath = CreateTestFile(content);
        var parser = new WhatsAppTextFileParser();

        var result = await parser.ParseAsync(filePath);

        // System message line (encryption notice) doesn't match pattern (no colon after sender)
        Assert.Equal(3, result.Messages.Count);
        Assert.Equal("Alice", result.Messages[0].Sender);
        Assert.Equal("Bob", result.Messages[1].Sender);
        Assert.Contains("Hope 2024 is amazing!", result.Messages[1].Content);
        Assert.Equal("Alice", result.Messages[2].Sender);
    }
}
