# Plan: Add Markdown Document Formatter for Google Docs

Extend the WhatsApp Archiver formatter system with a new `IDocumentFormatter` interface for batch document formatters and implement `MarkdownDocumentFormatter` that produces structured markdown with friendly date headers, timestamped posts, and horizontal rule separators per the specification. The plan is organized into parallel work streams and sequential dependencies.

## Steps

### Phase 1: Core Interfaces and Enum (Foundation - Sequential)

**1.1 Create `IDocumentFormatter` interface**
- **File**: [`src/WhatsAppArchiver.Domain/Formatting/IDocumentFormatter.cs`](src/WhatsAppArchiver.Domain/Formatting/IDocumentFormatter.cs)
- **Work**: Create new interface extending `IMessageFormatter`
- **Details**:
  - Add method signature: `string FormatDocument(ChatExport chatExport)`
  - Inherit `FormatMessage(ChatMessage)` from `IMessageFormatter` base
  - Add XML documentation explaining:
    - Document-level formatters process entire `ChatExport` aggregate at once
    - Message-level formatters process one message at a time
    - `FormatMessage` should throw `NotSupportedException` for document formatters
  - Include usage examples in XML docs
- **Dependencies**: None
- **Estimated Lines**: ~30 lines

**1.2 Add `MarkdownDocument` enum value**
- **File**: [`src/WhatsAppArchiver.Domain/Formatting/MessageFormatType.cs`](src/WhatsAppArchiver.Domain/Formatting/MessageFormatType.cs)
- **Work**: Add new enum member
- **Details**:
  - Add `MarkdownDocument = 3` to existing enum
  - Update XML documentation: "Structured markdown document with friendly date headers (MMMM d, yyyy) and individual timestamped posts. Requires IDocumentFormatter for batch processing."
  - No breaking changes to existing enum values (Default=0, Compact=1, Verbose=2)
- **Dependencies**: None
- **Estimated Lines**: ~5 lines

---

### Phase 2: Implementation (Parallel Workstreams)

**Workstream A: Formatter Implementation**

**2A.1 Implement `MarkdownDocumentFormatter` class**
- **File**: [`src/WhatsAppArchiver.Domain/Formatting/MarkdownDocumentFormatter.cs`](src/WhatsAppArchiver.Domain/Formatting/MarkdownDocumentFormatter.cs)
- **Work**: Create full formatter implementation
- **Details**:
  - Implement `IDocumentFormatter` interface
  - `FormatDocument(ChatExport chatExport)` method:
    - Extract sender name from `chatExport.Messages[0].Sender` (first message)
    - Build H1 title: `# WhatsApp Conversation Export - {senderName}`
    - Add metadata lines:
      - `**Export Date:** {DateTime.Now:MMMM d, yyyy}`
      - `**Total Messages:** {chatExport.MessageCount}`
    - Add horizontal rule separator: `---`
    - Group messages: `chatExport.Messages.GroupBy(m => m.Timestamp.Date).OrderBy(g => g.Key)`
    - For each date group:
      - Add H2 header: `## {date:MMMM d, yyyy}`
      - For each message in group:
        - Add bold timestamp: `**{message.Timestamp:HH:mm}**` (24-hour format)
        - Add message content: `{message.Content}` (preserve line breaks)
        - Add separator: blank line + `---`
  - `FormatMessage(ChatMessage)` method:
    - Throw `NotSupportedException("MarkdownDocumentFormatter requires FormatDocument method for batch processing. Use IDocumentFormatter.FormatDocument instead.")`
  - Handle empty exports: return header with "Total Messages: 0" and no date sections
  - Handle multi-line content: preserve original line breaks in `message.Content`
  - Add XML documentation with usage examples
- **Dependencies**: Phase 1 complete
- **Estimated Lines**: ~80-100 lines

**Workstream B: Factory Update**

**2B.1 Update `FormatterFactory` class**
- **File**: [`src/WhatsAppArchiver.Domain/Formatting/FormatterFactory.cs`](src/WhatsAppArchiver.Domain/Formatting/FormatterFactory.cs)
- **Work**: Add new case and helper method
- **Details**:
  - Add case to `Create(MessageFormatType)` switch expression:
    ```csharp
    MessageFormatType.MarkdownDocument => new MarkdownDocumentFormatter(),
    ```
  - Add static helper method:
    ```csharp
    public static bool IsDocumentFormatter(MessageFormatType formatType)
        => formatType == MessageFormatType.MarkdownDocument;
    ```
  - Update XML documentation to reference document vs message formatters
  - No changes to existing cases (Default, Compact, Verbose)
