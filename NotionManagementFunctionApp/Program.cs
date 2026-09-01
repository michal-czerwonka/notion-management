using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotionManagementFunctionApp.CreateNotionTasks;
using NotionManagementFunctionApp.SendNotionTaskNotifications;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<TaskConfigLoader>();
        services.AddSingleton<SchedulerClock>();
        services.AddSingleton<CreateNotionTasksRunner>();
        services.AddHttpClient<NotionTasksClient>();

        services.AddSingleton<SendNotionTaskNotificationsRunner>();
        services.AddSingleton<NtfyClient>();
        services.AddHttpClient<NotionTodayTasksClient>();
    })
    .Build();

host.Run();