using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WhatsAppArchiver.Application.Handlers;
using WhatsAppArchiver.Application.Services;
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

    // Host.CreateDefaultBuilder automatically loads appsettings.json, appsettings.{Environment}.json,
    // and environment variables, so no explicit ConfigureAppConfiguration is needed.
    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(context.Configuration);
        })
        .ConfigureServices((context, services) =>
        {
            // Retrieve configuration values
            var googleDocsCredentialPath = context.Configuration["WhatsAppArchiver:GoogleServiceAccount:CredentialsPath"]
                ?? throw new InvalidOperationException("Configuration key 'WhatsAppArchiver:GoogleServiceAccount:CredentialsPath' is not configured.");
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

    using var host = builder.Build();

    // TODO: Wave 5 will implement command-line interface using System.CommandLine.
    // The host currently runs and waits for shutdown signal. Command execution will be added
    // in the next wave to parse arguments and invoke the appropriate handler.
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
