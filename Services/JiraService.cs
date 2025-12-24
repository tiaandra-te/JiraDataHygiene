using System.Net.Http.Headers;
using System.Text.Json;
using JiraDataHygiene.Config;
using JiraDataHygiene.Models;
using JiraDataHygiene.Utils;

namespace JiraDataHygiene.Services;

public sealed class JiraService
{
    private const string CommentMarker = "#datahygiene";
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public JiraService(HttpClient client, JsonSerializerOptions jsonOptions)
    {
        _client = client;
        _jsonOptions = jsonOptions;
    }

    public static HttpClient CreateClient(JiraSettings settings)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(UrlHelper.EnsureTrailingSlash(settings.BaseUrl))
        };

        var authBytes = System.Text.Encoding.UTF8.GetBytes($"{settings.Email}:{settings.ApiToken}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<List<JiraIssue>> LoadIssuesForFilterAsync(int filterId)
    {
        var issues = new List<JiraIssue>();
        var startAt = 0;
        var maxResults = 50;

        while (true)
        {
            var jql = $"filter={filterId}";
            var fields = "summary,assignee";
            var url =
                $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&startAt={startAt}&maxResults={maxResults}&fields={Uri.EscapeDataString(fields)}";

            using var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                LogCollector.Error($"Failed to load filter {filterId}: {response.StatusCode} {errorBody}");
                break;
            }

            var payload = await response.Content.ReadAsStringAsync();
            var search = JsonSerializer.Deserialize<JiraSearchResponse>(payload, _jsonOptions);
            if (search?.Issues is null || search.Issues.Count == 0)
            {
                break;
            }

            issues.AddRange(search.Issues);
            startAt += search.Issues.Count;

            if (startAt >= search.Total)
            {
                break;
            }
        }

        return issues;
    }

    public async Task<string> LoadFilterNameAsync(int filterId)
    {
        var url = $"rest/api/3/filter/{filterId}";
        using var response = await _client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            LogCollector.Error($"Failed to load filter name {filterId}: {response.StatusCode} {errorBody}");
            return $"Filter {filterId}";
        }

        var payload = await response.Content.ReadAsStringAsync();
        var filter = JsonSerializer.Deserialize<JiraFilterResponse>(payload, _jsonOptions);
        return string.IsNullOrWhiteSpace(filter?.Name) ? $"Filter {filterId}" : filter.Name;
    }

    public async Task<bool> AddCommentAsync(string issueKey, string accountId, string message, int commentDupDaysSkip)
    {
        var existingCreated = await FindLatestMatchingCommentCreatedAsync(issueKey, message);
        if (existingCreated.HasValue)
        {
            var age = DateTimeOffset.UtcNow - existingCreated.Value;
            if (commentDupDaysSkip > 0 && age.TotalDays < commentDupDaysSkip)
            {
                LogCollector.Info(
                    $"\tComment already exists on {issueKey} from {existingCreated.Value:yyyy-MM-dd}; skipping.");
                return true;
            }

            if (commentDupDaysSkip <= 0)
            {
                LogCollector.Info($"\tComment already exists on {issueKey}; skipping.");
                return true;
            }

            LogCollector.Info(
                $"\tComment already exists on {issueKey} from {existingCreated.Value:yyyy-MM-dd}; " +
                $"creating duplicate because last comment is {Math.Floor(age.TotalDays)} days old.");
        }

        var url = $"rest/api/3/issue/{issueKey}/comment";
        var payload = new
        {
            body = new
            {
                type = "doc",
                version = 1,
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new object[]
                        {
                            new
                            {
                                type = "mention",
                                attrs = new { id = accountId }
                            },
                            new
                            {
                                type = "text",
                                text = $" {message} {CommentMarker}"
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(url, content);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var errorBody = await response.Content.ReadAsStringAsync();
        LogCollector.Error($"Failed to comment on {issueKey}: {response.StatusCode} {errorBody}");
        return false;
    }

    private async Task<DateTimeOffset?> FindLatestMatchingCommentCreatedAsync(string issueKey, string message)
    {
        var startAt = 0;
        var maxResults = 50;
        var expectedText = $"{message} {CommentMarker}";
        DateTimeOffset? latestMatch = null;

        while (true)
        {
            var url = $"rest/api/3/issue/{issueKey}/comment?startAt={startAt}&maxResults={maxResults}";
            using var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                LogCollector.Error($"Failed to load comments for {issueKey}: {response.StatusCode} {errorBody}");
                return latestMatch;
            }

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("comments", out var comments) ||
                comments.ValueKind != JsonValueKind.Array)
            {
                return latestMatch;
            }

            foreach (var comment in comments.EnumerateArray())
            {
                if (!comment.TryGetProperty("body", out var body))
                {
                    continue;
                }

                var text = ExtractCommentText(body);
                if (!text.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!comment.TryGetProperty("created", out var createdElement) ||
                    createdElement.ValueKind != JsonValueKind.String)
                {
                    latestMatch ??= DateTimeOffset.UtcNow;
                    continue;
                }

                if (DateTimeOffset.TryParse(createdElement.GetString(), out var created))
                {
                    if (!latestMatch.HasValue || created > latestMatch.Value)
                    {
                        latestMatch = created;
                    }
                }
            }

            var total = doc.RootElement.TryGetProperty("total", out var totalElement)
                ? totalElement.GetInt32()
                : startAt + comments.GetArrayLength();

            startAt += comments.GetArrayLength();
            if (startAt >= total || comments.GetArrayLength() == 0)
            {
                break;
            }
        }

        return latestMatch;
    }

    private static string ExtractCommentText(JsonElement body)
    {
        var builder = new System.Text.StringBuilder();
        AppendText(body, builder);
        return builder.ToString();
    }

    private static void AppendText(JsonElement element, System.Text.StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    builder.Append(textElement.GetString());
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("text"))
                    {
                        continue;
                    }

                    AppendText(property.Value, builder);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AppendText(item, builder);
                }
                break;
        }
    }
}
