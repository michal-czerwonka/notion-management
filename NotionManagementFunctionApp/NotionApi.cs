using System.Net.Http.Headers;
using System.Text.Json;

namespace NotionManagementFunctionApp;

internal static class NotionApi
{
    public static void Configure(HttpClient client)
    {
        client.BaseAddress = new Uri("https://api.notion.com/v1/");
        client.DefaultRequestHeaders.Add("Notion-Version", "2026-03-11");
    }

    public static HttpRequestMessage CreateRequest(HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static object TitleProperty(string name) => new
    {
        type = "title",
        title = new[] { new { type = "text", text = new { content = name } } }
    };

    public static string? ReadTitle(JsonElement page, string propertyName)
    {
        if (!page.TryGetProperty("properties", out var properties) ||
            !properties.TryGetProperty(propertyName, out var property) ||
            !property.TryGetProperty("title", out var title))
        {
            return null;
        }

        return string.Concat(title.EnumerateArray().Select(part =>
            part.TryGetProperty("plain_text", out var text) && text.ValueKind == JsonValueKind.String
                ? text.GetString() : null).Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
