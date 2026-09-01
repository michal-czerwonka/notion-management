using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NotionManagementFunctionApp;

public sealed class ApplicationInsightsTelemetryFilter : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;

    public ApplicationInsightsTelemetryFilter(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry or DependencyTelemetry)
        {
            return;
        }

        _next.Process(item);
    }
}
