# Plan: Replace Markdown Formatter with Google Docs Rich Formatter

Replace the MarkdownDocumentFormatter with a new GoogleDocsDocumentFormatter that generates structured formatting instructions for rich text styling in Google Docs. The formatter will produce a hierarchical document structure with style metadata, while the GoogleDocsServiceAccountAdapter will translate this into batch API requests with proper text and paragraph styling (bold timestamps, H1/H2 headings, horizontal rules).

## Steps

### Phase 1: Core Abstraction Layer (Foundation - Sequential)

**1.1 Create GoogleDocsDocument model and IGoogleDocsFormatter interface**
- **Files**: [`src/WhatsAppArchiver.Domain/Formatting/GoogleDocsDocument.cs`](src/WhatsAppArchiver.Domain/Formatting/GoogleDocsDocument.cs), [`src/WhatsAppArchiver.Domain/Formatting/IGoogleDocsFormatter.cs`](src/WhatsAppArchiver.Domain/Formatting/IGoogleDocsFormatter.cs)
- **Work**: Create structured document model and formatter interface
- **Details**:
  - `GoogleDocsDocument` class with:
    - `List<DocumentSection> Sections { get; }` - ordered list of content sections
    - `Add(DocumentSection section)` - append section to document
    - `ToPlainText()` - debug/logging method to generate plain text representation
  - `DocumentSection` abstract base class with `string Content` property
  - Concrete section types:
    - `HeadingSection` - `HeadingLevel Level { get; }` (H1=1, H2=2, etc.), `string Text`
    - `BoldTextSection` - `string Text`
    - `ParagraphSection` - `string Text` with optional multi-line support
    - `HorizontalRuleSection` - no content, represents visual separator
    - `MetadataSection` - `string Label { get; }` (bold), `string Value { get; }` (normal)
  - `IGoogleDocsFormatter` interface:
    - Method: `GoogleDocsDocument FormatDocument(ChatExport chatExport)`
    - Extends: `IMessageFormatter` (inherits `FormatMessage` which should throw)
    - XML docs: "Formats chat exports as structured Google Docs with rich text styling"
- **Dependencies**: None
- **Estimated Lines**: ~120 lines

---

### Phase 2: Parallel Implementation Tracks

**Track A: Formatter Implementation**

**2A.1 Implement GoogleDocsDocumentFormatter with tests**
- **Files**: [`src/WhatsAppArchiver.Domain/Formatting/GoogleDocsDocumentFormatter.cs`](src/WhatsAppArchiver.Domain/Formatting/GoogleDocsDocumentFormatter.cs), [`tests/WhatsAppArchiver.Domain.Tests/Formatting/GoogleDocsDocumentFormatterTests.cs`](tests/WhatsAppArchiver.Domain.Tests/Formatting/GoogleDocsDocumentFormatterTests.cs)
- **Work**: Implement formatter and comprehensive test suite
- **Details**:
  - **Formatter implementation**:
    - `FormatDocument(ChatExport chatExport)`:
      - Create `GoogleDocsDocument` instance
      - Extract sender from first message: `chatExport.Messages[0].Sender`
      - Add `HeadingSection(H1, $"WhatsApp Conversation Export - {senderName}")`
      - Add `MetadataSection("Export Date", DateTime.Now.ToString("MMMM d, yyyy"))`
      - Add `MetadataSection("Total Messages", chatExport.MessageCount.ToString())`
      - Add `HorizontalRuleSection()`
      - Group messages: `chatExport.Messages.GroupBy(m => m.Timestamp.Date).OrderBy(g => g.Key)`
      - For each date group:
        - Add `HeadingSection(H2, date.ToString("MMMM d, yyyy"))`
        - For each message:
          - Add `BoldTextSection(message.Timestamp.ToString("HH:mm"))`
          - Add `ParagraphSection(message.Content)` - preserves line breaks
          - Add `HorizontalRuleSection()`
      - Return document
    - `FormatMessage(ChatMessage)`: Throw `NotSupportedException("GoogleDocsDocumentFormatter requires FormatDocument for batch processing")`
    - Handle empty exports: Return document with header + metadata showing "0" messages
  - **Tests** (8 tests following existing patterns):
    - `FormatDocument_SingleDayMessages_ProducesCorrectSections` - verify section count and types
    - `FormatDocument_MultipleDays_GroupsByDateChronologically` - verify H2 sections in order
    - `FormatDocument_MultiLineMessage_PreservesParagraphContent` - verify content preservation
    - `FormatDocument_EmptyExport_ReturnsHeaderOnly` - verify graceful empty handling
    - `FormatDocument_SpecialCharacters_PreservesContent` - verify no escaping
    - `FormatDocument_SectionTypes_MatchesExpectedStructure` - verify H1, MetadataSection, H2, BoldText, Paragraph, HorizontalRule presence
    - `FormatDocument_TimestampFormat_Uses24Hour` - verify HH:mm format in BoldTextSection
    - `FormatMessage_Called_ThrowsNotSupportedException` - verify throws with message
