using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
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
                ?? throw new InvalidOperationException("GoogleDocs:CredentialFilePath is not configured.");
            var stateRepositoryBasePath = context.Configuration["WhatsAppArchiver:StateRepository:BasePath"]
                ?? throw new InvalidOperationException("StateRepository:BasePath is not configured.");

            // Register singleton services (stateless)
            services.AddSingleton<IChatParser, WhatsAppTextFileParser>();
            services.AddSingleton<IGoogleDocsClientFactory, GoogleDocsClientFactory>();

            // Register formatters as singletons (stateless)
            services.AddSingleton<IMessageFormatter, DefaultMessageFormatter>();
            services.AddSingleton<IMessageFormatter, CompactMessageFormatter>();
            services.AddSingleton<IMessageFormatter, VerboseMessageFormatter>();

            // Register Google Docs service with configured credential path
            services.AddSingleton<IGoogleDocsService>(sp =>
            {
                var clientFactory = sp.GetRequiredService<IGoogleDocsClientFactory>();

                return new GoogleDocsServiceAccountAdapter(googleDocsCredentialPath, clientFactory);
            });

            // Register processing state service with configured base path
            services.AddSingleton<IProcessingStateService>(sp =>
            {
                var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<JsonFileStateRepository>>();

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
