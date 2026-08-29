using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotionManagementFunctionApp.CreateNotionTasks;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<TaskConfigLoader>();
        services.AddSingleton<SchedulerClock>();
        services.AddSingleton<CreateNotionTasksRunner>();
        services.AddHttpClient<NotionTasksClient>();
    })
    .Build();

host.Run();