using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotionManagementFunctionApp.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<TaskConfigLoader>();
        services.AddSingleton<SchedulerClock>();
        services.AddSingleton<NotionTaskRunner>();
        services.AddHttpClient<NotionClient>();
    })
    .Build();

host.Run();
