# Google Docs Formatter - Technical Specification

This document provides detailed technical information about the GoogleDocs formatter implementation, including section types, styling rules, index calculations, and examples for developers working with the formatter.

## Overview

The GoogleDocs formatter (`GoogleDocsDocumentFormatter`) is a specialized formatter that creates rich text documents in Google Docs with professional styling and structure. Unlike simple text formatters, it uses the Google Docs API to apply heading styles, bold text, and visual separators.

**Key Characteristics:**
- Batch processing: Formats entire chat exports, not individual messages
- Rich text support: Uses Google Docs API styling capabilities
- Structured sections: Organizes content into typed document sections
- Index-based positioning: Calculates precise insertion points for each section

## Document Section Types

The formatter uses a structured document model composed of distinct section types. Each section type corresponds to a specific visual element in the final Google Docs document.

### HeadingSection

Represents a heading with a specific level (H1-H6).

**Properties:**
- `Level` (int): Heading level from 1 to 6 (H1=1, H2=2, etc.)
- `Text` (string): The heading text content
- `Content` (string): Returns the text content

**Usage in GoogleDocs Formatter:**
- **H1 (Level 1)**: Document title with sender name
  - Format: `"WhatsApp Conversation Export - {senderName}"`
  - Example: `new HeadingSection(1, "WhatsApp Conversation Export - John Smith")`
  
- **H2 (Level 2)**: Date group headers
  - Format: `"MMMM d, yyyy"` (e.g., "December 15, 2024")
  - Example: `new HeadingSection(2, "December 15, 2024")`

**Rendering:**
- Applied using Google Docs API `UpdateParagraphStyleRequest`
- Uses `NamedStyleType.HEADING_1` or `NamedStyleType.HEADING_2`
- Automatically formatted with Google Docs default heading styles

**Validation:**
- Level must be between 1 and 6 (enforced by constructor)
- Text cannot be null or whitespace

### BoldTextSection

Represents text that should be displayed in bold styling.

**Properties:**
- `Text` (string): The bold text content
- `Content` (string): Returns the text content

**Usage in GoogleDocs Formatter:**
- Message timestamps in 24-hour format
  - Format: `"HH:mm"` (e.g., "09:15", "14:30")
  - Example: `new BoldTextSection("09:15")`

**Rendering:**
- Applied using Google Docs API `UpdateTextStyleRequest`
- Sets `bold: true` in the text style
- Text is inserted as-is (no newline is automatically appended)

**Validation:**
- Text cannot be null or whitespace

### ParagraphSection

Represents normal paragraph text with optional multi-line support.

**Properties:**
- `Text` (string): The paragraph text content
- `Content` (string): Returns the text content

**Usage in GoogleDocs Formatter:**
- Message content (may contain multiple lines)
  - Preserves original line breaks from WhatsApp messages
  - Example: `new ParagraphSection("Hello!\nHow are you?\nTalk soon!")`

**Rendering:**
- Inserted as plain text using `InsertTextRequest`
- No special styling applied (normal paragraph style)
- Line breaks (`\n`) are preserved in the document

**Validation:**
- Text cannot be null or whitespace

### HorizontalRuleSection

Represents a visual separator (horizontal rule) between content sections.

**Properties:**
- `Content` (string): Returns empty string (section has no text content)

**Usage in GoogleDocs Formatter:**
- Separates individual messages within a date group
- Separates the metadata section from message content

**Rendering:**
- Rendered as Unicode character U+2501 (━) repeated 20 times
- Implementation: `"━━━━━━━━━━━━━━━━━━━━\n"` (20 box drawing characters + newline)
- Inserted as plain text using `InsertTextRequest`

**Visual Appearance:**
```
━━━━━━━━━━━━━━━━━━━━
```

### MetadataSection

Represents a key-value pair where the label is bold and the value is normal text.

**Properties:**
- `Label` (string): The metadata label (displayed in bold)
- `Value` (string): The metadata value (displayed in normal text)
- `Content` (string): Returns `"{Label}: {Value}"`

**Usage in GoogleDocs Formatter:**
- Export date metadata
  - Example: `new MetadataSection("Export Date", "December 15, 2024")`
  
- Total messages count
  - Example: `new MetadataSection("Total Messages", "127")`

**Rendering:**
- Label, colon (":"), and the following space are rendered in bold using `UpdateTextStyleRequest`
- Value is rendered in normal text (no bold)
- Format: `"{Label}: {Value}\n"`
- Example rendering: `**Export Date: **December 15, 2024`

