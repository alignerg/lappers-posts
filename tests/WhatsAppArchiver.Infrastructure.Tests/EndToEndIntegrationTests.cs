using FluentAssertions;

using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;
using WhatsAppArchiver.Infrastructure.Parsers;
using WhatsAppArchiver.Infrastructure.Repositories;

namespace WhatsAppArchiver.Infrastructure.Tests;

/// <summary>
/// End-to-end integration tests that validate cross-layer operations
/// between the Domain, Application, and Infrastructure layers.
/// </summary>
/// <remarks>
/// These tests use real file I/O with sample data to verify the complete
/// integration of parsing, formatting, filtering, and state persistence.
/// </remarks>
public sealed class EndToEndIntegrationTests : IDisposable
{
    private readonly WhatsAppTextFileParser _parser;
    private readonly string _sampleDataPath;
    private readonly string _testDirectory;

    public EndToEndIntegrationTests()
    {
        _parser = new WhatsAppTextFileParser();
        _sampleDataPath = TestFileUtils.GetSampleDataPath();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"EndToEndIntegrationTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact(DisplayName = "Parse and format sample chat produces expected output")]
    public async Task ParseAndFormat_SampleChat_ProducesExpectedOutput()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");
        var formatter = new DefaultMessageFormatter();

        var chatExport = await _parser.ParseAsync(filePath);
        var formattedMessages = chatExport.Messages
            .Select(message => formatter.FormatMessage(message))
            .ToList();

        chatExport.Should().NotBeNull();
        chatExport.Messages.Should().HaveCount(10);
        formattedMessages.Should().HaveCount(10);

        // Verify the first message formatted output matches the expected pattern
        var firstFormatted = formattedMessages[0];
        firstFormatted.Should().StartWith("[25/12/2024, 09:15:00]");
        firstFormatted.Should().Contain("John Smith:");
        firstFormatted.Should().Contain("Good morning everyone!");

        // Verify formatted messages contain all senders
        var allFormatted = string.Join(Environment.NewLine, formattedMessages);
        allFormatted.Should().Contain("John Smith:");
        allFormatted.Should().Contain("Maria Garcia:");
        allFormatted.Should().Contain("Alex Johnson:");

        // Verify the format pattern: [DD/MM/YYYY, HH:mm:ss] Sender: Content
        foreach (var formatted in formattedMessages)
        {
            formatted.Should().MatchRegex(@"^\[\d{2}/\d{2}/\d{4}, \d{2}:\d{2}:\d{2}\] .+: .+$");
        }
    }

    [Fact(DisplayName = "Filter by sender in multi-sender chat returns only sender messages")]
    public async Task FilterBySender_MultiSenderChat_ReturnsOnlySenderMessages()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");
        const string targetSender = "John Smith";

        var chatExport = await _parser.ParseAsync(filePath);
        var johnMessages = chatExport.GetMessagesBySender(targetSender).ToList();

        johnMessages.Should().NotBeEmpty();
        johnMessages.Should().HaveCount(4, "John Smith sends 4 messages in sample-dd-mm-yyyy.txt");

        // Verify all returned messages are from John Smith
        johnMessages.Should().OnlyContain(
            m => m.Sender.Equals(targetSender, StringComparison.OrdinalIgnoreCase),
            "all messages should be from the specified sender");

        // Verify message content matches expected messages
        var contents = johnMessages.Select(m => m.Content).ToList();
        contents.Should().Contain("Good morning everyone! 🎄");
        contents.Should().Contain("Hope you're having a wonderful holiday");
        contents.Should().Contain("I'm starving! Let's meet at 1 PM");
        contents.Should().Contain("Good morning! Back to work tomorrow 😢");

