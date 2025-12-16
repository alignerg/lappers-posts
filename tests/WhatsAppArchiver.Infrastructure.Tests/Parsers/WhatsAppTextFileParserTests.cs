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
        _sampleDataPath = TestFileUtils.GetSampleDataPath();
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

        // The sample-edge-cases.txt file contains 28 lines in total, including continuation lines.
        // Note: TotalLines counts all lines (including continuations), while ParsedMessageCount
        // counts successful messages and FailedLineCount counts lines that couldn't be parsed
        // as new messages (excluding continuation lines which are attached to previous messages).
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
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
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

    [Fact(DisplayName = "ParseAsync with timezone offset applies correct offset to timestamps")]
    public async Task ParseAsync_WithTimezoneOffset_AppliesCorrectOffset()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");
        var offset = TimeSpan.FromHours(5); // UTC+5

        var result = await _parser.ParseAsync(filePath, offset);

        result.Should().NotBeNull();
        result.Messages.Should().NotBeEmpty();

        // Verify that the offset is applied correctly
        var firstMessage = result.Messages.First();
        firstMessage.Timestamp.Offset.Should().Be(offset);
    }

    [Fact(DisplayName = "ParseAsync without timezone offset uses UTC")]
    public async Task ParseAsync_WithoutTimezoneOffset_UsesUtc()
    {
        var filePath = Path.Combine(_sampleDataPath, "sample-dd-mm-yyyy.txt");

        var result = await _parser.ParseAsync(filePath);

        result.Should().NotBeNull();
        result.Messages.Should().NotBeEmpty();

        // Verify that UTC (zero offset) is used by default
        var firstMessage = result.Messages.First();
        firstMessage.Timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "ParseAsync retries on transient IOException and succeeds")]
    public async Task ParseAsync_TransientIOException_RetriesAndSucceeds()
    {
        var callCount = 0;
        var testLines = new[] { "[25/12/2024, 09:15:00] John Smith: Hello!" };

        // Custom file reader that fails twice then succeeds
        Task<string[]> FileReaderWithTransientFailure(string path, CancellationToken ct)
        {
            callCount++;
            if (callCount < 3)
            {
                throw new IOException("Transient I/O error");
            }
            return Task.FromResult(testLines);
        }

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            FileReaderWithTransientFailure);

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(1);
        callCount.Should().Be(3, "Should have retried twice after initial failure");
    }

    [Fact(DisplayName = "ParseAsync throws after max retries exceeded")]
    public async Task ParseAsync_MaxRetriesExceeded_ThrowsIOException()
    {
        var callCount = 0;

        // Custom file reader that always fails
        Task<string[]> FileReaderThatAlwaysFails(string path, CancellationToken ct)
        {
            callCount++;
            throw new IOException("Persistent I/O error");
        }

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            FileReaderThatAlwaysFails);

        Func<Task> act = () => parser.ParseAsync("test.txt");

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("Persistent I/O error");

        // Initial attempt + 3 retries = 4 total attempts
        callCount.Should().Be(4, "Should have made initial attempt plus 3 retries");
    }

    [Fact(DisplayName = "ParseAsync filters out link-only messages with HTTP protocol")]
    public async Task ParseAsync_LinkOnlyMessagesWithHttp_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Hello everyone!",
            "[25/12/2024, 09:16:00] John Smith: http://example.com",
            "[25/12/2024, 09:17:00] John Smith: Thanks for sharing!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages.Should().NotContain(m => m.Content.Contains("http://example.com"));
        result.Messages[0].Content.Should().Be("Hello everyone!");
        result.Messages[1].Content.Should().Be("Thanks for sharing!");
    }

    [Fact(DisplayName = "ParseAsync filters out link-only messages with HTTPS protocol")]
    public async Task ParseAsync_LinkOnlyMessagesWithHttps_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Check this out",
            "[25/12/2024, 09:16:00] John Smith: https://www.youtube.com/watch?v=xyz",
            "[25/12/2024, 09:17:00] John Smith: Cool video!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages.Should().NotContain(m => m.Content.Contains("https://www.youtube.com"));
        result.Messages[0].Content.Should().Be("Check this out");
        result.Messages[1].Content.Should().Be("Cool video!");
    }

    [Fact(DisplayName = "ParseAsync does not filter messages with links and text")]
    public async Task ParseAsync_MessagesWithLinksAndText_DoesNotFilter()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Check out this article: https://example.com/article",
            "[25/12/2024, 09:16:00] John Smith: I found this link http://test.com which is interesting"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Check out this article: https://example.com/article");
        result.Messages[1].Content.Should().Be("I found this link http://test.com which is interesting");
    }

    [Fact(DisplayName = "ParseAsync filters link-only messages with URL fragments")]
    public async Task ParseAsync_LinkOnlyMessagesWithFragments_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Here's a link",
            "[25/12/2024, 09:16:00] John Smith: https://example.com/page#section",
            "[25/12/2024, 09:17:00] John Smith: Did you check it?"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Here's a link");
        result.Messages[1].Content.Should().Be("Did you check it?");
    }

    [Fact(DisplayName = "ParseAsync filters link-only messages with encoded characters")]
    public async Task ParseAsync_LinkOnlyMessagesWithEncodedChars_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Sharing a search",
            "[25/12/2024, 09:16:00] John Smith: https://example.com?q=hello%20world&sort=date",
            "[25/12/2024, 09:17:00] John Smith: Useful results!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Sharing a search");
        result.Messages[1].Content.Should().Be("Useful results!");
    }

    [Fact(DisplayName = "ParseAsync filters very long link-only URLs")]
    public async Task ParseAsync_VeryLongLinkOnlyUrls_FiltersOut()
    {
        var longUrl = "https://example.com/very/long/path/with/many/segments/and/parameters?param1=value1&param2=value2&param3=value3&param4=value4";
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Check this",
            $"[25/12/2024, 09:16:00] John Smith: {longUrl}",
            "[25/12/2024, 09:17:00] John Smith: Thanks!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Check this");
        result.Messages[1].Content.Should().Be("Thanks!");
    }

    [Fact(DisplayName = "ParseAsync filters out media omitted messages")]
    public async Task ParseAsync_MediaOmittedMessages_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Hello!",
            "[25/12/2024, 09:16:00] John Smith: <Media omitted>",
            "[25/12/2024, 09:17:00] John Smith: Did you see that?"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages.Should().NotContain(m => m.Content.Contains("<Media omitted>"));
        result.Messages[0].Content.Should().Be("Hello!");
        result.Messages[1].Content.Should().Be("Did you see that?");
    }

    [Fact(DisplayName = "ParseAsync filters out image omitted messages")]
    public async Task ParseAsync_ImageOmittedMessages_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Look at this",
            "[25/12/2024, 09:16:00] John Smith: [image omitted]",
            "[25/12/2024, 09:17:00] John Smith: Nice picture!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages.Should().NotContain(m => m.Content.Contains("[image omitted]"));
        result.Messages[0].Content.Should().Be("Look at this");
        result.Messages[1].Content.Should().Be("Nice picture!");
    }

    [Fact(DisplayName = "ParseAsync filters out video omitted messages")]
    public async Task ParseAsync_VideoOmittedMessages_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Watch this",
            "[25/12/2024, 09:16:00] John Smith: [video omitted]",
            "[25/12/2024, 09:17:00] John Smith: Great video!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages.Should().NotContain(m => m.Content.Contains("[video omitted]"));
        result.Messages[0].Content.Should().Be("Watch this");
        result.Messages[1].Content.Should().Be("Great video!");
    }

    [Fact(DisplayName = "ParseAsync filters out audio omitted messages")]
    public async Task ParseAsync_AudioOmittedMessages_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Listen to this",
            "[25/12/2024, 09:16:00] John Smith: [audio omitted]",
            "[25/12/2024, 09:17:00] John Smith: Cool song!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages.Should().NotContain(m => m.Content.Contains("[audio omitted]"));
        result.Messages[0].Content.Should().Be("Listen to this");
        result.Messages[1].Content.Should().Be("Cool song!");
    }

    [Fact(DisplayName = "ParseAsync filters out multiple media types in same conversation")]
    public async Task ParseAsync_MultipleMediaTypes_FiltersAllOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Hello!",
            "[25/12/2024, 09:16:00] John Smith: <Media omitted>",
            "[25/12/2024, 09:17:00] John Smith: [image omitted]",
            "[25/12/2024, 09:18:00] John Smith: [video omitted]",
            "[25/12/2024, 09:19:00] John Smith: [audio omitted]",
            "[25/12/2024, 09:20:00] John Smith: That was a lot of media!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Hello!");
        result.Messages[1].Content.Should().Be("That was a lot of media!");
    }

    [Fact(DisplayName = "ParseAsync filters link-only and media messages together")]
    public async Task ParseAsync_LinkOnlyAndMediaMessages_FiltersBothOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Hello!",
            "[25/12/2024, 09:16:00] John Smith: https://example.com",
            "[25/12/2024, 09:17:00] John Smith: <Media omitted>",
            "[25/12/2024, 09:18:00] John Smith: Check this out: https://test.com with context",
            "[25/12/2024, 09:19:00] John Smith: Goodbye!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(3);
        result.Messages[0].Content.Should().Be("Hello!");
        result.Messages[1].Content.Should().Be("Check this out: https://test.com with context");
        result.Messages[2].Content.Should().Be("Goodbye!");
    }

    [Fact(DisplayName = "ParseAsync does not filter multi-line messages with links")]
    public async Task ParseAsync_MultiLineMessagesWithLinks_DoesNotFilter()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Here's an interesting article",
            "https://example.com/article",
            "that I wanted to share with you all"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Content.Should().Contain("Here's an interesting article");
        result.Messages[0].Content.Should().Contain("https://example.com/article");
        result.Messages[0].Content.Should().Contain("that I wanted to share with you all");
    }

    [Fact(DisplayName = "ParseAsync handles case insensitive media placeholders")]
    public async Task ParseAsync_CaseInsensitiveMediaPlaceholders_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Hello!",
            "[25/12/2024, 09:16:00] John Smith: <MEDIA OMITTED>",
            "[25/12/2024, 09:17:00] John Smith: [IMAGE OMITTED]",
            "[25/12/2024, 09:18:00] John Smith: [Video Omitted]",
            "[25/12/2024, 09:19:00] John Smith: Goodbye!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Hello!");
        result.Messages[1].Content.Should().Be("Goodbye!");
    }

    [Fact(DisplayName = "ParseAsync filtered messages do not count as failed lines")]
    public async Task ParseAsync_FilteredMessages_DoNotCountAsFailed()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Hello!",
            "[25/12/2024, 09:16:00] John Smith: https://example.com",
            "[25/12/2024, 09:17:00] John Smith: <Media omitted>",
            "[25/12/2024, 09:18:00] John Smith: [image omitted]",
            "[25/12/2024, 09:19:00] John Smith: Goodbye!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2, "only non-filtered messages should be in the result");
        result.Metadata.ParsedMessageCount.Should().Be(2, "only non-filtered messages count as parsed");
        result.Metadata.FailedLineCount.Should().Be(0, "filtered messages should not count as failed");
        result.Metadata.TotalLines.Should().Be(5, "all lines should be counted");
    }

    [Fact(DisplayName = "ParseAsync filters out attached messages with filenames")]
    public async Task ParseAsync_AttachedMessagesWithFilenames_FiltersOut()
    {
        var testLines = new[]
        {
            "[19/07/2025, 08:43:57] Rudi Anderson: Good morning!",
            "[19/07/2025, 08:43:57] Rudi Anderson: <attached: 00000387-PHOTO-2025-07-19-08-43-56.jpg>",
            "[20/07/2025, 05:51:42] Rudi Anderson: <attached: 00000394-PHOTO-2025-07-20-05-51-42.jpg>",
            "[23/07/2025, 04:53:29] Rudi Anderson: <attached: 00000411-PHOTO-2025-07-23-04-53-29.jpg>",
            "[23/07/2025, 20:10:43] Rudi Anderson: <attached: 00000412-PHOTO-2025-07-23-20-10-43.jpg>",
            "[23/07/2025, 20:11:00] Rudi Anderson: That's all for now!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Good morning!");
        result.Messages[1].Content.Should().Be("That's all for now!");
        result.Messages.Should().NotContain(m => m.Content.Contains("<attached:"));
    }

    [Fact(DisplayName = "ParseAsync filters out various attachment file types")]
    public async Task ParseAsync_VariousAttachmentFileTypes_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Check this out!",
            "[25/12/2024, 09:16:00] John Smith: <attached: document.pdf>",
            "[25/12/2024, 09:17:00] John Smith: <attached: video-2024.mp4>",
            "[25/12/2024, 09:18:00] John Smith: <attached: audio-note.opus>",
            "[25/12/2024, 09:19:00] John Smith: <attached: IMG_1234.jpg>",
            "[25/12/2024, 09:20:00] John Smith: All sent!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Check this out!");
        result.Messages[1].Content.Should().Be("All sent!");
    }

    [Fact(DisplayName = "ParseAsync handles case insensitive attached pattern")]
    public async Task ParseAsync_CaseInsensitiveAttachedPattern_FiltersOut()
    {
        var testLines = new[]
        {
            "[25/12/2024, 09:15:00] John Smith: Hello!",
            "[25/12/2024, 09:16:00] John Smith: <ATTACHED: FILE.JPG>",
            "[25/12/2024, 09:17:00] John Smith: <Attached: MyPhoto.png>",
            "[25/12/2024, 09:18:00] John Smith: Goodbye!"
        };

        var parser = new WhatsAppTextFileParser(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppTextFileParser>.Instance,
            (path, ct) => Task.FromResult(testLines));

        var result = await parser.ParseAsync("test.txt");

        result.Should().NotBeNull();
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Content.Should().Be("Hello!");
        result.Messages[1].Content.Should().Be("Goodbye!");
    }
}
