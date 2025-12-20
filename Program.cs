using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string SettingsFileName = "appsettings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static async Task<int> Main()
    {
        if (!File.Exists(SettingsFileName))
        {
            Console.Error.WriteLine($"Missing {SettingsFileName}. Create it based on appsettings.example.json.");
            return 1;
        }

        AppSettings? settings;
        try
        {
            var settingsJson = await File.ReadAllTextAsync(SettingsFileName);
            settings = JsonSerializer.Deserialize<AppSettings>(settingsJson, JsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read {SettingsFileName}: {ex.Message}");
            return 1;
        }

        if (settings is null)
        {
            Console.Error.WriteLine($"Invalid {SettingsFileName} format.");
            return 1;
        }

        if (settings.Jira.FilterIds.Count == 0)
        {
            Console.Error.WriteLine("No Jira filter IDs configured.");
            return 1;
        }

        using var jiraClient = CreateJiraClient(settings.Jira);
        using var sendGridClient = settings.SendGrid.DryRun
            ? null
            : CreateSendGridClient(settings.SendGrid);

        foreach (var filterId in settings.Jira.FilterIds)
        {
            Console.WriteLine($"Loading issues for filter {filterId}...");
            var issues = await LoadIssuesForFilterAsync(jiraClient, filterId);

            foreach (var issue in issues)
            {
                var assignee = issue.Fields.Assignee;
                if (assignee is null)
                {
                    Console.WriteLine($"Skipping {issue.Key}: no assignee.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(assignee.EmailAddress))
                {
                    Console.WriteLine($"Skipping {issue.Key}: assignee email not available.");
                    continue;
                }

                var subject = ApplyTemplate(settings.SendGrid.SubjectTemplate, issue, filterId, settings.Jira.BaseUrl);
                var body = ApplyTemplate(settings.SendGrid.BodyTemplate, issue, filterId, settings.Jira.BaseUrl);

                if (settings.SendGrid.DryRun)
                {
                    Console.WriteLine($"[DryRun] Would send {issue.Key} to {assignee.EmailAddress} with subject '{subject}'.");
                    continue;
                }

                var sent = await SendEmailAsync(
                    sendGridClient!,
                    settings.SendGrid,
                    assignee.EmailAddress,
                    assignee.DisplayName ?? assignee.EmailAddress,
                    subject,
                    body);

                Console.WriteLine(sent
                    ? $"Sent email for {issue.Key} to {assignee.EmailAddress}."
                    : $"Failed to send email for {issue.Key} to {assignee.EmailAddress}.");
            }
        }

        return 0;
    }

    private static HttpClient CreateJiraClient(JiraSettings jira)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(EnsureTrailingSlash(jira.BaseUrl))
        };

        var authBytes = Encoding.UTF8.GetBytes($"{jira.Email}:{jira.ApiToken}");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static HttpClient CreateSendGridClient(SendGridSettings sendGrid)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.sendgrid.com/")
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sendGrid.ApiKey);
        return client;
    }

    private static async Task<List<JiraIssue>> LoadIssuesForFilterAsync(HttpClient client, int filterId)
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

            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to load filter {filterId}: {response.StatusCode} {errorBody}");
                break;
            }

            var payload = await response.Content.ReadAsStringAsync();
            var search = JsonSerializer.Deserialize<JiraSearchResponse>(payload, JsonOptions);
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

    private static async Task<bool> SendEmailAsync(
        HttpClient client,
        SendGridSettings settings,
        string toEmail,
        string toName,
        string subject,
        string body)
    {
        var payload = new SendGridEmailRequest
        {
            From = new SendGridEmailAddress
            {
                Email = settings.FromEmail,
                Name = settings.FromName
            },
            Personalizations =
            [
                new SendGridPersonalization
                {
                    To =
                    [
                        new SendGridEmailAddress
                        {
                            Email = toEmail,
                            Name = toName
                        }
                    ],
                    Subject = subject
                }
            ],
            Content =
            [
                new SendGridContent
                {
                    Type = settings.ContentType,
                    Value = body
                }
            ]
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync("v3/mail/send", content);
        return response.IsSuccessStatusCode;
    }

    private static string ApplyTemplate(string template, JiraIssue issue, int filterId, string baseUrl)
    {
        var issueUrl = $"{EnsureTrailingSlash(baseUrl)}browse/{issue.Key}";
        return template
            .Replace("{Key}", issue.Key ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Summary}", issue.Fields.Summary ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Assignee}", issue.Fields.Assignee?.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{FilterId}", filterId.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{IssueUrl}", issueUrl, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
}

sealed class AppSettings
{
    public JiraSettings Jira { get; set; } = new();
    public SendGridSettings SendGrid { get; set; } = new();
}

sealed class JiraSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public List<int> FilterIds { get; set; } = [];
}

sealed class SendGridSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Jira Data Hygiene";
    public string SubjectTemplate { get; set; } = "[Jira] {Key} - {Summary}";
    public string BodyTemplate { get; set; } = "Hello {Assignee},\n\nPlease review {Key}: {Summary}\n{IssueUrl}\n\nFilter: {FilterId}";
    public string ContentType { get; set; } = "text/plain";
    public bool DryRun { get; set; }
}

sealed class JiraSearchResponse
{
    public int Total { get; set; }
    public List<JiraIssue> Issues { get; set; } = [];
}


sealed class JiraIssue
{
    public string Key { get; set; } = string.Empty;
    public JiraIssueFields Fields { get; set; } = new();
}

sealed class JiraIssueFields
{
    public string Summary { get; set; } = string.Empty;
    public JiraUser? Assignee { get; set; }
}

sealed class JiraUser
{
    public string? DisplayName { get; set; }
    public string? EmailAddress { get; set; }
    public string? AccountId { get; set; }
}

sealed class SendGridEmailRequest
{
    public List<SendGridPersonalization> Personalizations { get; set; } = [];
    public SendGridEmailAddress From { get; set; } = new();
    public List<SendGridContent> Content { get; set; } = [];
}

sealed class SendGridPersonalization
{
    public List<SendGridEmailAddress> To { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
}

sealed class SendGridEmailAddress
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
}

sealed class SendGridContent
{
    public string Type { get; set; } = "text/plain";
    public string Value { get; set; } = string.Empty;
}
