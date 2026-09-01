using Microsoft.Extensions.Configuration;

namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed class NtfyClient
{
    private static readonly HttpClient HttpClient = new();

    private readonly string _baseUrl;
    private readonly string _topic;

    public NtfyClient(IConfiguration configuration)
    {
        _baseUrl = configuration["Ntfy:BaseUrl"] ?? "https://ntfy.sh";
        _topic = configuration["Ntfy:Topic"] ?? "";
    }

    public async Task<NtfyPublishResult> SendAsync(string message, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildTopicUri())
        {
            Content = new StringContent(message)
        };

        request.Headers.Add("Title", "Zadania na dzisiaj");
        request.Headers.Add("Tags", "white_check_mark");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ntfy returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        return new NtfyPublishResult(ReadMessageId(responseBody), TopicConfigured: true);
    }

    private Uri BuildTopicUri()
    {
        var baseUrl = _baseUrl.TrimEnd('/');
        var topic = Uri.EscapeDataString(_topic);

        return new Uri($"{baseUrl}/{topic}");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            throw new InvalidOperationException("Missing Ntfy:BaseUrl configuration.");
        }

        if (string.IsNullOrWhiteSpace(_topic))
        {
            throw new InvalidOperationException("Missing Ntfy:Topic configuration.");
        }
    }

    private static string? ReadMessageId(string responseBody)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}