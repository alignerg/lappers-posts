using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WhatsAppArchiver.Application.Commands;
using WhatsAppArchiver.Application.Handlers;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Infrastructure;
using WhatsAppArchiver.Infrastructure.Parsers;
using WhatsAppArchiver.Infrastructure.Repositories;

/// <summary>
/// Builds and configures the application host with dependency injection and services.
/// </summary>
/// <param name="args">Command-line arguments.</param>
/// <param name="customConfigFile">Optional custom configuration file to use instead of default appsettings.json.</param>
/// <returns>The configured host.</returns>
/// <remarks>
/// This method creates the host with all necessary services registered, including:
/// - Serilog logging
/// - Chat parser
/// - Google Docs service
/// - Processing state service
/// - Command handlers
/// If a custom configuration file is provided, it will be used instead of the default appsettings.json.
/// </remarks>
static IHost BuildHost(string[] args, FileInfo? customConfigFile)
{
    var builder = Host.CreateDefaultBuilder(args);

    // Configure custom appsettings.json if provided
    if (customConfigFile is not null)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Replace only the default appsettings.json sources with the custom config, keep other sources (e.g., user secrets)
            config.Sources.RemoveAll(s => s is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource);
            config.AddJsonFile(customConfigFile.FullName, optional: false, reloadOnChange: false);
            config.AddEnvironmentVariables();
            if (args.Length > 0)
            {
                config.AddCommandLine(args);
            }
        });
    }

    builder.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(context.Configuration);
        })
        .ConfigureServices((context, services) =>
        {
            // Retrieve configuration values
            var googleDocsCredentialPath = context.Configuration["WhatsAppArchiver:GoogleDocs:CredentialFilePath"]
                ?? throw new InvalidOperationException("Configuration key 'WhatsAppArchiver:GoogleDocs:CredentialFilePath' is not configured.");
            var stateRepositoryBasePath = context.Configuration["WhatsAppArchiver:StateRepository:BasePath"]
                ?? throw new InvalidOperationException("Configuration key 'WhatsAppArchiver:StateRepository:BasePath' is not configured.");

            // Register WhatsAppTextFileParser as Singleton because it's stateless and thread-safe.
            // The parser only reads files and doesn't maintain any mutable state between operations.
            services.AddSingleton<IChatParser>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WhatsAppTextFileParser>>();
                return new WhatsAppTextFileParser(logger);
            });

            // GoogleDocsClientFactory is stateless and can be shared across all usages.
            services.AddSingleton<IGoogleDocsClientFactory, GoogleDocsClientFactory>();

            // Note: IMessageFormatter implementations are created via FormatterFactory.Create()
            // which is a static factory method. The handlers use this factory directly,
            // so individual formatter registrations are not needed for DI resolution.

            // Register Google Docs service as Scoped to ensure proper disposal of IDisposable resources.
            // GoogleDocsServiceAccountAdapter holds unmanaged resources (Google API clients) that need
            // to be disposed properly. Scoped lifetime ensures disposal after each usage scope.
            services.AddScoped<IGoogleDocsService>(sp =>
            {
                var clientFactory = sp.GetRequiredService<IGoogleDocsClientFactory>();

                return new GoogleDocsServiceAccountAdapter(googleDocsCredentialPath, clientFactory);
            });

            // Register JsonFileStateRepository as Singleton because it's stateless and thread-safe.
            // The repository handles file I/O atomically and doesn't maintain mutable state.
            services.AddSingleton<IProcessingStateService>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JsonFileStateRepository>>();

                return new JsonFileStateRepository(stateRepositoryBasePath, logger);
            });

            // Register handlers as Scoped services to align with Scoped dependencies (IGoogleDocsService).
            // Scoped lifetime ensures handlers are created per logical operation/scope,
            // which is appropriate for command handlers that orchestrate business operations.
            services.AddScoped<ParseChatCommandHandler>();
            services.AddScoped<UploadToGoogleDocsCommandHandler>();
        });

    return builder.Build();
}