**Validation:**
- Both label and value cannot be null or whitespace

## Styling Rules

### Google Docs API Integration

The formatter uses the Google Docs API batch update mechanism to apply rich formatting. All styling is applied through structured API requests.

#### Heading Styles

**API Request Type:** `UpdateParagraphStyleRequest`

**H1 (Title):**
```csharp
new UpdateParagraphStyleRequest
{
    Range = new Range { StartIndex = startIndex, EndIndex = endIndex },
    ParagraphStyle = new ParagraphStyle
    {
        NamedStyleType = "HEADING_1"
    },
    Fields = "namedStyleType"
}
```

**H2 (Date Headers):**
```csharp
new UpdateParagraphStyleRequest
{
    Range = new Range { StartIndex = startIndex, EndIndex = endIndex },
    ParagraphStyle = new ParagraphStyle
    {
        NamedStyleType = "HEADING_2"
    },
    Fields = "namedStyleType"
}
```

#### Bold Text Styles

**API Request Type:** `UpdateTextStyleRequest`

**Bold Timestamps:**
```csharp
new UpdateTextStyleRequest
{
    Range = new Range { StartIndex = startIndex, EndIndex = endIndex },
    TextStyle = new TextStyle
    {
        Bold = true
    },
    Fields = "bold"
}
```

**Bold Metadata Labels:**
```csharp
// For "Export Date:" portion only
new UpdateTextStyleRequest
{
    Range = new Range { StartIndex = startIndex, EndIndex = labelEndIndex },
    TextStyle = new TextStyle
    {
        Bold = true
    },
    Fields = "bold"
}
```

#### Text Insertion

**API Request Type:** `InsertTextRequest`

All text content (paragraphs, horizontal rules, etc.) is inserted using:
```csharp
new InsertTextRequest
{
    Location = new Location { Index = currentIndex },
    Text = textContent
}
```

## Index Calculation for Developers

Understanding index calculation is crucial for working with the Google Docs API. The API uses a 1-based index system where index 1 represents the beginning of the document content.

### Basic Index Tracking

**Starting Index:**
- Documents start at index 1 (the beginning of content)
- Index 0 is invalid in the Google Docs API

**Index Progression:**
Each section inserted increments the index by:
```
newIndex = currentIndex + textLength + newlineCount
```

Where:
- `textLength`: Number of characters in the text content
- `newlineCount`: Number of newline characters (`\n`) added

### Detailed Index Calculation Examples

#### Example 1: H1 Title Section

**Content:** `"WhatsApp Conversation Export - John Smith\n"`

**Calculation:**
```
StartIndex: 1
Text: "WhatsApp Conversation Export - John Smith"
Length: 41 characters
Newline: 1 character (\n)
EndIndex: 1 + 41 + 1 = 43
```

**API Requests:**
1. Insert text at index 1: `"WhatsApp Conversation Export - John Smith\n"`
2. Apply H1 style to range [1, 43)

**Next section starts at:** Index 43

#### Example 2: Metadata Section

**Content:** `"Export Date: December 15, 2024\n"`

**Calculation:**
```
StartIndex: 43 (assuming previous section ended at 43)
Label: "Export Date"
Label with colon and space: "Export Date: " (includes colon and space)
Label Length: 13 characters
Value: "December 15, 2024"
Value with newline: "December 15, 2024\n"
Value Length: 18 characters (17 + newline)
EndIndex: 43 + 13 + 18 = 74
```

**API Requests:**
1. Insert label text at index 43: `"Export Date: "`
2. Apply bold to label range [43, 56) - "Export Date: " (13 characters including colon and space)
3. Insert value text at index 56: `"December 15, 2024\n"`

**Next section starts at:** Index 74

#### Example 3: Horizontal Rule

**Content:** `"━━━━━━━━━━━━━━━━━━━━\n"`

**Calculation:**
```
StartIndex: 74
Characters: 20 × "━" = 20 characters
Newline: 1 character (\n)
EndIndex: 74 + 20 + 1 = 95
```

**API Requests:**
1. Insert text at index 74: `"━━━━━━━━━━━━━━━━━━━━\n"`

**Next section starts at:** Index 95

#### Example 4: Complete Message Sequence

A typical message consists of: Bold timestamp + Paragraph content + Horizontal rule

**Message:** Time "09:15", Content "Hello!\nHow are you?"

