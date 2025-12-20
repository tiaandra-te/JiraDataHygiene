using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Net;

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
        var settingsPath = ResolveSettingsPath();
        if (settingsPath is null)
        {
            Console.Error.WriteLine($"Missing {SettingsFileName}. Create it based on appsettings.example.json.");
            return 1;
        }

        AppSettings? settings;
        try
        {
            var settingsJson = await File.ReadAllTextAsync(settingsPath);
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
        using var sendGridClient = CreateSendGridClient(settings.SendGrid);

        var filters = new List<FilterIssues>();

        foreach (var filterId in settings.Jira.FilterIds)
        {
            Console.WriteLine($"Loading issues for filter {filterId}...");
            var filterName = await LoadFilterNameAsync(jiraClient, filterId);
            var issues = await LoadIssuesForFilterAsync(jiraClient, filterId);
            filters.Add(new FilterIssues(filterId, filterName, issues));
        }

        var assignees = new Dictionary<string, AssigneeBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in filters)
        {
            foreach (var issue in filter.Issues)
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

                if (!assignees.TryGetValue(assignee.EmailAddress, out var bucket))
                {
                    bucket = new AssigneeBucket(assignee.EmailAddress, assignee.DisplayName);
                    assignees[assignee.EmailAddress] = bucket;
                }

                if (!bucket.Filters.TryGetValue(filter.FilterId, out var filterBucket))
                {
                    filterBucket = new FilterBucket(filter.FilterId, filter.FilterName);
                    bucket.Filters[filter.FilterId] = filterBucket;
                }

                filterBucket.Issues.Add(new IssueEntry(
                    issue.Key,
                    issue.Fields.Summary,
                    filter.FilterId,
                    $"{EnsureTrailingSlash(settings.Jira.BaseUrl)}browse/{issue.Key}"));
            }
        }

        var dryRunSentCount = 0;
        var dryRunMax = settings.SendGrid.DryRunMaxEmails;

        foreach (var bucket in assignees.Values)
        {
            var issueCount = bucket.Filters.Values.Sum(filter => filter.Issues.Count);
            var useHtml = string.Equals(settings.SendGrid.ContentType, "text/html", StringComparison.OrdinalIgnoreCase);
            var subject = ApplyUserTemplate(
                settings.SendGrid.SubjectTemplate,
                bucket,
                issueCount,
                settings.Jira.BaseUrl,
                useHtml,
                appendFiltersWhenMissing: false,
                includeFooter: false,
                footerHtml: settings.SendGrid.FooterHtml,
                footerText: settings.SendGrid.FooterText);
            var body = ApplyUserTemplate(
                settings.SendGrid.BodyTemplate,
                bucket,
                issueCount,
                settings.Jira.BaseUrl,
                useHtml,
                appendFiltersWhenMissing: true,
                includeFooter: true,
                footerHtml: settings.SendGrid.FooterHtml,
                footerText: settings.SendGrid.FooterText);

            var toEmail = settings.SendGrid.DryRun ? "tiaandra@cisco.com" : bucket.Email;
            var toName = settings.SendGrid.DryRun ? "Tiaandra Cisco" : (bucket.DisplayName ?? bucket.Email);

            if (settings.SendGrid.DryRun && dryRunMax > 0 && dryRunSentCount >= dryRunMax)
            {
                Console.WriteLine($"[DryRun] Reached DryRunMaxEmails ({dryRunMax}). Skipping remaining emails.");
                break;
            }

            var sent = await SendEmailAsync(
                sendGridClient,
                settings.SendGrid,
                toEmail,
                toName,
                subject,
                body);

            if (settings.SendGrid.DryRun)
            {
                dryRunSentCount++;
            }

            Console.WriteLine(sent
                ? $"{(settings.SendGrid.DryRun ? "[DryRun] " : string.Empty)}Sent email to {toEmail} for {issueCount} issues."
                : $"{(settings.SendGrid.DryRun ? "[DryRun] " : string.Empty)}Failed to send email to {toEmail} for {issueCount} issues.");
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

    private static async Task<string> LoadFilterNameAsync(HttpClient client, int filterId)
    {
        var url = $"rest/api/3/filter/{filterId}";
        using var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to load filter name {filterId}: {response.StatusCode} {errorBody}");
            return $"Filter {filterId}";
        }

        var payload = await response.Content.ReadAsStringAsync();
        var filter = JsonSerializer.Deserialize<JiraFilterResponse>(payload, JsonOptions);
        return string.IsNullOrWhiteSpace(filter?.Name) ? $"Filter {filterId}" : filter.Name;
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

    private static string ApplyUserTemplate(
        string template,
        AssigneeBucket bucket,
        int issueCount,
        string baseUrl,
        bool useHtml,
        bool appendFiltersWhenMissing,
        bool includeFooter,
        string footerHtml,
        string footerText)
    {
        var resolved = template
            .Replace("{Assignee}", bucket.DisplayName ?? bucket.Email, StringComparison.OrdinalIgnoreCase)
            .Replace("{IssueCount}", issueCount.ToString(), StringComparison.OrdinalIgnoreCase);

        var filtersBlock = BuildFiltersBlock(bucket, baseUrl, useHtml);
        if (resolved.Contains("{Filters}", StringComparison.OrdinalIgnoreCase))
        {
            resolved = resolved.Replace("{Filters}", filtersBlock, StringComparison.OrdinalIgnoreCase);
        }

        if (appendFiltersWhenMissing)
        {
            resolved = useHtml ? $"{resolved}<br/><br/>{filtersBlock}" : $"{resolved}\n\n{filtersBlock}";
        }

        if (!includeFooter)
        {
            return resolved;
        }

        var footer = BuildFooter(useHtml, footerHtml, footerText);
        return useHtml ? $"{resolved}<br/><br/>{footer}" : $"{resolved}\n\n{footer}";
    }

    private static string BuildFiltersBlock(AssigneeBucket bucket, string baseUrl, bool useHtml)
    {
        var builder = new StringBuilder();
        foreach (var filter in bucket.Filters.Values.OrderBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase))
        {
            var filterUrl = $"{EnsureTrailingSlash(baseUrl)}issues/?filter={filter.FilterId}";
            if (useHtml)
            {
                builder.AppendLine($"<a href=\"{filterUrl}\">{WebUtility.HtmlEncode(filter.FilterName)} ({filter.Issues.Count})</a><br/>");
                builder.AppendLine("<ul>");
            }
            else
            {
                builder.AppendLine($"Filter: {filter.FilterName} ({filter.FilterId}) {filterUrl}");
            }

            foreach (var issue in filter.Issues)
            {
                if (useHtml)
                {
                    builder.Append("<li><a href=\"")
                        .Append(issue.IssueUrl)
                        .Append("\">")
                        .Append(WebUtility.HtmlEncode(issue.Key))
                        .Append("</a>: ")
                        .Append(WebUtility.HtmlEncode(issue.Summary))
                        .AppendLine("</li>");
                }
                else
                {
                    builder.Append("- ")
                        .Append(issue.Key)
                        .Append(": ")
                        .Append(issue.Summary)
                        .Append(" ")
                        .Append(issue.IssueUrl)
                        .AppendLine();
                }
            }

            if (useHtml)
            {
                builder.AppendLine("</ul>");
                // builder.AppendLine("<br/>");
            }
            else
            {
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildFooter(bool useHtml, string footerHtml, string footerText)
    {
        if (useHtml)
        {
            return footerHtml;
        }

        return footerText;
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";

    private static string? ResolveSettingsPath()
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        if (File.Exists(SettingsFileName))
        {
            return SettingsFileName;
        }

        return null;
    }
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
    public string SubjectTemplate { get; set; } = "[Jira] {IssueCount} issues for {Assignee}";
    public string BodyTemplate { get; set; } = "Hello {Assignee},\n\nPlease review the following {IssueCount} issues that are in inconsistent state:\n{Filters}";
    public string ContentType { get; set; } = "text/plain";
    public bool DryRun { get; set; }
    public int DryRunMaxEmails { get; set; }
    public string FooterHtml { get; set; } = "This is an automated email. For any questions please reachout to <a href=\"mailto:tiaandra@cisco.com\">Tiago Andrade e Silva</a>. This is part of data hygiene process. The goal is that your name does not show up in the <a href=\"https://thousandeyes.atlassian.net/jira/dashboards/15665\">Data Hygiene dashboard</a>.";
    public string FooterText { get; set; } = "This is an automated email. For any questions please reachout to tiaandra@cisco.com. This is part of data hygiene process. The goal is that your name does not show up in the Data Hygiene dashboard: https://thousandeyes.atlassian.net/jira/dashboards/15665";
}

sealed class JiraSearchResponse
{
    public int Total { get; set; }
    public List<JiraIssue> Issues { get; set; } = [];
}

sealed class JiraFilterResponse
{
    public string Name { get; set; } = string.Empty;
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

sealed record IssueEntry(string Key, string Summary, int FilterId, string IssueUrl);

sealed class AssigneeBucket
{
    public AssigneeBucket(string email, string? displayName)
    {
        Email = email;
        DisplayName = displayName;
    }

    public string Email { get; }
    public string? DisplayName { get; }
    public Dictionary<int, FilterBucket> Filters { get; } = [];
}

sealed class FilterIssues
{
    public FilterIssues(int filterId, string filterName, List<JiraIssue> issues)
    {
        FilterId = filterId;
        FilterName = filterName;
        Issues = issues;
    }

    public int FilterId { get; }
    public string FilterName { get; }
    public List<JiraIssue> Issues { get; }
}

sealed class FilterBucket
{
    public FilterBucket(int filterId, string filterName)
    {
        FilterId = filterId;
        FilterName = filterName;
    }

    public int FilterId { get; }
    public string FilterName { get; }
    public List<IssueEntry> Issues { get; } = [];
}
