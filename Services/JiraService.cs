using System.Net.Http.Headers;
using System.Text.Json;
using JiraDataHygiene.Config;
using JiraDataHygiene.Models;
using JiraDataHygiene.Utils;

namespace JiraDataHygiene.Services;

public sealed class JiraService
{
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
                Console.WriteLine($"Failed to load filter {filterId}: {response.StatusCode} {errorBody}");
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
            Console.WriteLine($"Failed to load filter name {filterId}: {response.StatusCode} {errorBody}");
            return $"Filter {filterId}";
        }

        var payload = await response.Content.ReadAsStringAsync();
        var filter = JsonSerializer.Deserialize<JiraFilterResponse>(payload, _jsonOptions);
        return string.IsNullOrWhiteSpace(filter?.Name) ? $"Filter {filterId}" : filter.Name;
    }
}