- **Dependencies**: 1.1 complete
- **Estimated Lines**: ~150 lines (80 formatter + 70 tests)

**Track B: Enum and Factory Updates**

**2B.1 Add GoogleDocs enum value and update FormatterFactory with tests**
- **Files**: [`src/WhatsAppArchiver.Domain/Formatting/MessageFormatType.cs`](src/WhatsAppArchiver.Domain/Formatting/MessageFormatType.cs), [`src/WhatsAppArchiver.Domain/Formatting/FormatterFactory.cs`](src/WhatsAppArchiver.Domain/Formatting/FormatterFactory.cs), [`tests/WhatsAppArchiver.Domain.Tests/Formatting/FormatterFactoryTests.cs`](tests/WhatsAppArchiver.Domain.Tests/Formatting/FormatterFactoryTests.cs)
- **Work**: Add enum, update factory, add tests
- **Details**:
  - **Enum change**:
    - Add `GoogleDocs = 4` to `MessageFormatType`
    - XML docs: "Rich formatted Google Docs with heading styles, bold timestamps, and visual separators. Requires IGoogleDocsFormatter for batch processing."
  - **Factory changes**:
    - Add case: `MessageFormatType.GoogleDocs => new GoogleDocsDocumentFormatter(),`
    - Update `IsDocumentFormatter`: `=> formatType is MessageFormatType.MarkdownDocument or MessageFormatType.GoogleDocs;`
  - **Tests**:
    - `CreateFormatter_GoogleDocsType_ReturnsGoogleDocsDocumentFormatter` - verify type
    - `CreateFormatter_GoogleDocsType_ImplementsIGoogleDocsFormatter` - verify interface
    - `IsDocumentFormatter_GoogleDocs_ReturnsTrue` - verify classification
- **Dependencies**: 1.1 and 2A.1 complete
- **Estimated Lines**: ~30 lines (5 enum + 5 factory + 20 tests)

---

### Phase 3: Infrastructure Integration (Sequential)

