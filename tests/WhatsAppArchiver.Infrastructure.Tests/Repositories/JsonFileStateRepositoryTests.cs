using System.Text.Json;

using FluentAssertions;

using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;
using WhatsAppArchiver.Infrastructure.Repositories;

namespace WhatsAppArchiver.Infrastructure.Tests.Repositories;

public sealed class JsonFileStateRepositoryTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly JsonFileStateRepository _repository;

    public JsonFileStateRepositoryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JsonFileStateRepositoryTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _repository = new JsonFileStateRepository(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact(DisplayName = "SaveCheckpointAsync with new checkpoint creates JSON file")]
    public async Task SaveCheckpointAsync_NewCheckpoint_CreatesJsonFile()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Hello World");
        checkpoint.MarkAsProcessed(messageId);

        await _repository.SaveCheckpointAsync(checkpoint);

        var expectedPath = Path.Combine(_testDirectory, "doc-123.json");
        File.Exists(expectedPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(expectedPath);
        content.Should().Contain("doc-123");
        content.Should().Contain(messageId.ContentHash);
    }

    [Fact(DisplayName = "GetCheckpointAsync with existing file loads checkpoint")]
    public async Task GetCheckpointAsync_ExistingFile_LoadsCheckpoint()
    {
        var originalCheckpoint = ProcessingCheckpoint.Create("doc-456");
        var timestamp = DateTimeOffset.UtcNow;
        var messageId1 = MessageId.Create(timestamp, "Message 1");
        var messageId2 = MessageId.Create(timestamp.AddMinutes(1), "Message 2");
        originalCheckpoint.MarkAsProcessed(messageId1);
        originalCheckpoint.MarkAsProcessed(messageId2);
        await _repository.SaveCheckpointAsync(originalCheckpoint);

        var loadedCheckpoint = await _repository.GetCheckpointAsync("doc-456");

        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.Id.Should().Be(originalCheckpoint.Id);
        loadedCheckpoint.DocumentId.Should().Be("doc-456");
        loadedCheckpoint.ProcessedCount.Should().Be(2);
        loadedCheckpoint.HasBeenProcessed(messageId1).Should().BeTrue();
        loadedCheckpoint.HasBeenProcessed(messageId2).Should().BeTrue();
    }

    [Fact(DisplayName = "GetCheckpointAsync with no file creates new checkpoint")]
    public async Task GetCheckpointAsync_NoFile_CreatesNewCheckpoint()
    {
        var checkpoint = await _repository.GetCheckpointAsync("nonexistent-doc");

        checkpoint.Should().NotBeNull();
        checkpoint.DocumentId.Should().Be("nonexistent-doc");
        checkpoint.ProcessedCount.Should().Be(0);
        checkpoint.LastProcessedTimestamp.Should().BeNull();
        checkpoint.Id.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "SaveCheckpointAsync uses temporary file for atomic write")]
    public async Task SaveCheckpointAsync_AtomicWrite_UsesTemporaryFile()
    {
        var checkpoint = ProcessingCheckpoint.Create("atomic-test");
        var expectedPath = Path.Combine(_testDirectory, "atomic-test.json");

        await _repository.SaveCheckpointAsync(checkpoint);

        File.Exists(expectedPath).Should().BeTrue("the final file should exist");
        
        var remainingTempFiles = Directory.GetFiles(_testDirectory, "atomic-test.json.*.tmp");
        remainingTempFiles.Should().BeEmpty("all temporary files should be removed after successful write");
    }

    [Fact(DisplayName = "SaveCheckpointAsync with concurrent access handles file locking")]
    public async Task SaveCheckpointAsync_ConcurrentAccess_HandlesFileLocking()
    {
        var checkpoint1 = ProcessingCheckpoint.Create("concurrent-doc");
        var checkpoint2 = new ProcessingCheckpoint(
            checkpoint1.Id,
            "concurrent-doc",
            checkpoint1.LastProcessedTimestamp,
            checkpoint1.ProcessedMessageIds);

        var messageId1 = MessageId.Create(DateTimeOffset.UtcNow, "Message 1");
        var messageId2 = MessageId.Create(DateTimeOffset.UtcNow.AddSeconds(1), "Message 2");
        checkpoint1.MarkAsProcessed(messageId1);
        checkpoint2.MarkAsProcessed(messageId2);

        var task1 = _repository.SaveCheckpointAsync(checkpoint1);
        var task2 = _repository.SaveCheckpointAsync(checkpoint2);

        await Task.WhenAll(task1, task2);

        var loadedCheckpoint = await _repository.GetCheckpointAsync("concurrent-doc");
        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.ProcessedCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "GetCheckpointAsync with corrupted JSON throws JsonException")]
    public async Task GetCheckpointAsync_CorruptedJson_ThrowsException()
    {
        var filePath = Path.Combine(_testDirectory, "corrupted-doc.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json content }}}");

        var act = async () => await _repository.GetCheckpointAsync("corrupted-doc");

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact(DisplayName = "SaveCheckpointAsync with valid checkpoint serializes correctly")]
    public async Task SaveCheckpointAsync_ValidCheckpoint_SerializesCorrectly()
    {
        var checkpointId = Guid.NewGuid();
        var timestamp = DateTimeOffset.Parse("2024-01-15T10:30:00+00:00");
        var messageId = new MessageId(timestamp, "ABC123DEF456GHI789");
        var senderFilter = new SenderFilter("John Doe");
        var checkpoint = new ProcessingCheckpoint(
            checkpointId,
            "serialization-test",
            timestamp,
            [messageId],
            senderFilter);

        await _repository.SaveCheckpointAsync(checkpoint);

        var filePath = Path.Combine(_testDirectory, "serialization-test_john_doe.json");
        File.Exists(filePath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(filePath);
        var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement;

        root.GetProperty("id").GetGuid().Should().Be(checkpointId);
        root.GetProperty("documentId").GetString().Should().Be("serialization-test");
        root.GetProperty("senderName").GetString().Should().Be("John Doe");
        root.GetProperty("lastProcessedTimestamp").GetDateTimeOffset().Should().Be(timestamp);

        var messageIds = root.GetProperty("processedMessageIds");
        messageIds.GetArrayLength().Should().Be(1);
        messageIds[0].GetProperty("contentHash").GetString().Should().Be("ABC123DEF456GHI789");
        messageIds[0].GetProperty("timestamp").GetDateTimeOffset().Should().Be(timestamp);
    }

    [Fact(DisplayName = "Constructor with null base path throws ArgumentException")]
    public void Constructor_NullBasePath_ThrowsArgumentException()
    {
        var act = () => new JsonFileStateRepository(null!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("basePath");
    }

    [Fact(DisplayName = "Constructor with empty base path throws ArgumentException")]
    public void Constructor_EmptyBasePath_ThrowsArgumentException()
    {
        var act = () => new JsonFileStateRepository(string.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("basePath");
    }

    [Fact(DisplayName = "Constructor with whitespace base path throws ArgumentException")]
    public void Constructor_WhitespaceBasePath_ThrowsArgumentException()
    {
        var act = () => new JsonFileStateRepository("   ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("basePath");
    }

    [Fact(DisplayName = "GetCheckpointAsync with null document ID throws ArgumentException")]
    public async Task GetCheckpointAsync_NullDocumentId_ThrowsArgumentException()
    {
        var act = async () => await _repository.GetCheckpointAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "GetCheckpointAsync with empty document ID throws ArgumentException")]
    public async Task GetCheckpointAsync_EmptyDocumentId_ThrowsArgumentException()
    {
        var act = async () => await _repository.GetCheckpointAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "SaveCheckpointAsync with null checkpoint throws ArgumentNullException")]
    public async Task SaveCheckpointAsync_NullCheckpoint_ThrowsArgumentNullException()
    {
        var act = async () => await _repository.SaveCheckpointAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("checkpoint");
    }

    [Fact(DisplayName = "GetCheckpointAsync with sender filter loads filtered checkpoint")]
    public async Task GetCheckpointAsync_WithSenderFilter_LoadsFilteredCheckpoint()
    {
        var senderFilter = new SenderFilter("Alice");
        var checkpoint = ProcessingCheckpoint.Create("sender-test", senderFilter);
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Hello from Alice");
        checkpoint.MarkAsProcessed(messageId);
        await _repository.SaveCheckpointAsync(checkpoint);

        var loadedCheckpoint = await _repository.GetCheckpointAsync("sender-test", senderFilter);

        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.SenderFilter.Should().NotBeNull();
        loadedCheckpoint.SenderFilter!.SenderName.Should().Be("Alice");
        loadedCheckpoint.HasBeenProcessed(messageId).Should().BeTrue();
    }

    [Fact(DisplayName = "SaveCheckpointAsync creates directory if not exists")]
    public async Task SaveCheckpointAsync_DirectoryNotExists_CreatesDirectory()
    {
        var nestedPath = Path.Combine(_testDirectory, "nested", "path");
        var nestedRepository = new JsonFileStateRepository(nestedPath);
        var checkpoint = ProcessingCheckpoint.Create("nested-doc");

        await nestedRepository.SaveCheckpointAsync(checkpoint);

        Directory.Exists(nestedPath).Should().BeTrue();
        File.Exists(Path.Combine(nestedPath, "nested-doc.json")).Should().BeTrue();
    }

    [Fact(DisplayName = "GetCheckpointAsync preserves LastProcessedTimestamp")]
    public async Task GetCheckpointAsync_ExistingFile_PreservesLastProcessedTimestamp()
    {
        var originalCheckpoint = ProcessingCheckpoint.Create("timestamp-test");
        var timestamp = DateTimeOffset.Parse("2024-06-15T14:30:00+00:00");
        var messageId = MessageId.Create(timestamp, "Test message");
        originalCheckpoint.MarkAsProcessed(messageId);
        await _repository.SaveCheckpointAsync(originalCheckpoint);

        var loadedCheckpoint = await _repository.GetCheckpointAsync("timestamp-test");

        loadedCheckpoint.LastProcessedTimestamp.Should().Be(timestamp);
    }

    [Fact(DisplayName = "SaveCheckpointAsync overwrites existing file")]
    public async Task SaveCheckpointAsync_ExistingFile_OverwritesFile()
    {
        var checkpoint = ProcessingCheckpoint.Create("overwrite-test");
        var messageId1 = MessageId.Create(DateTimeOffset.UtcNow, "Message 1");
        checkpoint.MarkAsProcessed(messageId1);
        await _repository.SaveCheckpointAsync(checkpoint);

        var messageId2 = MessageId.Create(DateTimeOffset.UtcNow.AddMinutes(1), "Message 2");
        checkpoint.MarkAsProcessed(messageId2);
        await _repository.SaveCheckpointAsync(checkpoint);

        var loadedCheckpoint = await _repository.GetCheckpointAsync("overwrite-test");
        loadedCheckpoint.ProcessedCount.Should().Be(2);
    }

    [Fact(DisplayName = "GetCheckpointAsync with different sender filters uses different files")]
    public async Task GetCheckpointAsync_DifferentSenderFilters_UsesDifferentFiles()
    {
        var aliceFilter = new SenderFilter("Alice");
        var bobFilter = new SenderFilter("Bob");

        var aliceCheckpoint = ProcessingCheckpoint.Create("multi-sender", aliceFilter);
        var bobCheckpoint = ProcessingCheckpoint.Create("multi-sender", bobFilter);

        aliceCheckpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "Alice message"));
        bobCheckpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "Bob message 1"));
        bobCheckpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow.AddSeconds(1), "Bob message 2"));

        await _repository.SaveCheckpointAsync(aliceCheckpoint);
        await _repository.SaveCheckpointAsync(bobCheckpoint);

        var loadedAlice = await _repository.GetCheckpointAsync("multi-sender", aliceFilter);
        var loadedBob = await _repository.GetCheckpointAsync("multi-sender", bobFilter);

        loadedAlice.ProcessedCount.Should().Be(1);
        loadedBob.ProcessedCount.Should().Be(2);
    }

    [Fact(DisplayName = "SaveCheckpointAsync with invalid filename characters sanitizes correctly")]
    public async Task SaveCheckpointAsync_InvalidFilenameCharacters_SanitizesCorrectly()
    {
        var senderFilter = new SenderFilter("User/With Spaces");
        var checkpoint = ProcessingCheckpoint.Create("doc/with/slashes", senderFilter);

        await _repository.SaveCheckpointAsync(checkpoint);

        var files = Directory.GetFiles(_testDirectory, "doc_with_slashes_*.json");
        files.Should().HaveCount(1);
        
        var loadedCheckpoint = await _repository.GetCheckpointAsync("doc/with/slashes", senderFilter);
        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.DocumentId.Should().Be("doc/with/slashes");
        loadedCheckpoint.SenderFilter.Should().NotBeNull();
        loadedCheckpoint.SenderFilter!.SenderName.Should().Be("User/With Spaces");
    }

    [Fact(DisplayName = "SaveCheckpointAsync with very long document ID truncates and hashes filename")]
    public async Task SaveCheckpointAsync_VeryLongDocumentId_TruncatesAndHashesFilename()
    {
        var longDocumentId = new string('a', 200);
        var checkpoint = ProcessingCheckpoint.Create(longDocumentId);

        await _repository.SaveCheckpointAsync(checkpoint);

        var files = Directory.GetFiles(_testDirectory, "*.json");
        files.Should().HaveCount(1);

        var fileName = Path.GetFileName(files[0]);
        fileName.Length.Should().BeLessThanOrEqualTo(110, "filename should be truncated to stay within limits");
        
        var loadedCheckpoint = await _repository.GetCheckpointAsync(longDocumentId);
        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.DocumentId.Should().Be(longDocumentId);
    }

    [Fact(DisplayName = "SaveCheckpointAsync with very long sender name truncates and hashes filename")]
    public async Task SaveCheckpointAsync_VeryLongSenderName_TruncatesAndHashesFilename()
    {
        var longSenderName = new string('b', 200);
        var senderFilter = new SenderFilter(longSenderName);
        var checkpoint = ProcessingCheckpoint.Create("normal-doc", senderFilter);

        await _repository.SaveCheckpointAsync(checkpoint);

        var files = Directory.GetFiles(_testDirectory, "normal-doc_*.json");
        files.Should().HaveCount(1);

        var fileName = Path.GetFileName(files[0]);
        fileName.Length.Should().BeLessThanOrEqualTo(220, "combined filename should stay within reasonable limits");
        
        var loadedCheckpoint = await _repository.GetCheckpointAsync("normal-doc", senderFilter);
        loadedCheckpoint.Should().NotBeNull();
        loadedCheckpoint.SenderFilter.Should().NotBeNull();
        loadedCheckpoint.SenderFilter!.SenderName.Should().Be(longSenderName);
    }

    [Fact(DisplayName = "SaveCheckpointAsync with different long document IDs creates unique files")]
    public async Task SaveCheckpointAsync_DifferentLongDocumentIds_CreatesUniqueFiles()
    {
        var longDocumentId1 = new string('a', 200) + "1";
        var longDocumentId2 = new string('a', 200) + "2";
        var checkpoint1 = ProcessingCheckpoint.Create(longDocumentId1);
        var checkpoint2 = ProcessingCheckpoint.Create(longDocumentId2);
        checkpoint1.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "Message 1"));
        checkpoint2.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "Message 2"));

        await _repository.SaveCheckpointAsync(checkpoint1);
        await _repository.SaveCheckpointAsync(checkpoint2);

        var files = Directory.GetFiles(_testDirectory, "*.json");
        files.Should().HaveCount(2, "different long document IDs should create unique files due to hash");
        
        var loadedCheckpoint1 = await _repository.GetCheckpointAsync(longDocumentId1);
        var loadedCheckpoint2 = await _repository.GetCheckpointAsync(longDocumentId2);
        
        loadedCheckpoint1.ProcessedCount.Should().Be(1);
        loadedCheckpoint2.ProcessedCount.Should().Be(1);
    }
}
