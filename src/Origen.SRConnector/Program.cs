using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.Api;
using Origen.SRConnector.Infrastructure.Persistence;
using Origen.SRConnector.Infrastructure.SoftRestaurant;
using Origen.SRConnector.Services;
using Origen.SRConnector.Workers;

if (args.Length != 1 || args[0] is not ("run" or "test-sql" or "test-api" or "status"))
{
    Console.Error.WriteLine("Usage: origen-sr-connector <run|test-sql|test-api|status>");
    return 2;
}

var command = args[0];
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    // La configuración debe resolverse junto al ejecutable, no desde el directorio
    // de trabajo elegido por PowerShell o por Windows Service.
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
// Se agrega nuevamente para que las variables de entorno tengan prioridad sobre el archivo local.
builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<SoftRestaurantOptions>()
    .Bind(builder.Configuration.GetSection(SoftRestaurantOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services
    .AddOptions<ConnectorOptions>()
    .Bind(builder.Configuration.GetSection(ConnectorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ISoftRestaurantRepository, SoftRestaurantRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISaleOutboxRepository, SqliteSaleOutboxRepository>();
builder.Services.AddSingleton<ILoyaltyApiClient, MockLoyaltyApiClient>();
builder.Services.AddSingleton<ISaleSyncService, SaleSyncService>();
builder.Services.AddSingleton<IOutboxDispatchService, OutboxDispatchService>();
builder.Services.AddSingleton<ConnectorStatusService>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Origen SR Connector";
});

if (command == "run")
{
    builder.Services.AddHostedService<SalePollingWorker>();
    builder.Services.AddHostedService<OutboxDispatchWorker>();
}

try
{
    using var host = builder.Build();
    if (command == "test-sql")
    {
        await host.Services.GetRequiredService<ISoftRestaurantRepository>()
            .TestConnectionAsync(CancellationToken.None);
        return 0;
    }

    if (command == "test-api")
    {
        var result = await host.Services.GetRequiredService<ILoyaltyApiClient>()
            .TestConnectionAsync(CancellationToken.None);
        Console.WriteLine(result.Success
            ? "API client: Mock enabled (HTTP disabled; no request was made)"
            : $"API client test failed: {result.Error}");
        return result.Success ? 0 : 1;
    }

    if (command == "status")
    {
        var status = await host.Services.GetRequiredService<ConnectorStatusService>()
            .GetStatusAsync(CancellationToken.None);
        Console.WriteLine($"SQL Server: {status.SqlServer}");
        Console.WriteLine($"API: {status.Api}");
        Console.WriteLine($"Pending sales: {status.Outbox.Counts.Pending}");
        Console.WriteLine($"Sending sales: {status.Outbox.Counts.Sending}");
        Console.WriteLine($"Failed sales: {status.Outbox.Counts.Failed}");
        Console.WriteLine($"Sent sales: {status.Outbox.Counts.Sent}");
        Console.WriteLine($"Last sale detected: {status.Outbox.LastTicketDetected?.ToString() ?? "None"}");
        Console.WriteLine($"Last successful sync: {status.Outbox.LastSuccessfulSync?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}");
        return status.SqlServer == "Connected" ? 0 : 1;
    }

    await host.RunAsync();
    return 0;
}
catch (OptionsValidationException exception)
{
    Console.Error.WriteLine($"Invalid configuration: {string.Join("; ", exception.Failures)}");
    return 2;
}
catch (Exception exception)
{
    // No se imprime la cadena de conexión ni otros secretos.
    Console.Error.WriteLine($"Command failed: {exception.Message}");
    return 1;
}