**Calculation:**
```
StartIndex: 95

1. Bold Timestamp:
   Text: "09:15" (no newline appended to bold sections)
   Length: 5
   Range for bold: [95, 100)
   Current Index: 95 + 5 = 100

2. Paragraph Content:
   Text: "Hello!\nHow are you?\n" (newline IS appended to paragraphs)
   Length: 6 + 1 + 13 + 1 = 21
   Current Index: 100 + 21 = 121

3. Horizontal Rule:
   Text: "━━━━━━━━━━━━━━━━━━━━\n"
   Length: 20 + 1 = 21
   Current Index: 121 + 21 = 142
```

**API Requests:**
1. Insert "09:15" at index 95
2. Apply bold to range [95, 100)
3. Insert "Hello!\nHow are you?\n" at index 100
4. Insert "━━━━━━━━━━━━━━━━━━━━\n" at index 121

**Next section starts at:** Index 142

### Multi-line Content Handling

Multi-line content preserves internal newlines but always ends with a newline.

**Example:** `"Line 1\nLine 2\nLine 3\n"`

**Index Calculation:**
```
Text Length: 6 + 6 + 6 = 18 characters (excluding newlines)
Newlines: 3 characters (\n after each line)
Total: 18 + 3 = 21
Index Increment: 21
```

### Debugging Index Issues

**Common Problems:**

1. **Off-by-one errors**: Remember that ranges are [start, end) - end is exclusive
2. **Missing newlines**: Each section typically ends with \n
3. **Incorrect length calculation**: Count all characters including spaces and punctuation
4. **Style range mismatch**: Style ranges must match the exact text indices

**Validation Tips:**

```csharp
// Log index progression for debugging
Console.WriteLine($"Section: {section.GetType().Name}");
Console.WriteLine($"Start Index: {currentIndex}");
Console.WriteLine($"Text Length: {text.Length}");
Console.WriteLine($"End Index: {currentIndex + text.Length}");
```

## Document Structure Example

Here's a complete example showing how a chat export is structured:

### Input (ChatExport)

```csharp
var chatExport = new ChatExport
{
    Metadata = new ExportMetadata
    {
        ParsedAt = DateTime.Parse("2024-12-15T10:30:00")
    },
    Messages = new List<ChatMessage>
    {
        new ChatMessage("2024-12-14T09:15:00", "John Smith", "Good morning!"),
        new ChatMessage("2024-12-14T09:16:00", "John Smith", "How are you?"),
        new ChatMessage("2024-12-15T08:30:00", "John Smith", "Just checking in.")
    }
};
```

### Output (GoogleDocsDocument Sections)

```csharp
[
    HeadingSection(1, "WhatsApp Conversation Export - John Smith"),
    MetadataSection("Export Date", "December 15, 2024"),
    MetadataSection("Total Messages", "3"),
    HorizontalRuleSection(),
    
    HeadingSection(2, "December 14, 2024"),
    BoldTextSection("09:15"),
    ParagraphSection("Good morning!"),
    HorizontalRuleSection(),
    BoldTextSection("09:16"),
    ParagraphSection("How are you?"),
    HorizontalRuleSection(),
    
    HeadingSection(2, "December 15, 2024"),
    BoldTextSection("08:30"),
    ParagraphSection("Just checking in."),
    HorizontalRuleSection()
]
```

### Rendered in Google Docs

```
# WhatsApp Conversation Export - John Smith

Export Date: December 15, 2024
Total Messages: 3

━━━━━━━━━━━━━━━━━━━━

## December 14, 2024

09:15Good morning!
━━━━━━━━━━━━━━━━━━━━

09:16How are you?
━━━━━━━━━━━━━━━━━━━━

## December 15, 2024

08:30Just checking in.
━━━━━━━━━━━━━━━━━━━━
```

**Note:** In actual Google Docs:
- `#` headings are styled with H1 formatting
- `##` headings are styled with H2 formatting
- Timestamps (e.g., "09:15") and metadata labels (e.g., "Export Date:") appear in **bold**
- Horizontal rules appear as visual separators

## Implementation Details

### GoogleDocsDocumentFormatter Class

**Namespace:** `WhatsAppArchiver.Domain.Formatting`

**Implements:** `IGoogleDocsFormatter`, `IMessageFormatter`

**Key Methods:**

1. **FormatDocument(ChatExport chatExport): GoogleDocsDocument**
   - Main entry point for formatting
   - Processes entire chat export
   - Returns structured document model

