using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TennisSlotWatcher;
using TennisSlotWatcher.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// The Application Insights provider ships with a default rule that drops everything below Warning
// from the worker; remove it so Information logs (slot lists, summaries) reach App Insights too.
builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
    var rule = options.Rules.FirstOrDefault(r =>
        r.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (rule is not null) options.Rules.Remove(rule);
});

builder.Services.AddOptions<WatcherOptions>().BindConfiguration(WatcherOptions.SectionName);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient<PlayFiClient>(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(30);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("TennisSlotWatcher/1.0 (personal availability alert)");
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fi,en;q=0.8");
    })
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<INotifier, NtfyNotifier>(c => c.Timeout = TimeSpan.FromSeconds(15))
    .AddStandardResilienceHandler();

// State lives in the Function App's own storage account (AzureWebJobsStorage).
// Supports both a connection string and identity-based config (AzureWebJobsStorage__blobServiceUri).
builder.Services.AddSingleton(sp =>
{
    // Pin the REST API version: the SDK defaults to the newest one, which local Azurite (and older Visual Studio
    // bundles) may not support. Only basic blob operations are used, so an older version is fine in Azure too.
    var blobOptions = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04);
    var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var connectionString = config["AzureWebJobsStorage"];
    if (!string.IsNullOrEmpty(connectionString))
        return new BlobServiceClient(connectionString, blobOptions);

    var uri = config["AzureWebJobsStorage:blobServiceUri"]
              ?? (config["AzureWebJobsStorage:accountName"] is { Length: > 0 } account
                  ? $"https://{account}.blob.core.windows.net"
                  : null)
              ?? throw new InvalidOperationException("AzureWebJobsStorage is not configured.");
    return new BlobServiceClient(new Uri(uri), new DefaultAzureCredential(), blobOptions);
});
builder.Services.AddSingleton<ISlotStateStore, BlobSlotStateStore>();
builder.Services.AddSingleton<SlotCheckService>();

builder.Build().Run();
