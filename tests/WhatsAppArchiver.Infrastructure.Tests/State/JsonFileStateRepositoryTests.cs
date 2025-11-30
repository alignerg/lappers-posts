using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;
using WhatsAppArchiver.Infrastructure.State;

namespace WhatsAppArchiver.Infrastructure.Tests.State;

public class JsonFileStateRepositoryTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _tempFiles = [];

    public JsonFileStateRepositoryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "StateRepoTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            File.Delete(file);
        }

        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateTestFilePath(string fileName = "state.json")
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        _tempFiles.Add(filePath);

        return filePath;
    }

    [Fact(DisplayName = "GetCheckpointAsync with non-existent file returns new checkpoint")]
    public async Task GetCheckpointAsync_NonExistentFile_ReturnsNewCheckpoint()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var documentId = "doc-123";

        var checkpoint = await repository.GetCheckpointAsync(documentId);

        Assert.NotNull(checkpoint);
        Assert.Equal(documentId, checkpoint.DocumentId);
        Assert.Null(checkpoint.LastProcessedTimestamp);
        Assert.Empty(checkpoint.ProcessedMessageIds);
    }

    [Fact(DisplayName = "SaveCheckpointAsync creates file with checkpoint data")]
    public async Task SaveCheckpointAsync_NewCheckpoint_CreatesFile()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Test message");
        checkpoint.MarkAsProcessed(messageId);

        await repository.SaveCheckpointAsync(checkpoint);

        Assert.True(File.Exists(filePath));
    }

    [Fact(DisplayName = "GetCheckpointAsync after save returns persisted checkpoint")]
    public async Task GetCheckpointAsync_AfterSave_ReturnsPersistedCheckpoint()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var documentId = "doc-123";
        var checkpoint = ProcessingCheckpoint.Create(documentId);
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Test message");
        checkpoint.MarkAsProcessed(messageId);
        await repository.SaveCheckpointAsync(checkpoint);

        var loadedCheckpoint = await repository.GetCheckpointAsync(documentId);

        Assert.Equal(checkpoint.Id, loadedCheckpoint.Id);
        Assert.Equal(checkpoint.DocumentId, loadedCheckpoint.DocumentId);
        Assert.NotNull(loadedCheckpoint.LastProcessedTimestamp);
        Assert.Single(loadedCheckpoint.ProcessedMessageIds);
    }

    [Fact(DisplayName = "SaveCheckpointAsync with sender filter persists filter")]
    public async Task SaveCheckpointAsync_WithSenderFilter_PersistsFilter()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var documentId = "doc-123";
        var senderFilter = SenderFilter.Create("John");
        var checkpoint = ProcessingCheckpoint.Create(documentId, senderFilter);
        await repository.SaveCheckpointAsync(checkpoint);

        var loadedCheckpoint = await repository.GetCheckpointAsync(documentId, senderFilter);

        Assert.NotNull(loadedCheckpoint.SenderFilter);
        Assert.Equal("John", loadedCheckpoint.SenderFilter.SenderName);
    }

    [Fact(DisplayName = "GetCheckpointAsync with different sender filters returns different checkpoints")]
    public async Task GetCheckpointAsync_DifferentSenderFilters_ReturnsDifferentCheckpoints()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var documentId = "doc-123";
        var johnFilter = SenderFilter.Create("John");
        var janeFilter = SenderFilter.Create("Jane");

        var johnCheckpoint = ProcessingCheckpoint.Create(documentId, johnFilter);
        johnCheckpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "John's message"));
        await repository.SaveCheckpointAsync(johnCheckpoint);

        var janeCheckpoint = ProcessingCheckpoint.Create(documentId, janeFilter);
        janeCheckpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "Jane's message 1"));
        janeCheckpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "Jane's message 2"));
        await repository.SaveCheckpointAsync(janeCheckpoint);

        var loadedJohnCheckpoint = await repository.GetCheckpointAsync(documentId, johnFilter);
        var loadedJaneCheckpoint = await repository.GetCheckpointAsync(documentId, janeFilter);

        Assert.Single(loadedJohnCheckpoint.ProcessedMessageIds);
        Assert.Equal(2, loadedJaneCheckpoint.ProcessedMessageIds.Count);
    }

    [Fact(DisplayName = "SaveCheckpointAsync with null checkpoint throws ArgumentNullException")]
    public async Task SaveCheckpointAsync_NullCheckpoint_ThrowsArgumentNullException()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.SaveCheckpointAsync(null!));
    }

    [Fact(DisplayName = "GetCheckpointAsync with null document ID throws ArgumentException")]
    public async Task GetCheckpointAsync_NullDocumentId_ThrowsArgumentException()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetCheckpointAsync(null!));

        Assert.Equal("documentId", exception.ParamName);
    }

    [Fact(DisplayName = "GetCheckpointAsync with whitespace document ID throws ArgumentException")]
    public async Task GetCheckpointAsync_WhitespaceDocumentId_ThrowsArgumentException()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetCheckpointAsync("   "));

        Assert.Equal("documentId", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with null file path throws ArgumentException")]
    public void Constructor_NullFilePath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new JsonFileStateRepository(null!));

        Assert.Equal("filePath", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with whitespace file path throws ArgumentException")]
    public void Constructor_WhitespaceFilePath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new JsonFileStateRepository("   "));

        Assert.Equal("filePath", exception.ParamName);
    }

    [Fact(DisplayName = "SaveCheckpointAsync updates existing checkpoint")]
    public async Task SaveCheckpointAsync_ExistingCheckpoint_UpdatesData()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var documentId = "doc-123";
        var checkpoint = ProcessingCheckpoint.Create(documentId);

        var messageId1 = MessageId.Create(DateTimeOffset.UtcNow, "First message");
        checkpoint.MarkAsProcessed(messageId1);
        await repository.SaveCheckpointAsync(checkpoint);

        var messageId2 = MessageId.Create(DateTimeOffset.UtcNow.AddMinutes(1), "Second message");
        checkpoint.MarkAsProcessed(messageId2);
        await repository.SaveCheckpointAsync(checkpoint);

        var loadedCheckpoint = await repository.GetCheckpointAsync(documentId);

        Assert.Equal(2, loadedCheckpoint.ProcessedMessageIds.Count);
    }

    [Fact(DisplayName = "GetCheckpointAsync persists timestamp correctly")]
    public async Task GetCheckpointAsync_AfterSave_PreservesTimestamp()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var documentId = "doc-123";
        var timestamp = new DateTimeOffset(2024, 12, 25, 14, 30, 0, TimeSpan.Zero);
        var checkpoint = ProcessingCheckpoint.Create(documentId);
        var messageId = MessageId.Create(timestamp, "Christmas message");
        checkpoint.MarkAsProcessed(messageId);
        await repository.SaveCheckpointAsync(checkpoint);

        var loadedCheckpoint = await repository.GetCheckpointAsync(documentId);

        Assert.NotNull(loadedCheckpoint.LastProcessedTimestamp);
        Assert.Equal(timestamp, loadedCheckpoint.LastProcessedTimestamp);
    }

    [Fact(DisplayName = "SaveCheckpointAsync creates directory if not exists")]
    public async Task SaveCheckpointAsync_NonExistentDirectory_CreatesDirectory()
    {
        var nestedPath = Path.Combine(_testDirectory, "nested", "path", "state.json");
        _tempFiles.Add(nestedPath);
        var repository = new JsonFileStateRepository(nestedPath);
        var checkpoint = ProcessingCheckpoint.Create("doc-123");

        await repository.SaveCheckpointAsync(checkpoint);

        Assert.True(File.Exists(nestedPath));
    }

    [Fact(DisplayName = "Multiple repositories can read same file")]
    public async Task MultipleRepositories_SameFile_ReadCorrectly()
    {
        var filePath = CreateTestFilePath();
        var repository1 = new JsonFileStateRepository(filePath);
        var documentId = "doc-123";
        var checkpoint = ProcessingCheckpoint.Create(documentId);
        checkpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "Test"));
        await repository1.SaveCheckpointAsync(checkpoint);

        var repository2 = new JsonFileStateRepository(filePath);
        var loadedCheckpoint = await repository2.GetCheckpointAsync(documentId);

        Assert.Equal(checkpoint.Id, loadedCheckpoint.Id);
        Assert.Single(loadedCheckpoint.ProcessedMessageIds);
    }

    [Fact(DisplayName = "SaveCheckpointAsync respects cancellation token")]
    public async Task SaveCheckpointAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.SaveCheckpointAsync(checkpoint, cts.Token));
    }

    [Fact(DisplayName = "GetCheckpointAsync respects cancellation token")]
    public async Task GetCheckpointAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var filePath = CreateTestFilePath();
        var repository = new JsonFileStateRepository(filePath);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetCheckpointAsync("doc-123", cancellationToken: cts.Token));
    }
}
