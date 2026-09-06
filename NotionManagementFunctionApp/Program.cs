using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotionManagementFunctionApp;
using NotionManagementFunctionApp.CreateNotionTasks;
using NotionManagementFunctionApp.Inbox;
using NotionManagementFunctionApp.SendNotionTaskNotifications;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddApplicationInsightsTelemetryProcessor<ApplicationInsightsTelemetryFilter>();
        services.Configure<LoggerFilterOptions>(options =>
        {
            var applicationInsightsRule = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

            if (applicationInsightsRule is not null)
            {
                options.Rules.Remove(applicationInsightsRule);
            }
        });

        services.AddSingleton<TaskConfigLoader>();
        services.AddSingleton<SchedulerClock>();
        services.AddSingleton<CreateNotionTasksRunner>();
        services.AddHttpClient<NotionTasksClient>();
        services.AddHttpClient<NotionInboxClient>();

        services.AddSingleton<SendNotionTaskNotificationsRunner>();
        services.AddSingleton<NtfyClient>();
        services.AddHttpClient<NotionTodayTasksClient>();
    })
    .Build();

host.Run();
