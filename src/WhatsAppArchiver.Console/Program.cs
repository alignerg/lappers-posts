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

    // Define command-line options with validation
    var chatFileOption = new Option<string>("--chat-file")
    {
        Description = "Path to the WhatsApp chat export file",
        Required = true
    };
    chatFileOption.Validators.Add(result =>
    {
        var filePath = result.GetValue(chatFileOption)!;
        if (filePath.Any(c => Path.GetInvalidPathChars().Contains(c)))
        {
            result.AddError("Chat file path contains invalid characters");
            return;
        }
        if (!File.Exists(filePath))
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
        if (!string.IsNullOrWhiteSpace(statePath) && statePath.Any(c => Path.GetInvalidPathChars().Contains(c)))
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
            if (configPath.Any(c => Path.GetInvalidPathChars().Contains(c)))
            {
                result.AddError("Config file path contains invalid characters");
                return;
            }
            if (!File.Exists(configPath))
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
    rootCommand.SetAction(async (ParseResult parseResult) =>
    {
        var chatFile = parseResult.GetValue(chatFileOption)!;
        var senderFilter = parseResult.GetValue(senderFilterOption)!;
        var docId = parseResult.GetValue(docIdOption)!;
        var format = parseResult.GetValue(formatOption);
        var stateFile = parseResult.GetValue(stateFileOption);
        var configFile = parseResult.GetValue(configOption);

        // Determine state file path
        var resolvedStateFile = stateFile;
        if (string.IsNullOrWhiteSpace(resolvedStateFile))
        {
            var chatFileDirectory = Path.GetDirectoryName(Path.GetFullPath(chatFile))
                ?? throw new InvalidOperationException("Unable to determine chat file directory");
            resolvedStateFile = Path.Combine(chatFileDirectory, "processingState.json");
        }

        // Build the host with optional custom configuration
        var hostBuilder = Host.CreateDefaultBuilder(args);

        if (!string.IsNullOrWhiteSpace(configFile))
        {
            hostBuilder.ConfigureAppConfiguration((hostContext, config) =>
            {
                config.Sources.Clear();
                config.AddJsonFile(configFile, optional: false, reloadOnChange: false);
                config.AddEnvironmentVariables();
            });
        }

        hostBuilder.UseSerilog((hostContext, services, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(hostContext.Configuration);
        })
        .ConfigureServices((hostContext, services) =>
        {
            // Retrieve configuration values
            var googleDocsCredentialPath = hostContext.Configuration["WhatsAppArchiver:GoogleServiceAccount:CredentialsPath"]
                ?? throw new InvalidOperationException("Configuration key 'WhatsAppArchiver:GoogleServiceAccount:CredentialsPath' is not configured.");

            // Use the resolved state file path from command-line argument
            var stateRepositoryBasePath = Path.GetDirectoryName(resolvedStateFile)
                ?? throw new InvalidOperationException("Unable to determine state repository base path");

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
        await host.StartAsync();

        try
        {
            // Execute the upload command
            using var scope = host.Services.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UploadToGoogleDocsCommandHandler>();

            var command = new UploadToGoogleDocsCommand(
                FilePath: chatFile,
                Sender: senderFilter,
                DocumentId: docId,
                FormatterType: format);

            Log.Information("Uploading messages from {ChatFile} by sender '{Sender}' to document {DocumentId} with format {Format}",
                chatFile, senderFilter, docId, format);

            var uploadedCount = await handler.HandleAsync(command);

            Log.Information("Successfully uploaded {Count} messages to Google Docs", uploadedCount);
            Console.WriteLine($"Successfully uploaded {uploadedCount} messages to Google Docs");

            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing upload command");
            return 1;
        }
        finally
        {
            await host.StopAsync();
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