**3.1 Extend GoogleDocsServiceAccountAdapter with rich formatting support and tests**
- **Files**: [`src/WhatsAppArchiver.Infrastructure/GoogleDocsServiceAccountAdapter.cs`](src/WhatsAppArchiver.Infrastructure/GoogleDocsServiceAccountAdapter.cs), [`tests/WhatsAppArchiver.Infrastructure.Tests/GoogleDocsServiceAccountAdapterTests.cs`](tests/WhatsAppArchiver.Infrastructure.Tests/GoogleDocsServiceAccountAdapterTests.cs)
- **Work**: Add method to process GoogleDocsDocument with rich styling
- **Details**:
  - **New public method**:
    - `Task AppendRichAsync(string documentId, GoogleDocsDocument document, CancellationToken cancellationToken)`
    - Calls private helper to generate requests
  - **Private helper method**: `List<Request> CreateRichContentRequests(GoogleDocsDocument document, int startIndex)`
    - Initialize `currentIndex = startIndex`
    - Create `List<Request> requests`
    - For each section in `document.Sections`:
      - **HeadingSection**: 
        - Add `InsertText` request at `currentIndex` with text + `"\n"`
        - Add `UpdateParagraphStyle` request for range with `NamedStyleType = HEADING_1 or HEADING_2`
        - Increment `currentIndex` by text length + 1
      - **BoldTextSection**:
        - Add `InsertText` request with text
        - Add `UpdateTextStyle` request with `Bold = true`, `Fields = "bold"`
        - Increment `currentIndex` by text length
      - **ParagraphSection**:
        - Add `InsertText` request with text + `"\n"`
        - Increment `currentIndex` by text length + 1
      - **HorizontalRuleSection**:
        - Add `InsertText` request with `"━━━━━━━━━━━━━━━━━━━━\n"` (using Unicode box-drawing character U+2501 repeated)
        - Increment `currentIndex` by length
      - **MetadataSection**:
        - Add `InsertText` for `label + ": "` 
        - Add `UpdateTextStyle` for label range with `Bold = true`
        - Add `InsertText` for `value + "\n"`
        - Update indices accordingly
    - Return requests list
  - **Index calculation rules**:
    - Start at provided `startIndex` (1 for new document, existing length for append)
    - Text requests insert at current index
    - Style requests apply to range [startIndex, startIndex + length)
    - Increment index by character count including newlines
  - **Tests** (6 tests):
    - `AppendRichAsync_WithHeadingSections_CreatesHeadingStyleRequests` - verify paragraph style requests
    - `AppendRichAsync_WithBoldSections_CreatesBoldTextStyleRequests` - verify bold styling
    - `AppendRichAsync_WithHorizontalRule_InsertsUnicodeLine` - verify separator
    - `AppendRichAsync_WithMetadata_AppliesBoldToLabels` - verify metadata bold styling
    - `AppendRichAsync_ComplexDocument_CalculatesIndicesCorrectly` - verify index math with mock inspection
    - `AppendRichAsync_EmptyDocument_NoRequests` - verify graceful empty handling
- **Dependencies**: 2A.1 complete
- **Estimated Lines**: ~180 lines (120 implementation + 60 tests)

**3.2 Update UploadToGoogleDocsCommandHandler to use rich formatting and tests**
- **Files**: [`src/WhatsAppArchiver.Application/Handlers/UploadToGoogleDocsCommandHandler.cs`](src/WhatsAppArchiver.Application/Handlers/UploadToGoogleDocsCommandHandler.cs), [`tests/WhatsAppArchiver.Application.Tests/Handlers/UploadToGoogleDocsCommandHandlerTests.cs`](tests/WhatsAppArchiver.Application.Tests/Handlers/UploadToGoogleDocsCommandHandlerTests.cs)
- **Work**: Update handler to detect and use rich formatter
- **Details**:
  - **Handler changes** (at existing formatting conditional ~line 101):
    ```csharp
    var formatter = FormatterFactory.Create(command.FormatterType);
    
    if (formatter is IGoogleDocsFormatter googleDocsFormatter)
    {
        // Rich Google Docs formatting
        var exportToFormat = ChatExport.Create(unprocessedMessages, chatExport.Metadata);
        var richDocument = googleDocsFormatter.FormatDocument(exportToFormat);
        await _googleDocsService.AppendRichAsync(command.DocumentId, richDocument, cancellationToken);
    }
    else if (formatter is IDocumentFormatter documentFormatter)
    {
        // Plain text document formatting (e.g., markdown)
        var exportToFormat = ChatExport.Create(unprocessedMessages, chatExport.Metadata);
        var content = documentFormatter.FormatDocument(exportToFormat);
        await _googleDocsService.AppendAsync(command.DocumentId, content, cancellationToken);
    }
    else
    {
        // Message-level formatting
        var content = FormatMessages(unprocessedMessages, formatter);
        await _googleDocsService.AppendAsync(command.DocumentId, content, cancellationToken);
    }
    ```
  - **Tests**:
    - `HandleAsync_WithGoogleDocsFormatter_CallsAppendRichAsync` - verify rich path taken
    - `HandleAsync_WithGoogleDocsFormatter_PassesCorrectDocument` - verify document structure passed
    - Update existing `HandleAsync_WithDocumentFormatter_UsesFormatDocumentMethod` to verify markdown still works
