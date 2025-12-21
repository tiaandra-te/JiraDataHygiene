using System.Text.Json;
using System.Text.Json.Serialization;
using JiraDataHygiene.Config;
using JiraDataHygiene.Models;
using JiraDataHygiene.Services;
using JiraDataHygiene.Utils;

namespace JiraDataHygiene;

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
        var settingsPath = SettingsLoader.ResolveSettingsPath(SettingsFileName);
        if (settingsPath is null)
        {
            Console.Error.WriteLine($"Missing {SettingsFileName}. Create it based on appsettings.example.json.");
            return 1;
        }

        AppSettings? settings;
        try
        {
            settings = await SettingsLoader.LoadAsync(settingsPath, JsonOptions);
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

        if (settings.Jira.Filters.Count == 0)
        {
            Console.Error.WriteLine("No Jira filter IDs configured.");
            return 1;
        }

        using var jiraClient = JiraService.CreateClient(settings.Jira);
        using var sendGridClient = SendGridService.CreateClient(settings.SendGrid);

        var jiraService = new JiraService(jiraClient, JsonOptions);
        var sendGridService = new SendGridService(sendGridClient, JsonOptions);

        var issuesByFilter = new List<FilterIssues>();
        foreach (var filterConfig in settings.Jira.Filters)
        {
            Console.WriteLine($"Loading issues for filter {filterConfig.Id}...");
            var filterName = await jiraService.LoadFilterNameAsync(filterConfig.Id);
            var issues = await jiraService.LoadIssuesForFilterAsync(filterConfig.Id);
            issuesByFilter.Add(new FilterIssues(filterConfig.Id, filterName, filterConfig.Description, issues));
        }

        var assignees = BuildAssigneeBuckets(settings.Jira.BaseUrl, issuesByFilter);

        var useHtml = string.Equals(settings.SendGrid.ContentType, "text/html", StringComparison.OrdinalIgnoreCase);
        var templateBuilder = new EmailTemplateBuilder(
            settings.Jira.BaseUrl,
            useHtml,
            settings.SendGrid.FooterHtml,
            settings.SendGrid.FooterText);

        var dryRunSentCount = 0;
        var dryRunMax = settings.SendGrid.DryRunMaxEmails;

        foreach (var bucket in assignees.Values)
        {
            var issueCount = bucket.Filters.Values.Sum(filter => filter.Issues.Count);
            var subject = templateBuilder.Build(
                settings.SendGrid.SubjectTemplate,
                bucket,
                issueCount,
                appendFiltersWhenMissing: false,
                includeFooter: false);
            var body = templateBuilder.Build(
                settings.SendGrid.BodyTemplate,
                bucket,
                issueCount,
                appendFiltersWhenMissing: true,
                includeFooter: true);

            var toEmail = settings.SendGrid.DryRun ? "tiaandra@cisco.com" : bucket.Email;
            var toName = settings.SendGrid.DryRun ? "Tiaandra Cisco" : (bucket.DisplayName ?? bucket.Email);

            if (settings.SendGrid.DryRun && dryRunMax > 0 && dryRunSentCount >= dryRunMax)
            {
                Console.WriteLine($"[DryRun] Reached DryRunMaxEmails ({dryRunMax}). Skipping remaining emails.");
                break;
            }

            var sent = await sendGridService.SendAsync(
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

    private static Dictionary<string, AssigneeBucket> BuildAssigneeBuckets(
        string baseUrl,
        IEnumerable<FilterIssues> filters)
    {
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
                    filterBucket = new FilterBucket(filter.FilterId, filter.FilterName, filter.Description);
                    bucket.Filters[filter.FilterId] = filterBucket;
                }

                filterBucket.Issues.Add(new IssueEntry(
                    issue.Key,
                    issue.Fields.Summary,
                    filter.FilterId,
                    $"{UrlHelper.EnsureTrailingSlash(baseUrl)}browse/{issue.Key}"));
            }
        }

        return assignees;
    }
}