- **Dependencies**: Phase 1 complete, 2A.1 complete
- **Estimated Lines**: ~10 lines

---

### Phase 3: Tests (Parallel Workstreams)

**Workstream C: Formatter Tests**

**3C.1 Create `MarkdownDocumentFormatterTests` class**
- **File**: [`tests/WhatsAppArchiver.Domain.Tests/Formatting/MarkdownDocumentFormatterTests.cs`](tests/WhatsAppArchiver.Domain.Tests/Formatting/MarkdownDocumentFormatterTests.cs)
- **Work**: Comprehensive test suite (8 tests)
- **Details**:
  - Test 1: `FormatDocument_SingleDayMessages_ProducesCorrectStructure`
    - Create `ChatExport` with 3 messages on same date
    - Assert H1 contains sender name from first message
    - Assert metadata contains export date and count (3)
    - Assert single H2 date header with "MMMM d, yyyy" format
    - Assert 3 timestamp blocks with `**HH:mm**` format
    - Assert 3 horizontal rule separators
  - Test 2: `FormatDocument_MultipleDays_GroupsByDateChronologically`
    - Create messages spanning 3 dates (not in order)
    - Assert 3 H2 sections sorted chronologically
    - Assert messages grouped under correct date headers
  - Test 3: `FormatDocument_MultiLineMessage_PreservesLineBreaks`
    - Create message with `\n` in content (3 paragraphs)
    - Assert all 3 paragraphs present in output
    - Assert line breaks preserved within single post
  - Test 4: `FormatDocument_EmptyExport_ReturnsHeaderOnly`
    - Create `ChatExport` with empty message collection
    - Assert H1 and metadata present
    - Assert "Total Messages: 0"
    - Assert no H2 sections (no dates)
  - Test 5: `FormatDocument_SpecialCharacters_DoesNotEscapeMarkdown`
    - Create message with `*`, `_`, `#`, `-` characters
    - Assert characters preserved (WhatsApp bold/italic formatting)
  - Test 6: `FormatDocument_DateFormat_UsesFriendlyFormat`
    - Create message with known date (e.g., Dec 8, 2024)
    - Assert H2 contains "December 8, 2024" (not "12/8/2024")
  - Test 7: `FormatDocument_TimeFormat_Uses24HourFormat`
    - Create messages at various times (1 AM, 1 PM, 23:59)
    - Assert timestamps formatted as "01:00", "13:00", "23:59"
  - Test 8: `FormatMessage_Called_ThrowsNotSupportedException`
    - Create formatter and message
    - Assert `FormatMessage(message)` throws `NotSupportedException`
    - Assert exception message mentions "FormatDocument"
- **Dependencies**: 2A.1 complete
- **Estimated Lines**: ~200-250 lines

**Workstream D: Factory Tests**

**3D.1 Update `FormatterFactoryTests` class**
- **File**: [`tests/WhatsAppArchiver.Domain.Tests/Formatting/FormatterFactoryTests.cs`](tests/WhatsAppArchiver.Domain.Tests/Formatting/FormatterFactoryTests.cs)
- **Work**: Add 3 new tests
- **Details**:
  - Test 1: `CreateFormatter_MarkdownDocumentType_ReturnsMarkdownDocumentFormatter`
    - Call `FormatterFactory.Create(MessageFormatType.MarkdownDocument)`
    - Assert result is `MarkdownDocumentFormatter`
    - Assert result implements `IDocumentFormatter`
  - Test 2: `IsDocumentFormatter_MarkdownDocument_ReturnsTrue`
    - Call `FormatterFactory.IsDocumentFormatter(MessageFormatType.MarkdownDocument)`
    - Assert returns `true`
  - Test 3: `IsDocumentFormatter_OtherTypes_ReturnsFalse` (parameterized)
    - Use `[Theory]` with `[InlineData(MessageFormatType.Default)]`, `[InlineData(MessageFormatType.Compact)]`, `[InlineData(MessageFormatType.Verbose)]`
    - Assert each returns `false`
- **Dependencies**: 2B.1 complete
- **Estimated Lines**: ~40 lines

---

### Phase 4: Application Layer Integration (Sequential)

