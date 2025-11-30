using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;

namespace WhatsAppArchiver.Domain.Tests.Specifications;

public class SenderFilterTests
{
    [Fact(DisplayName = "Constructor with valid sender name creates SenderFilter")]
    public void Constructor_ValidSenderName_CreatesSenderFilter()
    {
        var senderName = "John Doe";

        var filter = new SenderFilter(senderName);

        Assert.Equal(senderName, filter.SenderName);
    }

    [Theory(DisplayName = "Constructor with null or whitespace sender name throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceSenderName_ThrowsArgumentException(string? senderName)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SenderFilter(senderName!));

        Assert.Equal("senderName", exception.ParamName);
    }

    [Fact(DisplayName = "IsSatisfiedBy with matching sender returns true")]
    public void IsSatisfiedBy_MatchingSender_ReturnsTrue()
    {
        var filter = new SenderFilter("John");
        var message = new ChatMessage(DateTimeOffset.UtcNow, "John", "Hello");

        var result = filter.IsSatisfiedBy(message);

        Assert.True(result);
    }

    [Fact(DisplayName = "IsSatisfiedBy with non-matching sender returns false")]
    public void IsSatisfiedBy_NonMatchingSender_ReturnsFalse()
    {
        var filter = new SenderFilter("John");
        var message = new ChatMessage(DateTimeOffset.UtcNow, "Jane", "Hello");

        var result = filter.IsSatisfiedBy(message);

        Assert.False(result);
    }

    [Theory(DisplayName = "IsSatisfiedBy with case-insensitive matching returns true")]
    [InlineData("john", "John")]
    [InlineData("JOHN", "john")]
    [InlineData("JoHn", "jOhN")]
    [InlineData("John Doe", "john doe")]
    public void IsSatisfiedBy_CaseInsensitiveMatching_ReturnsTrue(string filterName, string messageSender)
    {
        var filter = new SenderFilter(filterName);
        var message = new ChatMessage(DateTimeOffset.UtcNow, messageSender, "Hello");

        var result = filter.IsSatisfiedBy(message);

        Assert.True(result);
    }

    [Fact(DisplayName = "IsSatisfiedBy with null candidate throws ArgumentNullException")]
    public void IsSatisfiedBy_NullCandidate_ThrowsArgumentNullException()
    {
        var filter = new SenderFilter("John");

        Assert.Throws<ArgumentNullException>(() => filter.IsSatisfiedBy(null!));
    }

    [Fact(DisplayName = "Create with valid sender name creates SenderFilter")]
    public void Create_ValidSenderName_CreatesSenderFilter()
    {
        var filter = SenderFilter.Create("John");

        Assert.NotNull(filter);
        Assert.Equal("John", filter.SenderName);
    }

    [Fact(DisplayName = "IsSatisfiedBy with partial match returns false")]
    public void IsSatisfiedBy_PartialMatch_ReturnsFalse()
    {
        var filter = new SenderFilter("John");
        var message = new ChatMessage(DateTimeOffset.UtcNow, "John Doe", "Hello");

        var result = filter.IsSatisfiedBy(message);

        Assert.False(result);
    }

    [Fact(DisplayName = "SenderFilter implements ISpecification interface")]
    public void SenderFilter_ImplementsISpecification()
    {
        var filter = new SenderFilter("John");

        Assert.IsAssignableFrom<ISpecification<ChatMessage>>(filter);
    }
}
