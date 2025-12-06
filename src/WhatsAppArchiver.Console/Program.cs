using Microsoft.Extensions.Configuration;
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

    var builder = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            config.AddJsonFile(
                $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true);
            config.AddEnvironmentVariables();
        })
        .UseSerilog((context, services, loggerConfiguration) =>
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

            // Register WhatsAppTextFileParser with injected logger for diagnostics
            services.AddSingleton<IChatParser>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WhatsAppTextFileParser>>();
                return new WhatsAppTextFileParser(logger);
            });
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

            // Register processing state service with configured base path
            services.AddSingleton<IProcessingStateService>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JsonFileStateRepository>>();

                return new JsonFileStateRepository(stateRepositoryBasePath, logger);
            });

            // Register handlers as scoped services
            services.AddScoped<ParseChatCommandHandler>();
            services.AddScoped<UploadToGoogleDocsCommandHandler>();
        });

    using var host = builder.Build();
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