2. **FormatMessage(ChatMessage message): string**
   - Throws NotSupportedException
   - Individual message formatting not supported

### GoogleDocsServiceAccountAdapter Class

**Namespace:** `WhatsAppArchiver.Infrastructure`

**Key Methods:**

1. **InsertRichAsync(string documentId, GoogleDocsDocument document, CancellationToken cancellationToken)**
   - Inserts formatted document at beginning of Google Doc (index 1)
   - Creates batch update requests from document sections
   - Applies all formatting in a single API call

2. **AppendRichAsync(string documentId, GoogleDocsDocument document, CancellationToken cancellationToken)**
   - Appends formatted document to end of Google Doc
   - Retrieves current document length to calculate start index
   - Creates batch update requests from document sections

3. **CreateRichContentRequests(GoogleDocsDocument document, int startIndex): List<Request>**
   - Converts document sections to Google Docs API requests
   - Calculates indices for each section
   - Returns batch of insert and style requests

### Request Processing Order

Requests should be added in the following order to ensure correct formatting:

1. **Text insertion requests** (added first)
2. **Styling requests** (added second)

This ensures text exists before styling is applied.

## Testing Considerations

### Unit Testing Sections

```csharp
[Fact]
public void HeadingSection_ValidLevel_CreatesHeading()
{
    // Arrange & Act
    var heading = new HeadingSection(1, "Test Heading");
    
    // Assert
    Assert.Equal(1, heading.Level);
    Assert.Equal("Test Heading", heading.Text);
    Assert.Equal("Test Heading", heading.Content);
}
```

### Integration Testing Formatter

```csharp
[Fact]
public void FormatDocument_WithMessages_CreatesStructuredDocument()
{
    // Arrange
    var formatter = new GoogleDocsDocumentFormatter();
    var chatExport = CreateTestChatExport();
    
    // Act
    var document = formatter.FormatDocument(chatExport);
    
    // Assert
    Assert.NotEmpty(document.Sections);
    Assert.IsType<HeadingSection>(document.Sections[0]);
    Assert.Equal(1, ((HeadingSection)document.Sections[0]).Level);
}
```

### Testing Index Calculations

```csharp
[Fact]
public void CreateRichContentRequests_CalculatesCorrectIndices()
{
    // Arrange
    var document = new GoogleDocsDocument();
    document.Add(new HeadingSection(1, "Title"));
    int startIndex = 1;
    
    // Act
    var requests = CreateRichContentRequests(document, startIndex);
    
    // Assert
    var insertRequest = requests.OfType<InsertTextRequest>().First();
    Assert.Equal(1, insertRequest.Location.Index);
    
    var styleRequest = requests.OfType<UpdateParagraphStyleRequest>().First();
    Assert.Equal(1, styleRequest.Range.StartIndex);
    Assert.Equal(7, styleRequest.Range.EndIndex); // "Title\n" = 6 chars
}
```

## Troubleshooting

### Index Mismatch Errors

**Symptom:** Google Docs API returns error about invalid range

**Cause:** Style range doesn't match inserted text

**Solution:**
1. Verify text was inserted before applying styles
2. Check that EndIndex = StartIndex + text.Length
3. Ensure newlines are counted in length calculations

### Missing Formatting

**Symptom:** Text appears but formatting is missing

**Cause:** Batch request order incorrect or style requests missing

**Solution:**
1. Verify style requests are included in batch
2. Check request order (text first, then styles)
3. Confirm Fields parameter is set ("namedStyleType" for headings, "bold" for text)

### Unexpected Line Breaks

**Symptom:** Extra blank lines in document

**Cause:** Inconsistent newline handling

**Solution:**
1. Ensure each section ends with exactly one newline
2. Don't add extra newlines between sections
3. Check that multi-line content preserves internal newlines

## Further Reading

- **Google Docs API Documentation**: https://developers.google.com/docs/api
- **Batch Update Requests**: https://developers.google.com/docs/api/how-tos/batch-update
- **Text Styling**: https://developers.google.com/docs/api/how-tos/style-text
- **Domain-Driven Design**: `.github/instructions/dotnet-architecture-good-practices.instructions.md`

## Conclusion

The GoogleDocs formatter provides a sophisticated way to create rich, professional chat archives in Google Docs. By understanding the section types, styling rules, and index calculations, developers can extend and maintain the formatter with confidence.

For questions or contributions, please refer to the main project README.md and contribution guidelines.
