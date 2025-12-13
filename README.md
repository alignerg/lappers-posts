# WhatsApp Archiver

A .NET 10 console application that parses WhatsApp text exports, filters messages by sender, and uploads them to Google Docs using service account authentication. Built with Domain-Driven Design (DDD) principles and SOLID architecture.

## Table of Contents

- [Features](#features)
- [Architecture Overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [WhatsApp Export Format Requirements](#whatsapp-export-format-requirements)
- [Google Service Account Setup](#google-service-account-setup)
- [Installation](#installation)
- [Configuration](#configuration)
- [Running the Application](#running-the-application)
  - [Windows](#windows)
  - [macOS](#macos)
- [Usage Examples](#usage-examples)
- [Troubleshooting](#troubleshooting)
- [Project Structure](#project-structure)
- [Development](#development)
  - [Build and Test](#build-and-test)
  - [Testing and Code Coverage](#testing-and-code-coverage)
  - [Architecture](#architecture)

## Features

- Parse WhatsApp chat export text files with multiple date/time formats
- Filter messages by sender (case-insensitive)
- Multiple message formatting options (default, compact, verbose)
- Idempotent processing with state tracking
- Upload to Google Docs using service account authentication
- Resilient operations with automatic retry policies
- Structured logging with Serilog

## Architecture Overview

This application follows **Domain-Driven Design (DDD)** principles with a clean, layered architecture that enforces separation of concerns and promotes maintainability. The architecture consists of four distinct layers:

### Domain Layer (`WhatsAppArchiver.Domain`)
The core business logic layer containing:
- **Entities**: `ChatExport`, `ChatMessage` - Core business objects representing the chat domain
- **Value Objects**: `MessageTimestamp`, `MessageContent`, `Sender` - Immutable objects representing domain concepts
- **Specifications**: Business rules for filtering and querying messages
- **Formatters**: `IMessageFormatter` implementations for different message display formats
- **Domain Services**: Stateless services implementing complex business operations

**Key Principles:**
- No dependencies on other layers
- Contains all business logic and domain rules
- Rich domain models with behavior, not just data
- Immutable value objects for thread safety

### Application Layer (`WhatsAppArchiver.Application`)
Orchestrates domain operations and coordinates workflows:
- **Command Handlers**: `ParseChatCommandHandler`, `UploadToGoogleDocsCommandHandler` - Execute business operations
- **Commands**: DTOs representing user intentions
- **Service Interfaces**: Defines contracts for infrastructure services
- **Application Services**: Coordinate multiple domain operations

**Key Principles:**
- Depends only on Domain layer
- No business logic (orchestration only)
- Transaction boundaries and workflow coordination
- Input validation before domain operations

### Infrastructure Layer (`WhatsAppArchiver.Infrastructure`)
Implements external integrations and technical capabilities:
- **Google Docs Integration**: Service account authentication and API client
- **File Parsers**: WhatsApp text file parsing with multiple format support
- **State Repository**: JSON-based persistence for idempotent processing
- **Resilience Policies**: Retry logic using Polly for external service calls

**Key Principles:**
- Implements interfaces defined in Application/Domain layers
- All external dependencies (APIs, file system, database)
- Technology-specific implementations
- Adapters for third-party services

### Console Layer (`WhatsAppArchiver.Console`)
Application host and user interface:
- **CLI Interface**: System.CommandLine for argument parsing and validation
- **Dependency Injection**: Service registration and lifetime management
- **Configuration**: Settings management and environment handling
- **Logging Setup**: Serilog configuration

**Key Principles:**
- Entry point for the application
- Minimal logic (configuration and startup only)
- Dependency injection container setup
- User-facing error handling

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Google Cloud Platform account
- A Google Docs document ID where you want to upload the messages

## WhatsApp Export Format Requirements

The application supports two WhatsApp export date/time formats. When exporting your WhatsApp chat, select **"Export chat without media"** to get a text-only file.

### Supported Date Formats

#### Format 1: DD/MM/YYYY, HH:mm:ss (European/International)
```
[25/12/2024, 09:15:00] John Smith: Good morning everyone! 🎄
[25/12/2024, 09:16:30] Maria Garcia: Merry Christmas to all! 🎁
[25/12/2024, 09:18:45] John Smith: Hope you're having a wonderful holiday
[26/12/2024, 14:20:33] Alex Johnson: Yesterday was amazing!
```

**Format characteristics:**
- Day: 2 digits (01-31)
- Month: 2 digits (01-12)
- Year: 4 digits
- Time: 24-hour format with seconds
- Separator: Square brackets with comma

#### Format 2: M/D/YY, H:mm:ss AM/PM (US/Mobile)
```
[1/5/24, 8:30:00 AM] Sarah Wilson: Hey, are you coming to the meeting?
[1/5/24, 8:32:15 AM] Michael Brown: Yes, running 5 min late
[12/31/24, 11:58:45 PM] Sarah Wilson: Happy New Year everyone! 🎆
[1/1/25, 12:00:00 AM] Michael Brown: 🎉🎉🎉
```

**Format characteristics:**
- Month: 1-2 digits (1-12)
- Day: 1-2 digits (1-31)
- Year: 2 digits
- Time: 12-hour format with AM/PM and seconds
- Separator: Square brackets with comma

### Message Structure

Each message line follows this pattern:
```
[TIMESTAMP] SENDER_NAME: MESSAGE_CONTENT
```

**Multi-line messages** are supported - continuation lines don't have a timestamp or sender:
```
[25/12/2024, 09:15:00] John Smith: This is a long message
that spans multiple lines
and continues here
[25/12/2024, 09:16:00] Maria Garcia: Next message
```

### How to Export from WhatsApp

**Android:**
1. Open the chat you want to export
2. Tap the three dots (⋮) menu → More → Export chat
3. Select "WITHOUT MEDIA"
4. Choose where to save the file

**iOS:**
1. Open the chat you want to export
2. Tap the contact/group name at the top
3. Scroll down and tap "Export Chat"
4. Select "Without Media"
5. Choose where to save the file

**Important Notes:**
- Always export **without media** to get the text format
- The format depends on your phone's region settings
- Both formats are automatically detected by the application
- System messages (encryption notices, group changes) are skipped
- Emojis and special characters are preserved

## Google Service Account Setup

Follow these steps to create and configure a Google service account for the application:

### 1. Create a Google Cloud Project

1. Go to the [Google Cloud Console](https://console.cloud.google.com)
2. Click on the project dropdown at the top of the page
3. Click **"New Project"**
4. Enter a project name (e.g., "WhatsApp Archiver")
5. Click **"Create"**

### 2. Enable the Google Docs API

1. In the Google Cloud Console, navigate to **"APIs & Services"** > **"Library"**
2. Search for **"Google Docs API"**
3. Click on **"Google Docs API"** in the results
4. Click **"Enable"**

### 3. Create a Service Account

1. Navigate to **"APIs & Services"** > **"Credentials"**
2. Click **"Create Credentials"** > **"Service Account"**
3. Enter a service account name (e.g., "whatsapp-archiver-sa")
4. Enter a service account ID (e.g., "whatsapp-archiver-sa")
5. Optionally add a description (e.g., "Service account for WhatsApp Archiver application")
6. Click **"Create and Continue"**
7. Skip the optional role assignment (click **"Continue"**)
8. Skip granting user access (click **"Done"**)

### 4. Create and Download the Service Account Key

1. On the **"Credentials"** page, find your newly created service account in the **"Service Accounts"** section
2. Click on the service account name
3. Go to the **"Keys"** tab
4. Click **"Add Key"** > **"Create new key"**
5. Select **"JSON"** as the key type
6. Click **"Create"**
7. The JSON key file will be automatically downloaded to your computer
8. **Important**: Store this file securely - it contains credentials to access Google APIs

### 5. Grant the Service Account Access to Your Google Doc

1. Open the downloaded JSON key file and locate the `client_email` field
   - It will look like: `whatsapp-archiver-sa@your-project-id.iam.gserviceaccount.com`
2. Open the Google Docs document where you want to upload messages
3. Click **"Share"** in the top-right corner
4. Paste the service account email address
5. Grant **"Editor"** permissions
6. Uncheck **"Notify people"** (service accounts don't need notifications)
7. Click **"Share"**

### 6. Get Your Google Doc ID

The document ID can be found in the Google Docs URL:
```
https://docs.google.com/document/d/YOUR_DOCUMENT_ID/edit
                                  ^^^^^^^^^^^^^^^^^^^^
```
Copy the document ID - you'll need it when running the application.

### 7. Store the Credentials File Securely

**Option A: Store in a secure directory (recommended)**

**Windows:**
```powershell
# Create a credentials directory
mkdir C:\Users\YourUsername\.credentials
# Move the downloaded key file
move Downloads\your-project-123456-abc123.json C:\Users\YourUsername\.credentials\google-service-account.json
```

**macOS:**
```bash
# Create a credentials directory
mkdir -p ~/.credentials
# Move the downloaded key file
mv ~/Downloads/your-project-123456-abc123.json ~/.credentials/google-service-account.json
# Secure the file permissions
chmod 600 ~/.credentials/google-service-account.json
```

**Option B: Store in the application directory**

Place the JSON key file in the application directory (e.g., `./credentials/google-service-account.json`)

**Important Security Notes:**
- Never commit the credentials JSON file to version control
- Use restrictive file permissions (600 on Unix-like systems)
- Consider using environment-specific secrets management in production

## Installation

### Clone the Repository

```bash
git clone https://github.com/alignerg/lappers-posts.git
cd lappers-posts
```

### Build the Application

```bash
# Restore dependencies
dotnet restore src/WhatsAppArchiver.sln

# Build the solution
dotnet build src/WhatsAppArchiver.sln --configuration Release

# Run tests (optional)
dotnet test src/WhatsAppArchiver.sln
```

The compiled application will be in:
- `src/WhatsAppArchiver.Console/bin/Release/net10.0/`

## Configuration

### Create Your Configuration File

1. Navigate to the console application directory:
   ```bash
   cd src/WhatsAppArchiver.Console
   ```

2. Copy the sample configuration:
   
   **Windows:**
   ```powershell
   copy appsettings.sample.json appsettings.json
   ```
   
   **macOS:**
   ```bash
   cp appsettings.sample.json appsettings.json
   ```

3. Edit `appsettings.json` and update the following values:

```json
{
  "WhatsAppArchiver": {
    "GoogleServiceAccount": {
      "CredentialsPath": "PATH_TO_YOUR_CREDENTIALS_FILE"
    },
    "DefaultFormatter": "default",
    "Logging": {
      "MinimumLevel": "Information",
      "FilePath": "./logs/whatsapp-archiver.log",
      "RetentionDays": 30
    }
  }
}
```

**Configuration Options:**

- **CredentialsPath**: Path to your Google service account JSON key file
  - Windows example: `C:\\Users\\YourUsername\\.credentials\\google-service-account.json`
  - macOS example: `~/.credentials/google-service-account.json` or `/Users/YourUsername/.credentials/google-service-account.json`
  - Relative path example: `./credentials/google-service-account.json`

- **DefaultFormatter**: Message formatting style
  - `default`: `[{timestamp}] {sender}: {content}`
  - `compact`: `{sender}: {content}`
  - `verbose`: Detailed format with full date/time and metadata
  - `markdowndocument`: Structured markdown with friendly date headers and individual timestamped posts

- **Logging**: Configure application logging
  - `MinimumLevel`: Trace, Debug, Information, Warning, Error, or Critical
  - `FilePath`: Where log files are written
  - `RetentionDays`: How long to keep old log files

## Running the Application

### Windows

#### Option 1: Using the Published Executable

```powershell
# Navigate to the build output directory
cd src\WhatsAppArchiver.Console\bin\Release\net10.0

# Run the application with required arguments
.\WhatsAppArchiver.Console.exe --chat-file "path\to\chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir ".\state"
```

#### Option 2: Using dotnet run

```powershell
# From the repository root
cd src\WhatsAppArchiver.Console

# Run with dotnet and required arguments
dotnet run --configuration Release -- --chat-file "path\to\chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir ".\state"
```

#### Example with Full Paths (Windows)

```powershell
# Using the executable
cd C:\Projects\lappers-posts\src\WhatsAppArchiver.Console\bin\Release\net10.0
.\WhatsAppArchiver.Console.exe --chat-file "C:\exports\chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir "C:\state"

# Using dotnet run
cd C:\Projects\lappers-posts\src\WhatsAppArchiver.Console
dotnet run --configuration Release -- --chat-file "C:\exports\chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir "C:\state"
```

### macOS

#### Option 1: Using the Published Executable

```bash
# Navigate to the build output directory
cd src/WhatsAppArchiver.Console/bin/Release/net10.0

# Make the executable runnable (if needed)
chmod +x WhatsAppArchiver.Console

# Run the application with required arguments
./WhatsAppArchiver.Console --chat-file "./exports/chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir "./state"
```

#### Option 2: Using dotnet run

```bash
# From the repository root
cd src/WhatsAppArchiver.Console

# Run with dotnet and required arguments
dotnet run --configuration Release -- --chat-file "./exports/chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir "./state"
```

#### Example with Full Paths (macOS)

```bash
# Using the executable
cd /Users/yourusername/Projects/lappers-posts/src/WhatsAppArchiver.Console/bin/Release/net10.0
./WhatsAppArchiver.Console --chat-file "/Users/yourusername/exports/chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir "/Users/yourusername/state"

# Using dotnet run
cd /Users/yourusername/Projects/lappers-posts/src/WhatsAppArchiver.Console
dotnet run --configuration Release -- --chat-file "/Users/yourusername/exports/chat.txt" --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir "/Users/yourusername/state"
```

## Usage Examples

### Upload Messages to Google Docs

The application requires four mandatory arguments and supports several optional arguments:

```bash
# Basic usage with required arguments
dotnet run -- --chat-file ./exports/chat.txt --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir ./state

# Use a specific message format
dotnet run -- --chat-file ./exports/chat.txt --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir ./state --format compact

# Use markdown document format with friendly date headers
dotnet run -- --chat-file ./exports/chat.txt --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir ./state --format markdowndocument

# Use a custom configuration file
dotnet run -- --chat-file ./exports/chat.txt --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir ./state --config ./custom-appsettings.json

# Combine multiple arguments with different state directory
dotnet run -- --chat-file ./exports/chat.txt --sender-filter "John Smith" --doc-id "YOUR_DOCUMENT_ID" --state-dir ~/docs/lappers/state --format verbose --config ./custom-appsettings.json
```

### Command-Line Arguments

**Required Arguments:**

- `--chat-file`: Path to the WhatsApp chat export text file
  - The file must exist
  - Supports both absolute and relative paths

- `--sender-filter`: Name of the sender to filter messages by
  - Case-insensitive matching
  - Cannot be empty

- `--doc-id`: Google Docs document ID
  - Found in the document URL: `https://docs.google.com/document/d/YOUR_DOCUMENT_ID/edit`
  - Cannot be empty

- `--state-dir`: Directory path where processing state files will be stored
  - The application creates this directory if it doesn't exist
  - State files track which messages have been processed to enable resumable operations
  - Supports both absolute and relative paths (including tilde expansion on Unix-like systems)

**Optional Arguments:**

- `--format`: Message format type (default: `default`)
  - `default`: `[{timestamp}] {sender}: {content}`
  - `compact`: `{sender}: {content}`
  - `verbose`: Detailed format with full date/time and metadata
  - `markdowndocument`: Structured markdown with friendly date headers (e.g., "December 5, 2024") and individual timestamped posts (case-insensitive)

- `--config`: Path to a custom configuration file (appsettings.json)
  - Overrides the default configuration file
  - File must exist if specified

### State File Behavior

The application uses **state files** to track processing progress and ensure **idempotent operations** - meaning you can safely run the same command multiple times without uploading duplicate messages.

#### How State Files Work

1. **Location**: State files are stored in the directory you specify with the `--state-dir` argument:
   ```
   /state/                        # State directory (specified via --state-dir)
   /state/1syiodaz_rzgytu7c-mu__peyyczv0byfjfurf_stvy8__rudi_anderson.json  # Auto-generated state file
   ```

2. **Filename Generation**: The application automatically generates state filenames based on:
   - **Document ID**: Sanitized and normalized
   - **Sender filter**: Sanitized and normalized (if provided)
   - Format: `{documentId}__{senderName}.json` or `{documentId}.json` (without sender filter)

3. **Content**: Each state file contains:
   - **Document ID**: The Google Docs document being updated
   - **Processed message IDs**: List of message identifiers already uploaded
   - **Last processing timestamp**: When the operation last ran
   - **Sender filter**: The sender name being filtered (if applicable)

4. **Idempotency**: On subsequent runs:
   - The application loads the appropriate state file from the specified directory
   - Skips messages already in the processed list
   - Only uploads new messages not previously sent
   - Updates the state file with newly processed messages

#### State File Example

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "documentId": "1a2b3c4d5e6f7g8h9i0j",
  "lastProcessedTimestamp": "2024-12-06T10:30:45.123Z",
  "processedMessageIds": [
    {
      "timestamp": "2024-12-05T08:15:30.000Z",
      "contentHash": "a1b2c3d4e5f6g7h8"
    }
  ],
  "senderName": "John Smith"
  }
}
```

#### Use Cases

**Resume after failure**: If upload fails mid-way (network issue, API limit), simply re-run with the same arguments:
```bash
# First run - uploads 100 messages, then fails
dotnet run -- --chat-file ./chat.txt --sender-filter "John" --doc-id "ABC123" --state-dir ./state

# Second run - automatically resumes from message 101
dotnet run -- --chat-file ./chat.txt --sender-filter "John" --doc-id "ABC123" --state-dir ./state
```

**Incremental updates**: Process new messages from updated chat exports:
```bash
# Monday - export and upload
dotnet run -- --chat-file ./monday-export.txt --sender-filter "John" --doc-id "ABC123" --state-dir ./state

# Friday - export again with new messages
dotnet run -- --chat-file ./friday-export.txt --sender-filter "John" --doc-id "ABC123" --state-dir ./state
# Only new messages are uploaded; previously processed ones are skipped
```

**Multiple documents/senders**: Each combination gets its own state file in the same directory:
```bash
# Family chat - one sender
dotnet run -- --chat-file ./family.txt --sender-filter "Mom" --doc-id "DOC1" --state-dir ./state
# Creates: ./state/doc1__mom.json

# Work chat - different sender
dotnet run -- --chat-file ./work.txt --sender-filter "Boss" --doc-id "DOC2" --state-dir ./state
# Creates: ./state/doc2__boss.json

# Both state files coexist in the same directory
```

**State file cleanup**: Delete state files to force reprocessing:
```bash
# Windows - delete all state files
del state\*.json

# macOS/Linux - delete all state files
rm state/*.json

# Or delete a specific state file
rm state/doc1__mom.json
```

#### Important Notes

- **Automatic filenames**: State filenames are automatically generated from document ID and sender filter
- **Different filters**: Using a different `--sender-filter` or `--doc-id` creates a separate state file
- **Manual state management**: State files are plain JSON and can be manually edited if needed
- **No state file**: If a state file doesn't exist, all messages are processed as new
- **Directory required**: You must always specify `--state-dir` - there is no default location

## Troubleshooting

### Common Issues

#### "Credential file not found"

**Problem**: The application can't locate your Google service account JSON file.

**Solution**:
1. Verify the path in `appsettings.json` is correct
2. Use absolute paths to avoid confusion
   - Windows: `C:\\Users\\YourUsername\\.credentials\\google-service-account.json`
   - macOS: `/Users/yourusername/.credentials/google-service-account.json`
3. Check file permissions - the application must be able to read the file

#### "Permission denied" when accessing Google Docs

**Problem**: The service account doesn't have access to the Google Doc.

**Solution**:
1. Open your Google Doc
2. Click "Share"
3. Add the service account email (found in the JSON key file under `client_email`)
4. Grant "Editor" permissions
5. Click "Share"

#### "Google Docs API has not been used in project"

**Problem**: The Google Docs API isn't enabled for your project.

**Solution**:
1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Navigate to "APIs & Services" > "Library"
3. Search for "Google Docs API"
4. Click "Enable"

#### File Path Issues on Windows

**Problem**: Backslashes in paths aren't being interpreted correctly.

**Solution**:
- Use double backslashes in JSON: `C:\\Users\\...`
- Or use forward slashes: `C:/Users/...`
- Or use the `@` prefix in PowerShell: `@"C:\Users\..."@`

#### "Unable to parse chat file"

**Problem**: The WhatsApp export format isn't recognized.

**Solution**:
1. Export your WhatsApp chat without media (text only)
2. Ensure the export uses one of these date formats:
   - `DD/MM/YYYY, HH:mm` (e.g., `25/12/2024, 09:15`)
   - `M/D/YY, H:mm AM/PM` (e.g., `12/25/24, 9:15 AM`)
3. Check the sample files in `tests/SampleData/` for reference formats

### Checking Logs

Logs are written to the path specified in `appsettings.json` (default: `./logs/whatsapp-archiver.log`)

**Windows:**
```powershell
Get-Content .\logs\whatsapp-archiver.log -Tail 50
```

**macOS:**
```bash
tail -f ./logs/whatsapp-archiver.log
```

## Project Structure

```
lappers-posts/
├── src/
│   ├── WhatsAppArchiver.Domain/         # Domain entities and business logic
│   ├── WhatsAppArchiver.Application/    # Application services and handlers
│   ├── WhatsAppArchiver.Infrastructure/ # External integrations (Google Docs, file I/O)
│   └── WhatsAppArchiver.Console/        # Console application entry point
├── tests/
│   ├── WhatsAppArchiver.Domain.Tests/
│   ├── WhatsAppArchiver.Application.Tests/
│   ├── WhatsAppArchiver.Infrastructure.Tests/
│   └── SampleData/                      # Sample WhatsApp chat exports
├── .github/
│   ├── agents/                          # Custom agent definitions
│   ├── instructions/                    # Coding guidelines
│   └── prompts/                         # Development prompts
└── README.md
```

## Development

### Build and Test

```bash
# Restore dependencies
dotnet restore src/WhatsAppArchiver.sln

# Build the solution
dotnet build src/WhatsAppArchiver.sln --no-restore

# Run all tests
dotnet test src/WhatsAppArchiver.sln --no-build

# Format code
dotnet format src/WhatsAppArchiver.sln

# Build with warnings as errors
dotnet build src/WhatsAppArchiver.sln --no-restore /warnaserror
```

### Testing and Code Coverage

#### Running Tests

```bash
# Run all tests with detailed output
dotnet test src/WhatsAppArchiver.sln --verbosity normal

# Run tests for a specific project
dotnet test tests/WhatsAppArchiver.Domain.Tests/WhatsAppArchiver.Domain.Tests.csproj

# Run tests matching a filter
dotnet test --filter "FullyQualifiedName~ChatMessage"
```

#### Code Coverage

The project uses **Coverlet** for code coverage collection with a target of **85% coverage** for the Domain and Application layers.

**Collect coverage during test execution:**

```bash
# Generate coverage in multiple formats (Cobertura, JSON, LCOV, OpenCover)
dotnet test --collect:"XPlat Code Coverage"

# Coverage reports are generated in: tests/{Project}/TestResults/{guid}/coverage.cobertura.xml
```

**Coverage configuration** is defined in `.coverletrc.json`:
- **Target**: 85% line, branch, and method coverage
- **Included assemblies**: `WhatsAppArchiver.Domain`, `WhatsAppArchiver.Application`
- **Excluded assemblies**: Test projects, Console project, Program.cs
- **Formats**: Cobertura, JSON, LCOV, OpenCover

**View coverage reports:**

```bash
# Install the ReportGenerator tool (one-time setup)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML coverage report
reportgenerator \
  -reports:"tests/**/TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# Open the report (macOS)
open coveragereport/index.html

# Open the report (Windows)
start coveragereport/index.html

# Open the report (Linux)
xdg-open coveragereport/index.html
```

**Coverage threshold validation:**

```bash
# The build will fail if coverage drops below 85% for Domain/Application layers
dotnet test --collect:"XPlat Code Coverage" /p:Threshold=85 /p:ThresholdType=line
```

#### Test Organization

Tests are organized by layer, following the same structure as the source code:

```
tests/
├── WhatsAppArchiver.Domain.Tests/       # Domain logic tests (target: 85%+ coverage)
│   ├── Entities/                        # Entity behavior tests
│   ├── ValueObjects/                    # Value object immutability tests
│   ├── Specifications/                  # Business rule validation tests
│   └── Formatters/                      # Message formatting tests
│
├── WhatsAppArchiver.Application.Tests/  # Application layer tests (target: 85%+ coverage)
│   ├── Commands/                        # Command validation tests
│   └── Handlers/                        # Handler orchestration tests
│
├── WhatsAppArchiver.Infrastructure.Tests/  # Infrastructure integration tests
│   ├── Parsers/                         # File parsing tests with sample data
│   ├── Repositories/                    # State persistence tests
│   └── GoogleDocs/                      # Google Docs integration tests (mocked)
│
└── SampleData/                          # Sample WhatsApp exports for testing
    ├── sample-dd-mm-yyyy.txt           # European date format
    ├── sample-m-d-yy.txt               # US date format
    ├── sample-edge-cases.txt           # Multi-line, emojis, special chars
    └── sample-with-errors.txt          # Invalid format lines
```

#### Test Naming Convention

All tests follow the pattern: `MethodName_Condition_ExpectedResult()`

```csharp
[Fact]
public void Parse_ValidEuropeanFormat_ReturnsMessages()
{
    // Arrange
    var parser = new WhatsAppTextFileParser();
    
    // Act
    var result = await parser.ParseAsync("sample-dd-mm-yyyy.txt");
    
    // Assert
    result.Messages.Should().NotBeEmpty();
}
```

#### Running Specific Test Categories

```bash
# Run only Domain tests
dotnet test tests/WhatsAppArchiver.Domain.Tests/

# Run only Application tests  
dotnet test tests/WhatsAppArchiver.Application.Tests/

# Run only Infrastructure tests
dotnet test tests/WhatsAppArchiver.Infrastructure.Tests/

# Run all unit tests (Domain + Application)
dotnet test --filter "FullyQualifiedName~Domain|FullyQualifiedName~Application"
```

#### Continuous Integration

The CI pipeline runs:
1. **Build**: Compile all projects with warnings as errors
2. **Tests**: Execute all tests with code coverage collection
3. **Coverage Check**: Validate 85% coverage threshold for Domain/Application
4. **Format Check**: Ensure code follows formatting standards
5. **Security Scan**: Run CodeQL analysis for vulnerabilities

### Architecture

This application follows Domain-Driven Design (DDD) principles with clean separation of concerns. For a detailed explanation of each layer and its responsibilities, see the [Architecture Overview](#architecture-overview) section above.

**Layer Dependencies:**
```
Console Layer
    ↓ depends on
Application Layer
    ↓ depends on
Domain Layer (no dependencies)

Infrastructure Layer → implements interfaces from → Application/Domain Layers
```

**Design Principles:**
- **Dependency Inversion**: High-level modules don't depend on low-level modules; both depend on abstractions
- **Separation of Concerns**: Each layer has a single, well-defined responsibility
- **Testability**: Business logic is isolated and can be tested without external dependencies
- **Domain-Centric**: The domain model is the heart of the application, free from technical concerns

### Key Technologies

- **.NET 10**: Latest .NET runtime
- **Google.Apis.Docs.v1**: Google Docs API client
- **Polly**: Resilience and retry policies
- **Serilog**: Structured logging
- **System.CommandLine**: Command-line parsing (Wave 5)
- **xUnit**: Testing framework

## Contributing

This project follows strict DDD and SOLID principles. Please review:
- `.github/instructions/csharp.instructions.md` - C# coding guidelines
- `.github/instructions/dotnet-architecture-good-practices.instructions.md` - DDD architecture guidelines

## License

[License information to be added]

## Support

For issues, questions, or contributions, please open an issue in the GitHub repository.
