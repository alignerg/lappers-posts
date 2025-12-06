# WhatsApp Archiver

A .NET 10 console application that parses WhatsApp text exports, filters messages by sender, and uploads them to Google Docs using service account authentication. Built with Domain-Driven Design (DDD) principles and SOLID architecture.

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
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

## Features

- Parse WhatsApp chat export text files with multiple date/time formats
- Filter messages by sender (case-insensitive)
- Multiple message formatting options (default, compact, verbose)
- Idempotent processing with state tracking
- Upload to Google Docs using service account authentication
- Resilient operations with automatic retry policies
- Structured logging with Serilog

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Google Cloud Platform account
- A Google Docs document ID where you want to upload the messages

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
    "StateRepository": {
      "BasePath": "./state"
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

- **BasePath**: Directory where processing state files will be stored
  - The application creates this directory if it doesn't exist
  - State files enable resumable operations and prevent duplicate uploads

- **DefaultFormatter**: Message formatting style
  - `default`: `[{timestamp}] {sender}: {content}`
  - `compact`: `{sender}: {content}`
  - `verbose`: Detailed format with full date/time and metadata

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

# Run the application (to be implemented in Wave 5)
.\WhatsAppArchiver.Console.exe
```

#### Option 2: Using dotnet run

```powershell
# From the repository root
cd src\WhatsAppArchiver.Console

# Run with dotnet
dotnet run --configuration Release
```

#### Example with Full Paths (Windows)

```powershell
# Using the executable
cd C:\Projects\lappers-posts\src\WhatsAppArchiver.Console\bin\Release\net10.0
.\WhatsAppArchiver.Console.exe

# Using dotnet run
cd C:\Projects\lappers-posts\src\WhatsAppArchiver.Console
dotnet run --configuration Release
```

### macOS

#### Option 1: Using the Published Executable

```bash
# Navigate to the build output directory
cd src/WhatsAppArchiver.Console/bin/Release/net10.0

# Make the executable runnable (if needed)
chmod +x WhatsAppArchiver.Console

# Run the application (to be implemented in Wave 5)
./WhatsAppArchiver.Console
```

#### Option 2: Using dotnet run

```bash
# From the repository root
cd src/WhatsAppArchiver.Console

# Run with dotnet
dotnet run --configuration Release
```

#### Example with Full Paths (macOS)

```bash
# Using the executable
cd /Users/yourusername/Projects/lappers-posts/src/WhatsAppArchiver.Console/bin/Release/net10.0
./WhatsAppArchiver.Console

# Using dotnet run
cd /Users/yourusername/Projects/lappers-posts/src/WhatsAppArchiver.Console
dotnet run --configuration Release
```

## Usage Examples

> **Note**: The command-line interface is currently being implemented in Wave 5. The examples below show the planned usage once the CLI is complete.

### Parse a WhatsApp Chat Export

```bash
# Parse chat file and filter by sender
dotnet run -- parse-chat --chat-file ./exports/chat.txt --sender "John Smith"

# Upload filtered messages to Google Docs
dotnet run -- upload --chat-file ./exports/chat.txt --sender "John Smith" --doc-id "YOUR_DOCUMENT_ID"

# Use a specific formatter
dotnet run -- upload --chat-file ./exports/chat.txt --sender "John Smith" --doc-id "YOUR_DOCUMENT_ID" --formatter compact

# Specify a custom configuration file
dotnet run -- upload --chat-file ./exports/chat.txt --sender "John Smith" --doc-id "YOUR_DOCUMENT_ID" --config ./custom-config.json
```

### Command-Line Arguments (Planned)

- `--chat-file` or `-f`: Path to the WhatsApp chat export text file
- `--sender` or `-s`: Name of the sender to filter messages by
- `--doc-id` or `-d`: Google Docs document ID
- `--formatter`: Message format (default, compact, or verbose)
- `--config` or `-c`: Path to custom configuration file

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

### Architecture

This application follows Domain-Driven Design (DDD) principles:

- **Domain Layer**: Core business entities, value objects, and specifications
- **Application Layer**: Command handlers and service interfaces
- **Infrastructure Layer**: External service adapters (Google Docs, file system)
- **Console Layer**: Application host and CLI interface

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
