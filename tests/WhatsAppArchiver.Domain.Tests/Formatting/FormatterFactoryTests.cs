using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class FormatterFactoryTests
{
    [Fact(DisplayName = "Create with Default returns DefaultMessageFormatter")]
    public void Create_DefaultType_ReturnsDefaultMessageFormatter()
    {
        var formatter = FormatterFactory.Create(MessageFormatType.Default);

        Assert.IsType<DefaultMessageFormatter>(formatter);
    }

    [Fact(DisplayName = "Create with Compact returns CompactMessageFormatter")]
    public void Create_CompactType_ReturnsCompactMessageFormatter()
    {
        var formatter = FormatterFactory.Create(MessageFormatType.Compact);

        Assert.IsType<CompactMessageFormatter>(formatter);
    }

    [Fact(DisplayName = "Create with Verbose returns VerboseMessageFormatter")]
    public void Create_VerboseType_ReturnsVerboseMessageFormatter()
    {
        var formatter = FormatterFactory.Create(MessageFormatType.Verbose);

        Assert.IsType<VerboseMessageFormatter>(formatter);
    }

    [Fact(DisplayName = "Create with invalid format type throws ArgumentOutOfRangeException")]
    public void Create_InvalidFormatType_ThrowsArgumentOutOfRangeException()
    {
        var invalidFormatType = (MessageFormatType)999;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FormatterFactory.Create(invalidFormatType));

        Assert.Equal("formatType", exception.ParamName);
    }

    [Fact(DisplayName = "Create with MarkdownDocument returns MarkdownDocumentFormatter")]
    public void Create_MarkdownDocumentType_ReturnsMarkdownDocumentFormatter()
    {
        var formatter = FormatterFactory.Create(MessageFormatType.MarkdownDocument);

        Assert.IsType<MarkdownDocumentFormatter>(formatter);
        Assert.IsAssignableFrom<IDocumentFormatter>(formatter);
        Assert.IsAssignableFrom<IMessageFormatter>(formatter);
    }

    [Theory(DisplayName = "Create returns IMessageFormatter for all valid types")]
    [InlineData(MessageFormatType.Default)]
    [InlineData(MessageFormatType.Compact)]
    [InlineData(MessageFormatType.Verbose)]
    [InlineData(MessageFormatType.MarkdownDocument)]
    [InlineData(MessageFormatType.GoogleDocs)]
    public void Create_AllValidTypes_ReturnsIMessageFormatter(MessageFormatType formatType)
    {
        var formatter = FormatterFactory.Create(formatType);

        Assert.IsAssignableFrom<IMessageFormatter>(formatter);
    }

    [Fact(DisplayName = "Create returns new instance each time")]
    public void Create_CalledTwice_ReturnsNewInstances()
    {
        var formatter1 = FormatterFactory.Create(MessageFormatType.Default);
        var formatter2 = FormatterFactory.Create(MessageFormatType.Default);

        Assert.NotSame(formatter1, formatter2);
    }

    [Fact(DisplayName = "IsDocumentFormatter with MarkdownDocument returns true")]
    public void IsDocumentFormatter_MarkdownDocumentType_ReturnsTrue()
    {
        var result = FormatterFactory.IsDocumentFormatter(MessageFormatType.MarkdownDocument);

        Assert.True(result);
    }

    [Theory(DisplayName = "IsDocumentFormatter with message-level types returns false")]
    [InlineData(MessageFormatType.Default)]
    [InlineData(MessageFormatType.Compact)]
    [InlineData(MessageFormatType.Verbose)]
    public void IsDocumentFormatter_MessageLevelTypes_ReturnsFalse(MessageFormatType formatType)
    {
        var result = FormatterFactory.IsDocumentFormatter(formatType);

        Assert.False(result);
    }

    [Fact(DisplayName = "Create with GoogleDocs returns GoogleDocsDocumentFormatter")]
    public void Create_GoogleDocsType_ReturnsGoogleDocsDocumentFormatter()
    {
        var formatter = FormatterFactory.Create(MessageFormatType.GoogleDocs);

        Assert.IsType<GoogleDocsDocumentFormatter>(formatter);
    }

    [Fact(DisplayName = "Create with GoogleDocs implements IGoogleDocsFormatter")]
    public void Create_GoogleDocsType_ImplementsIGoogleDocsFormatter()
    {
        var formatter = FormatterFactory.Create(MessageFormatType.GoogleDocs);

        Assert.IsAssignableFrom<IGoogleDocsFormatter>(formatter);
        Assert.IsAssignableFrom<IMessageFormatter>(formatter);
    }

    [Fact(DisplayName = "IsDocumentFormatter with GoogleDocs returns true")]
    public void IsDocumentFormatter_GoogleDocs_ReturnsTrue()
    {
        var result = FormatterFactory.IsDocumentFormatter(MessageFormatType.GoogleDocs);

        Assert.True(result);
    }
}