**4.1 Refactor `UploadToGoogleDocsCommandHandler`**
- **File**: [`src/WhatsAppArchiver.Application/Handlers/UploadToGoogleDocsCommandHandler.cs`](src/WhatsAppArchiver.Application/Handlers/UploadToGoogleDocsCommandHandler.cs)
- **Work**: Update handler to support document formatters
- **Details**:
  - Modify `HandleAsync` method at line ~101:
    - Create formatter: `var formatter = FormatterFactory.Create(command.FormatterType);`
    - Add conditional logic after filtering unprocessed messages:
      ```csharp
      string content;
      if (formatter is IDocumentFormatter documentFormatter)
      {
          // Document-level formatting: process entire export at once
          var exportToFormat = ChatExport.Create(unprocessedMessages, chatExport.Metadata);
          content = documentFormatter.FormatDocument(exportToFormat);
      }
      else
      {
          // Message-level formatting: process messages individually
          content = FormatMessages(unprocessedMessages, formatter);
      }
      ```
    - Replace existing `FormatMessages` call with conditional logic above
    - Keep existing `AppendAsync` and checkpoint update logic unchanged
  - Add XML documentation comment explaining both formatting paths
  - Keep `FormatMessages` private method for message-level formatters
- **Dependencies**: Phase 2 and 3 complete
- **Estimated Lines**: ~15 lines modified/added

---

### Phase 5: CLI and Validation (Parallel Workstreams)

**Workstream E: Console CLI Update**

**5E.1 Update CLI help text**
- **File**: [`src/WhatsAppArchiver.Console/Program.cs`](src/WhatsAppArchiver.Console/Program.cs)
- **Work**: Update format option description
- **Details**:
  - Modify `--format` option description at line ~82:
    - Change description to: `"Message format type (default|compact|verbose|markdowndocument)"`
    - Add explanation: `"markdowndocument: Structured markdown with friendly date headers and timestamped posts"`
  - Update help examples if present to demonstrate `--format markdowndocument`
  - No code changes needed (enum parsing automatic via System.CommandLine)
- **Dependencies**: Phase 1 complete
- **Estimated Lines**: ~3 lines

**Workstream F: Validation Check**

**5F.1 Verify validator includes new enum**
- **File**: [`src/WhatsAppArchiver.Application/Validators/UploadToGoogleDocsCommandValidator.cs`](src/WhatsAppArchiver.Application/Validators/UploadToGoogleDocsCommandValidator.cs)
- **Work**: Verify existing validation covers new enum value
- **Details**:
  - Read validator to confirm `FormatterType` validation uses `Enum.IsDefined` or similar pattern (should automatically include `MarkdownDocument = 3`)
  - If hardcoded value list exists, add `MessageFormatType.MarkdownDocument` to list
  - If already using dynamic enum validation, no changes needed
  - Add test case if validator has test file
- **Dependencies**: Phase 1 complete
- **Estimated Lines**: 0-5 lines (likely no changes needed)

---

## Parallel Execution Summary

**Phase 1** (Sequential): Complete steps 1.1 and 1.2 in order
**Phase 2** (Parallel): Execute 2A.1 and 2B.1 simultaneously (2B.1 waits for 2A.1 completion before final integration)
**Phase 3** (Parallel): Execute 3C.1 and 3D.1 simultaneously after Phase 2 complete
**Phase 4** (Sequential): Execute 4.1 after Phases 2-3 complete
**Phase 5** (Parallel): Execute 5E.1 and 5F.1 simultaneously after Phase 1 complete

**Total Estimated Lines of Code**: ~400-450 lines across all files

## Further Considerations

1. **Empty ChatExport Handling** - When `ChatExport.Messages` is empty, sender name cannot be extracted. Current plan: Use `"Unknown User"` as placeholder in H1 title. Alternative: Throw `InvalidOperationException` requiring non-empty exports for document formatting. **Recommend**: Placeholder approach for graceful degradation.

2. **Time Format Detection** - Current plan uses hardcoded 24-hour `HH:mm` format for timestamps. This doesn't preserve original WhatsApp export format (12-hour vs 24-hour). The parser stores `DateTimeOffset` which loses original format string. **Recommend**: Accept this limitation and use consistent 24-hour format per specification example, or add optional `OriginalTimeFormat` property to `ChatMessage` in future enhancement.

3. **Horizontal Rule Spacing** - Specification shows blank line before `---` separator but not after. Current plan: Add `\n---\n` after each message (blank line before, newline after). This matches the specification example exactly.
