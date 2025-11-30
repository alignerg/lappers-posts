using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.ValueObjects;

public class ParsingMetadataTests
{
    [Fact(DisplayName = "Constructor with valid parameters creates ParsingMetadata")]
    public void Constructor_ValidParameters_CreatesParsingMetadata()
    {
        var sourceFileName = "chat.txt";
        var parsedAt = DateTimeOffset.UtcNow;
        var totalLines = 100;
        var parsedMessageCount = 95;
        var failedLineCount = 5;

        var metadata = new ParsingMetadata(sourceFileName, parsedAt, totalLines, parsedMessageCount, failedLineCount);

        Assert.Equal(sourceFileName, metadata.SourceFileName);
        Assert.Equal(parsedAt, metadata.ParsedAt);
        Assert.Equal(totalLines, metadata.TotalLines);
        Assert.Equal(parsedMessageCount, metadata.ParsedMessageCount);
        Assert.Equal(failedLineCount, metadata.FailedLineCount);
    }

    [Theory(DisplayName = "Constructor with null or whitespace source file name throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceSourceFileName_ThrowsArgumentException(string? sourceFileName)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ParsingMetadata(sourceFileName!, DateTimeOffset.UtcNow, 100, 95, 5));

        Assert.Equal("sourceFileName", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with negative total lines throws ArgumentOutOfRangeException")]
    public void Constructor_NegativeTotalLines_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParsingMetadata("chat.txt", DateTimeOffset.UtcNow, -1, 95, 5));

        Assert.Equal("totalLines", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with negative parsed message count throws ArgumentOutOfRangeException")]
    public void Constructor_NegativeParsedMessageCount_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParsingMetadata("chat.txt", DateTimeOffset.UtcNow, 100, -1, 5));

        Assert.Equal("parsedMessageCount", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with negative failed line count throws ArgumentOutOfRangeException")]
    public void Constructor_NegativeFailedLineCount_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParsingMetadata("chat.txt", DateTimeOffset.UtcNow, 100, 95, -1));

        Assert.Equal("failedLineCount", exception.ParamName);
    }

    [Fact(DisplayName = "Create with valid parameters creates ParsingMetadata")]
    public void Create_ValidParameters_CreatesParsingMetadata()
    {
        var sourceFileName = "chat.txt";
        var parsedAt = DateTimeOffset.UtcNow;

        var metadata = ParsingMetadata.Create(sourceFileName, parsedAt, 100, 95, 5);

        Assert.NotNull(metadata);
        Assert.Equal(sourceFileName, metadata.SourceFileName);
    }

    [Fact(DisplayName = "Equality with same values returns true")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var parsedAt = DateTimeOffset.UtcNow;
        var metadata1 = new ParsingMetadata("chat.txt", parsedAt, 100, 95, 5);
        var metadata2 = new ParsingMetadata("chat.txt", parsedAt, 100, 95, 5);

        Assert.Equal(metadata1, metadata2);
    }

    [Fact(DisplayName = "Equality with different values returns false")]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var parsedAt = DateTimeOffset.UtcNow;
        var metadata1 = new ParsingMetadata("chat1.txt", parsedAt, 100, 95, 5);
        var metadata2 = new ParsingMetadata("chat2.txt", parsedAt, 100, 95, 5);

        Assert.NotEqual(metadata1, metadata2);
    }

    [Fact(DisplayName = "Constructor with zero counts is valid")]
    public void Constructor_ZeroCounts_CreatesValidMetadata()
    {
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.UtcNow, 0, 0, 0);

        Assert.Equal(0, metadata.TotalLines);
        Assert.Equal(0, metadata.ParsedMessageCount);
        Assert.Equal(0, metadata.FailedLineCount);
    }
}