/// <summary>
/// Executes the upload command by creating a service scope, resolving the handler,
/// and processing the chat file.
/// </summary>
/// <param name="host">The configured host containing registered services.</param>
/// <param name="chatFile">The WhatsApp chat export file to process.</param>
/// <param name="sender">The sender name to filter messages by.</param>
/// <param name="documentId">The Google Docs document ID to upload messages to.</param>
/// <param name="formatType">The message formatting type to apply.</param>
/// <param name="stateFile">Optional custom path for the processing state file.</param>
/// <param name="configFile">Optional custom configuration file used to build the host.</param>
/// <returns>A task representing the asynchronous operation.</returns>
/// <remarks>
/// This method uses dependency injection to resolve the <see cref="UploadToGoogleDocsCommandHandler"/>
/// from the host's service provider. It creates a service scope to ensure proper disposal
/// of scoped services like <see cref="IGoogleDocsService"/>.
/// Note: The state file path is only used for logging purposes. The actual state repository location is determined by the
/// <c>WhatsAppArchiver:StateRepository:BasePath</c> configuration setting. The <paramref name="stateFile"/> parameter and
/// <c>--state-file</c> CLI option do not affect where state is actually stored.
/// </remarks>
static async Task ExecuteUploadCommandAsync(
    IHost host,
    FileInfo chatFile,
    string sender,
    string documentId,
    MessageFormatType formatType,
    FileInfo? stateFile,
    FileInfo? configFile)
{
    try
    {
        // Derive default state file path if not provided
        var stateFilePath = stateFile?.FullName
            ?? Path.Combine(
                chatFile.DirectoryName ?? Environment.CurrentDirectory,
                "processingState.json");

        Log.Information(
            "Processing chat file: {ChatFile}, Sender: {Sender}, Document: {DocumentId}, Format: {Format}, State: {StateFile}",
            chatFile.FullName,
            sender,
            documentId,
            formatType,
            stateFilePath);

        // Create a service scope to ensure proper disposal of scoped services
        await using var scope = host.Services.CreateAsyncScope();

        // Resolve the handler from the service provider
        var handler = scope.ServiceProvider.GetRequiredService<UploadToGoogleDocsCommandHandler>();

        // Create the command with the provided parameters
        var command = new UploadToGoogleDocsCommand(
            FilePath: chatFile.FullName,
            Sender: sender,
            DocumentId: documentId,
            FormatterType: formatType);

        // Execute the handler with cancellation support
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (_, e) =>
        {
            Log.Warning("Cancellation requested by user");
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var messagesUploaded = await handler.HandleAsync(command, cts.Token);

            Log.Information(
                "Successfully uploaded {Count} message(s) to Google Docs document {DocumentId}",
                messagesUploaded,
                documentId);

            if (messagesUploaded == 0)
            {
                Log.Information("No new messages to upload. All messages have been previously processed");
            }
        }
        finally
        {
            if (cancelHandler is not null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        if (messagesUploaded == 0)
        {
            Log.Information("No new messages to upload. All messages have been previously processed");
        }
    }
    catch (OperationCanceledException)
    {
        Log.Warning("Operation was cancelled");
        return;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to execute upload command");
        return;
    }
}


// Configure Serilog early to capture startup logs
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting WhatsApp Archiver application");

    // Configure CLI using System.CommandLine
    var rootCommand = new RootCommand("WhatsApp Archiver - Upload chat messages to Google Docs");

    var chatFileOption = new Option<FileInfo>(
        "--chat-file",
        "Path to the WhatsApp chat export file")
    {
        IsRequired = true
    };
    chatFileOption.AddValidator(result =>
    {
        var file = result.GetValueOrDefault<FileInfo>();
        if (file is null || !file.Exists)
        {
            result.ErrorMessage = $"Chat file does not exist: {file?.FullName ?? "null"}";
        }
    });

    var senderFilterOption = new Option<string>(
        "--sender-filter",
        "Filter messages by sender name (case-insensitive)")
    {
        IsRequired = true
    };
    senderFilterOption.AddValidator(result =>
    {
        var sender = result.GetValueOrDefault<string>();
        if (string.IsNullOrWhiteSpace(sender))
        {
            result.ErrorMessage = "Sender filter cannot be empty or whitespace";
        }
    });

    var docIdOption = new Option<string>(
        "--doc-id",
        "Google Docs document ID")
    {
        IsRequired = true
    };
    docIdOption.AddValidator(result =>
    {
        var docId = result.GetValueOrDefault<string>();
        if (string.IsNullOrWhiteSpace(docId))
        {
            result.ErrorMessage = "Document ID cannot be empty or whitespace";
        }
        else if (docId.Contains('/') || docId.Contains('\\'))
        {
            result.ErrorMessage = "Document ID should not contain path separators. Use only the document ID, not a full URL";
        }
    });

    var formatOption = new Option<MessageFormatType>(
        "--format",
        () => MessageFormatType.Default,
        "Message format type");

    var stateFileOption = new Option<FileInfo?>(
        "--state-file",
        "Path to the processing state file (default: {chat-file-directory}/processingState.json)");
    stateFileOption.AddValidator(result =>
    {
        var stateFile = result.GetValueOrDefault<FileInfo?>();
        if (stateFile is not null)
        {
            var directory = stateFile.Directory;
            if (directory is null || !directory.Exists)
            {
                result.ErrorMessage = $"The directory for the state file does not exist: {directory?.FullName ?? stateFile.FullName}";
                return;
            }
            // Check if directory is writable by attempting to create a temp file
            try
            {
                var testFilePath = Path.Combine(directory.FullName, Path.GetRandomFileName());
                using (FileStream fs = File.Create(testFilePath, 1, FileOptions.DeleteOnClose))
                {
                    // Successfully created, directory is writable
                }
            }
            catch (Exception)
            {
                result.ErrorMessage = $"The directory for the state file is not writable: {directory.FullName}";
            }
        }
    });

    var configOption = new Option<FileInfo?>(
        "--config",
        "Path to custom appsettings.json configuration file");
    configOption.AddValidator(result =>
    {
        if (result.GetValueOrDefault<FileInfo?>() is { } configFile && !configFile.Exists)
        {
            result.ErrorMessage = $"Configuration file does not exist: {configFile.FullName}";
        }
    });

    rootCommand.Add(chatFileOption);
    rootCommand.Add(senderFilterOption);
    rootCommand.Add(docIdOption);
    rootCommand.Add(formatOption);
    rootCommand.Add(stateFileOption);
    rootCommand.Add(configOption);

    rootCommand.SetHandler(
        async (chatFile, sender, docId, format, stateFile, configFile) =>
        {
            // Build the host with custom configuration if provided
            var host = BuildHost(args, configFile);
            using (host)
            {
                await ExecuteUploadCommandAsync(
                    host, chatFile, sender, docId, format, stateFile, configFile);
            }
        },
        chatFileOption,
        senderFilterOption,
        docIdOption,
        formatOption,
        stateFileOption,
        configOption);

    var exitCode = await rootCommand.InvokeAsync(args);
    Environment.Exit(exitCode);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.Exit(1);
}
finally
{
    await Log.CloseAndFlushAsync();
}