        // Verify using the SenderFilter specification directly
        var senderFilter = SenderFilter.Create(targetSender);
        var filteredMessages = chatExport.FilterMessages(senderFilter).ToList();
        filteredMessages.Should().BeEquivalentTo(johnMessages);
    }

    [Fact(DisplayName = "Filter by sender with case insensitivity returns correct messages")]
    public async Task FilterBySender_CaseInsensitive_ReturnsCorrectMessages()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-m-d-yy.txt");

        var chatExport = await _parser.ParseAsync(filePath);
        var sarahMessagesLower = chatExport.GetMessagesBySender("sarah wilson").ToList();
        var sarahMessagesUpper = chatExport.GetMessagesBySender("SARAH WILSON").ToList();
        var sarahMessagesMixed = chatExport.GetMessagesBySender("Sarah Wilson").ToList();

        sarahMessagesLower.Should().HaveCount(5);
        sarahMessagesUpper.Should().HaveCount(5);
        sarahMessagesMixed.Should().HaveCount(5);

        sarahMessagesLower.Should().BeEquivalentTo(sarahMessagesUpper);
        sarahMessagesUpper.Should().BeEquivalentTo(sarahMessagesMixed);
    }

    [Fact(DisplayName = "Processing checkpoint save and load maintains state")]
    public async Task ProcessingCheckpoint_SaveAndLoad_MaintainsState()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");
        var chatExport = await _parser.ParseAsync(filePath);
        var documentId = "test-document-" + Guid.NewGuid().ToString("N")[..8];
        var repository = new JsonFileStateRepository(_testDirectory);

        var checkpoint = ProcessingCheckpoint.Create(documentId);
        var messagesToMark = chatExport.Messages.Take(5).ToList();

        foreach (var message in messagesToMark)
        {
            checkpoint.MarkAsProcessed(message.Id);
        }

        await repository.SaveCheckpointAsync(checkpoint);

        var newRepository = new JsonFileStateRepository(_testDirectory);
        var loadedCheckpoint = await newRepository.GetCheckpointAsync(documentId);

        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.Id.Should().Be(checkpoint.Id);
        loadedCheckpoint.DocumentId.Should().Be(documentId);
        loadedCheckpoint.ProcessedCount.Should().Be(5);

        // Verify all marked messages are still marked as processed
        foreach (var message in messagesToMark)
        {
            loadedCheckpoint.HasBeenProcessed(message.Id).Should().BeTrue(
                $"message '{message.Content[..Math.Min(20, message.Content.Length)]}...' should be marked as processed");
        }

        // Verify messages not marked are not in the checkpoint
        var unprocessedMessages = chatExport.Messages.Skip(5).ToList();
        foreach (var message in unprocessedMessages)
        {
            loadedCheckpoint.HasBeenProcessed(message.Id).Should().BeFalse(
                "message should not be marked as processed");
        }

        // Verify last processed timestamp
        loadedCheckpoint.LastProcessedTimestamp.Should().NotBeNull();
        loadedCheckpoint.LastProcessedTimestamp.Should().Be(
            messagesToMark.Max(m => m.Timestamp));
    }

    [Fact(DisplayName = "Processing checkpoint with sender filter save and load maintains filter")]
    public async Task ProcessingCheckpoint_WithSenderFilter_MaintainsFilter()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");
        var chatExport = await _parser.ParseAsync(filePath);
        var documentId = "sender-filter-test-" + Guid.NewGuid().ToString("N")[..8];
        var senderFilter = SenderFilter.Create("Maria Garcia");
        var repository = new JsonFileStateRepository(_testDirectory);

        var mariaMessages = chatExport.GetMessagesBySender("Maria Garcia").ToList();
        var checkpoint = ProcessingCheckpoint.Create(documentId, senderFilter);

        foreach (var message in mariaMessages)
        {
            checkpoint.MarkAsProcessed(message.Id);
        }

        await repository.SaveCheckpointAsync(checkpoint);
        var loadedCheckpoint = await repository.GetCheckpointAsync(documentId, senderFilter);

        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.SenderFilter.Should().NotBeNull();
        loadedCheckpoint.SenderFilter!.SenderName.Should().Be("Maria Garcia");
        loadedCheckpoint.ProcessedCount.Should().Be(mariaMessages.Count);

        foreach (var message in mariaMessages)
        {
            loadedCheckpoint.HasBeenProcessed(message.Id).Should().BeTrue();
        }
    }

    [Fact(DisplayName = "Full workflow parse filter format and checkpoint maintains consistency")]
    public async Task FullWorkflow_ParseFilterFormatCheckpoint_MaintainsConsistency()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-m-d-yy.txt");
        var documentId = "full-workflow-" + Guid.NewGuid().ToString("N")[..8];
        var senderFilter = SenderFilter.Create("Michael Brown");
        var formatter = new DefaultMessageFormatter();
        var repository = new JsonFileStateRepository(_testDirectory);

        var chatExport = await _parser.ParseAsync(filePath);
        var michaelMessages = chatExport.FilterMessages(senderFilter).ToList();
        var formattedMessages = michaelMessages
            .Select(m => formatter.FormatMessage(m))
            .ToList();

        var checkpoint = ProcessingCheckpoint.Create(documentId, senderFilter);
        foreach (var message in michaelMessages)
        {
            checkpoint.MarkAsProcessed(message.Id);
        }
        await repository.SaveCheckpointAsync(checkpoint);

        var loadedCheckpoint = await repository.GetCheckpointAsync(documentId, senderFilter);

        michaelMessages.Should().HaveCount(5);
        formattedMessages.Should().HaveCount(5);

        // All formatted messages should contain Michael Brown
        formattedMessages.Should().OnlyContain(f => f.Contains("Michael Brown:"));

        loadedCheckpoint.ProcessedCount.Should().Be(5);
        loadedCheckpoint.SenderFilter.Should().NotBeNull();
        loadedCheckpoint.SenderFilter!.SenderName.Should().Be("Michael Brown");

        // Verify we can determine which messages were already processed
        foreach (var message in michaelMessages)
        {
            loadedCheckpoint.HasBeenProcessed(message.Id).Should().BeTrue();
        }

        // Verify unprocessed messages are not in checkpoint
        var sarahMessages = chatExport.GetMessagesBySender("Sarah Wilson");
        foreach (var message in sarahMessages)
        {
            loadedCheckpoint.HasBeenProcessed(message.Id).Should().BeFalse();
        }
    }

    [Fact(DisplayName = "Get distinct senders returns all unique senders")]
    public async Task GetDistinctSenders_MultiSenderChat_ReturnsAllUniqueSenders()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");

        var chatExport = await _parser.ParseAsync(filePath);
        var senders = chatExport.GetDistinctSenders().ToList();

        senders.Should().HaveCount(3);
        senders.Should().Contain("John Smith");
        senders.Should().Contain("Maria Garcia");
        senders.Should().Contain("Alex Johnson");
    }

    [Fact(DisplayName = "Parse file with errors tracks failed line count in metadata")]
    public async Task ParseFileWithErrors_TracksFailedLineCount_InMetadata()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-with-errors.txt");

        var chatExport = await _parser.ParseAsync(filePath);

        chatExport.Should().NotBeNull();
        chatExport.Metadata.Should().NotBeNull();

        // Sample file has 7 total lines
        chatExport.Metadata.TotalLines.Should().Be(7);

        // Should successfully parse 4 messages (lines with valid format)
        chatExport.Metadata.ParsedMessageCount.Should().Be(4);

        // Should have at least 1 failed line (orphaned first line with no prior message)
        // The parser counts orphan lines and lines with invalid timestamps as failures
        chatExport.Metadata.FailedLineCount.Should().BeGreaterThan(0);

        // Verify successfully parsed messages are correct
        chatExport.Messages.Should().HaveCount(4);
        var senders = chatExport.Messages.Select(m => m.Sender).Distinct().ToList();
        senders.Should().Contain("John Smith");
        senders.Should().Contain("Maria Garcia");
        senders.Should().Contain("Alex Johnson");
    }

    [Fact(DisplayName = "Parse file with errors can still process valid messages")]
    public async Task ParseFileWithErrors_CanStillProcessValidMessages()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-with-errors.txt");

        var chatExport = await _parser.ParseAsync(filePath);

        // Even with parsing errors, valid messages should be processed correctly
        chatExport.Messages.Should().HaveCount(4);

        var johnMessages = chatExport.GetMessagesBySender("John Smith").ToList();
        johnMessages.Should().HaveCount(2);

        // Verify specific message content
        johnMessages.Should().Contain(m => m.Content.Contains("Good morning everyone!"));
        johnMessages.Should().Contain(m => m.Content.Contains("Hope you're having a wonderful holiday"));
    }
}
