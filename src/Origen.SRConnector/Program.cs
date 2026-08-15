using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.Api;
using Origen.SRConnector.Infrastructure.Persistence;
using Origen.SRConnector.Infrastructure.SoftRestaurant;
using Origen.SRConnector.Services;
using Origen.SRConnector.Workers;

if (args.Length != 1 || args[0] is not ("run" or "test-sql"))
{
    Console.Error.WriteLine("Usage: origen-sr-connector <run|test-sql>");
    return 2;
}

var command = args[0];
var builder = Host.CreateApplicationBuilder(args);
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
