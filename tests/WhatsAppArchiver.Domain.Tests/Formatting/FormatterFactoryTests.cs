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

    [Fact(DisplayName = "Create with MarkdownDocument throws ArgumentException indicating IDocumentFormatter required")]
    public void Create_MarkdownDocumentType_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            FormatterFactory.Create(MessageFormatType.MarkdownDocument));

        Assert.Equal("formatType", exception.ParamName);
        Assert.Contains("IDocumentFormatter", exception.Message);
        Assert.Contains("batch processing", exception.Message);
    }

    [Theory(DisplayName = "Create returns IMessageFormatter for all valid types")]
    [InlineData(MessageFormatType.Default)]
    [InlineData(MessageFormatType.Compact)]
    [InlineData(MessageFormatType.Verbose)]
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
}