- **Dependencies**: 3.1 complete
- **Estimated Lines**: ~40 lines (15 handler + 25 tests)

---

### Phase 4: Markdown Removal (Sequential - Must complete after Phase 3)

**4.1 Remove MarkdownDocument enum and MarkdownDocumentFormatter**
- **Files**: Delete [`src/WhatsAppArchiver.Domain/Formatting/MarkdownDocumentFormatter.cs`](src/WhatsAppArchiver.Domain/Formatting/MarkdownDocumentFormatter.cs), Delete [`tests/WhatsAppArchiver.Domain.Tests/Formatting/MarkdownDocumentFormatterTests.cs`](tests/WhatsAppArchiver.Domain.Tests/Formatting/MarkdownDocumentFormatterTests.cs), Modify [`src/WhatsAppArchiver.Domain/Formatting/MessageFormatType.cs`](src/WhatsAppArchiver.Domain/Formatting/MessageFormatType.cs), Modify [`src/WhatsAppArchiver.Domain/Formatting/FormatterFactory.cs`](src/WhatsAppArchiver.Domain/Formatting/FormatterFactory.cs)
- **Work**: Remove markdown formatter and enum value
- **Details**:
  - Delete `MarkdownDocumentFormatter.cs` file
  - Delete `MarkdownDocumentFormatterTests.cs` file
  - Remove `MarkdownDocument = 3` from enum (renumber if needed or keep gap)
  - Remove case from `FormatterFactory.Create` switch
  - Update `IsDocumentFormatter` to only check for `GoogleDocs`
  - Remove any markdown-specific XML documentation
- **Dependencies**: 3.2 complete (ensures GoogleDocs formatter is working end-to-end)
- **Estimated Lines**: ~250 lines deleted, ~10 lines modified

**4.2 Remove IDocumentFormatter interface**
- **Files**: Delete [`src/WhatsAppArchiver.Domain/Formatting/IDocumentFormatter.cs`](src/WhatsAppArchiver.Domain/Formatting/IDocumentFormatter.cs), Modify [`src/WhatsAppArchiver.Application/Handlers/UploadToGoogleDocsCommandHandler.cs`](src/WhatsAppArchiver.Application/Handlers/UploadToGoogleDocsCommandHandler.cs)
- **Work**: Remove now-unused abstraction
- **Details**:
  - Delete `IDocumentFormatter.cs` file
  - Remove `IDocumentFormatter` conditional branch from handler (only check `IGoogleDocsFormatter` and message-level formatters)
  - Simplify handler logic to two paths: rich Google Docs or message-level
  - Update tests to remove `IDocumentFormatter` assertions
- **Dependencies**: 4.1 complete
- **Estimated Lines**: ~40 lines deleted, ~5 lines modified in handler

---

### Phase 5: CLI and Documentation (Parallel - After Phase 3)

**Track C: Console Updates**

**5C.1 Update CLI to reference GoogleDocs formatter**
- **Files**: [`src/WhatsAppArchiver.Console/Program.cs`](src/WhatsAppArchiver.Console/Program.cs)
- **Work**: Update help text and examples
- **Details**:
  - Modify `--format` option description:
    - Change to: `"Message format type (default|compact|verbose|googledocs)"`
    - Add explanation: `"googledocs: Rich formatted document with styled headings, bold timestamps, and visual separators"`
  - Remove any references to `markdowndocument` in help or examples
  - Update default formatter example if present
