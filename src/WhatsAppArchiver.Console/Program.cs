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

// Configure Serilog early to capture startup logs
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting WhatsApp Archiver application");

    // Cache invalid path characters for efficient validation
    var invalidPathChars = new HashSet<char>(Path.GetInvalidPathChars());

    // Helper method to validate if a path contains invalid characters
    bool ContainsInvalidPathChars(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        return path.Any(invalidPathChars.Contains);
    }

    // Define command-line options with validation
    var chatFileOption = new Option<string>("--chat-file")
    {
        Description = "Path to the WhatsApp chat export file",
        Required = true
    };
    chatFileOption.Validators.Add(result =>
    {
        var filePath = result.GetValue(chatFileOption)!;
        if (ContainsInvalidPathChars(filePath))
        {
            result.AddError("Chat file path contains invalid characters");
            return;
        }
        // Expand tilde before checking file existence
        var expandedPath = PathUtilities.ExpandTildePath(filePath);
        if (!File.Exists(expandedPath))
        {
            result.AddError($"Chat file does not exist: {filePath}");
        }
    });

    var senderFilterOption = new Option<string>("--sender-filter")
    {
        Description = "Name of the sender to filter messages by",
        Required = true
    };
    senderFilterOption.Validators.Add(result =>
    {
        var sender = result.GetValue(senderFilterOption);
        if (string.IsNullOrWhiteSpace(sender))
        {
            result.AddError("Sender filter cannot be empty");
        }
    });

    var docIdOption = new Option<string>("--doc-id")
    {
        Description = "Google Docs document ID",
        Required = true
    };
    docIdOption.Validators.Add(result =>
    {
        var docId = result.GetValue(docIdOption);
        if (string.IsNullOrWhiteSpace(docId))
        {
            result.AddError("Document ID cannot be empty");
        }
    });

    var formatOption = new Option<MessageFormatType>("--format")
    {
        Description = "Message format type (default|compact|verbose)"
    };
    formatOption.DefaultValueFactory = _ => MessageFormatType.Default;

    var stateFileOption = new Option<string?>("--state-file")
    {
        Description = "Path to the processing state file (defaults to processingState.json in the chat file directory)"
    };
    stateFileOption.Validators.Add(result =>
    {
        var statePath = result.GetValue(stateFileOption);
        if (ContainsInvalidPathChars(statePath))
        {
            result.AddError("State file path contains invalid characters");
        }
    });

    var configOption = new Option<string?>("--config")
    {
        Description = "Path to the configuration file (appsettings.json)"
    };
    configOption.Validators.Add(result =>
    {
        var configPath = result.GetValue(configOption);
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            if (ContainsInvalidPathChars(configPath))
            {
                result.AddError("Config file path contains invalid characters");
                return;
            }
            // Expand tilde before checking file existence
            var expandedPath = PathUtilities.ExpandTildePath(configPath);
            if (!File.Exists(expandedPath))
            {
                result.AddError($"Config file does not exist: {configPath}");
            }
        }
    });

    // Define root command
    var rootCommand = new RootCommand("WhatsApp Archiver - Upload WhatsApp chat messages to Google Docs");
    rootCommand.Add(chatFileOption);
    rootCommand.Add(senderFilterOption);
    rootCommand.Add(docIdOption);
    rootCommand.Add(formatOption);
    rootCommand.Add(stateFileOption);
    rootCommand.Add(configOption);

    // Set the handler using ParseResult
    rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
    {
        var chatFile = parseResult.GetValue(chatFileOption)!;
        var senderFilter = parseResult.GetValue(senderFilterOption)!;
        var docId = parseResult.GetValue(docIdOption)!;
        var format = parseResult.GetValue(formatOption);
        var stateFile = parseResult.GetValue(stateFileOption);
        var configFile = parseResult.GetValue(configOption);

        // Expand tilde (~) in file paths if present
        // chatFile is required and non-null, so expand unconditionally
        chatFile = PathUtilities.ExpandTildePath(chatFile)!;
        // configFile is optional, so only expand if provided
        if (!string.IsNullOrWhiteSpace(configFile))
        {
            configFile = PathUtilities.ExpandTildePath(configFile);
        }

        // Determine state file path
        var resolvedStateFile = stateFile;
        if (string.IsNullOrWhiteSpace(resolvedStateFile))
        {
            var chatFileFullPath = Path.GetFullPath(chatFile!);
            var chatFileDirectory = Path.GetDirectoryName(chatFileFullPath);
            if (string.IsNullOrEmpty(chatFileDirectory))
            {
                chatFileDirectory = Directory.GetCurrentDirectory();
            }
            resolvedStateFile = Path.Combine(chatFileDirectory, "processingState.json");
        }
        else
        {
            // Expand tilde (~) in state file path if present
            resolvedStateFile = PathUtilities.ExpandTildePath(resolvedStateFile);
        }

        // Build the host with optional custom configuration
        var hostBuilder = Host.CreateDefaultBuilder(Array.Empty<string>());

        if (!string.IsNullOrWhiteSpace(configFile))
        {
            hostBuilder.ConfigureAppConfiguration((hostContext, config) =>
            {
                // Add the custom config file without clearing existing sources to preserve standard .NET configuration precedence:
                // appsettings.json < appsettings.{Environment}.json < user secrets < custom config file < environment variables < command-line args
                config.AddJsonFile(configFile, optional: false, reloadOnChange: false);
            });
        }

        hostBuilder.UseSerilog((hostContext, services, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(hostContext.Configuration);
        })
        .ConfigureServices((hostContext, services) =>
        {
            // Retrieve configuration values
            var googleDocsCredentialPath = hostContext.Configuration["WhatsAppArchiver:GoogleServiceAccount:CredentialsPath"];
            if (string.IsNullOrWhiteSpace(googleDocsCredentialPath))
            {
                throw new InvalidOperationException("Configuration key 'WhatsAppArchiver:GoogleServiceAccount:CredentialsPath' is not configured.");
            }

            // Expand tilde (~) in credential path if present
            googleDocsCredentialPath = PathUtilities.ExpandTildePath(googleDocsCredentialPath)!;

            // Use the resolved state file path from command-line argument
            var stateRepositoryBasePath = Path.GetDirectoryName(resolvedStateFile);
            if (string.IsNullOrEmpty(stateRepositoryBasePath))
            {
                // If no directory component is present, use the current directory as fallback.
                stateRepositoryBasePath = Directory.GetCurrentDirectory();
            }

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

        using var host = hostBuilder.Build();
        await host.StartAsync(cancellationToken);

        try
        {
            // Generate a correlation ID for tracking this operation
            using var correlationIdScope = Serilog.Context.LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString("N")[..8]);

            using var scope = host.Services.CreateScope();
            var chatParser = scope.ServiceProvider.GetRequiredService<IChatParser>();

            // Step 1: Parse entire chat file into memory
            Log.Information("Starting to parse chat file: {ChatFile}", chatFile);
            var chatExport = await chatParser.ParseAsync(chatFile!, timeZoneOffset: null, cancellationToken);

            // Step 2: Validate parsing results
            if (chatExport.Metadata.FailedLineCount > 0)
            {
                var errorRate = (double)chatExport.Metadata.FailedLineCount / chatExport.Metadata.TotalLines * 100;
                Log.Error("Parsing failed with {FailedLineCount} errors out of {TotalLines} total lines ({ErrorRate:F2}% error rate)",
                    chatExport.Metadata.FailedLineCount,
                    chatExport.Metadata.TotalLines,
                    errorRate);

                Console.Error.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
                Console.Error.WriteLine("║                         PARSING ERROR REPORT                         ║");
                Console.Error.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Source File:         {chatExport.Metadata.SourceFileName}");
                Console.Error.WriteLine($"Total Lines:         {chatExport.Metadata.TotalLines}");
                Console.Error.WriteLine($"Parsed Messages:     {chatExport.Metadata.ParsedMessageCount}");
                Console.Error.WriteLine($"Failed Lines:        {chatExport.Metadata.FailedLineCount}");
                Console.Error.WriteLine($"Error Rate:          {errorRate:F2}%");
                Console.Error.WriteLine();
                Console.Error.WriteLine("The chat file contains lines that could not be parsed.");
                Console.Error.WriteLine("Please review the file format and ensure it matches the expected WhatsApp");
                Console.Error.WriteLine("export format (DD/MM/YYYY, HH:mm:ss or M/D/YY, H:mm AM/PM).");
                Console.Error.WriteLine();

                return 1;
            }

            // Step 3: Log successful parse
            Log.Information("Successfully parsed chat file: {ParsedMessageCount} messages from {TotalLines} lines",
                chatExport.Metadata.ParsedMessageCount,
                chatExport.Metadata.TotalLines);

            // Step 4: Upload formatted messages to Google Docs
            var uploadHandler = scope.ServiceProvider.GetRequiredService<UploadToGoogleDocsCommandHandler>();

            var command = new UploadToGoogleDocsCommand(
                FilePath: chatFile,
                Sender: senderFilter,
                DocumentId: docId,
                FormatterType: format,
                CachedChatExport: chatExport);

            Log.Information("Uploading messages from {ChatFile} by sender '{Sender}' to document {DocumentId} with format {Format}",
                chatFile, senderFilter, docId, format);

            var uploadedCount = await uploadHandler.HandleAsync(command, cancellationToken);

            Log.Information("Successfully uploaded {Count} messages to Google Docs", uploadedCount);
            Console.WriteLine($"Successfully uploaded {uploadedCount} messages to Google Docs");

            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log.Warning("Operation cancelled by user");
            Console.WriteLine("Operation cancelled");
            return 130; // Standard exit code for Ctrl+C
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing upload command");
            return 1;
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    });

    // Invoke the command
    var result = rootCommand.Parse(args);
    return await result.InvokeAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