- **Dependencies**: 2B.1 complete (enum exists)
- **Estimated Lines**: ~5 lines modified

**Track D: Documentation**

**5D.1 Update README and formatting specification**
- **Files**: [`README.md`](README.md), [`lappers/formatting-spec.md`](/home/garethd/docs/lappers/formatting-spec.md) (if exists)
- **Work**: Document new Google Docs rich formatting capability
- **Details**:
  - Update README section on formatters:
    - Remove markdown formatter documentation
    - Add GoogleDocs formatter section with visual example
    - Explain rich formatting features (headings, bold, separators)
    - Show CLI usage: `--format googledocs`
  - Update `formatting-spec.md` if it exists:
    - Document section types and styling rules
    - Add examples of rendered output
    - Explain index calculation for developers
- **Dependencies**: None (can document before implementation)
- **Estimated Lines**: ~100 lines documentation

---

## Parallel Execution Summary

```
Phase 1 (Sequential):
  └─ 1.1: Create abstractions (GoogleDocsDocument, IGoogleDocsFormatter, section types)

Phase 2 (Parallel after Phase 1):
  ├─ Track A: 2A.1: Implement GoogleDocsDocumentFormatter + tests
  └─ Track B: 2B.1: Update enum + factory + tests [WAIT for 2A.1]

Phase 3 (Sequential after Phase 2):
  ├─ 3.1: Extend GoogleDocsServiceAccountAdapter + tests
  └─ 3.2: Update handler + tests

Phase 4 (Sequential after Phase 3):
  ├─ 4.1: Remove MarkdownDocumentFormatter + enum
  └─ 4.2: Remove IDocumentFormatter interface

Phase 5 (Parallel after Phase 3):
  ├─ Track C: 5C.1: Update CLI help
  └─ Track D: 5D.1: Update documentation
```

**Total Estimated Lines**: ~520 new/modified, ~290 deleted

## Further Considerations

1. **Horizontal Rule Representation** - Google Docs API doesn't have native horizontal rule insert. Current plan uses repeated Unicode box-drawing character (U+2501 = ━). Alternatives: A) Use triple underscores `___` with bottom border paragraph style, B) Insert actual horizontal line via `InsertHorizontalRule` request (requires researching if API supports), C) Use empty paragraph with bottom border. **Recommend**: Unicode character approach for simplicity unless native API support exists.

2. **Index Calculation Strategy** - Creating batch requests requires careful index tracking. Current plan: Calculate indices sequentially as sections are added. Risk: Off-by-one errors breaking styling. Alternatives: A) Build all text first, then calculate style ranges (two-pass), B) Use helper class `IndexTracker` with validation, C) Integration test with real API to verify calculations. **Recommend**: Add `IndexTracker` helper class in Phase 3.1 with extensive tests.

3. **Backward Compatibility for Existing State Files** - Removing `MarkdownDocument` enum changes serialization. Processing state files may contain `FormatterType = 3`. Options: A) Break compatibility (users re-run from scratch), B) Add migration logic to remap 3→4, C) Keep enum value 3 empty with obsolete attribute. **Recommend**: Option C - mark `MarkdownDocument = 3` as `[Obsolete("Replaced by GoogleDocs")]` and map to GoogleDocs in factory, remove in future major version.

4. **Font and Color Customization** - Current plan uses default Google Docs styling. Users may want customizable colors for timestamps or backgrounds. Options: A) Hardcode blue timestamps like sample.md, B) Add configuration for colors/fonts, C) Keep default black text only. **Recommend**: Start with option C (default styling) to match Google Docs conventions, add customization in future enhancement based on user feedback.
