namespace JiraDataHygiene.Config;

public sealed class AppSettings
{
    public JiraSettings Jira { get; set; } = new();
    public SendGridSettings SendGrid { get; set; } = new();
}

public sealed class JiraSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public List<JiraFilterConfig> Filters { get; set; } = [];
    public bool EnableJiraComments { get; set; }
    public bool LogJiraCommentsToConsole { get; set; }
    public int CommentDupDaysSkip { get; set; } = 7;
}

public sealed class SendGridSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Jira Data Hygiene";
    public string SubjectTemplate { get; set; } = "[Jira] {IssueCount} issues for {Assignee}";
    public string BodyTemplate { get; set; } = "Hello {Assignee},\n\nPlease review the following {IssueCount} issues that are in inconsistent state:\n{Filters}";
    public string ContentType { get; set; } = "text/plain";
    public bool DryRun { get; set; }
    public int DryRunMaxEmails { get; set; }
    public string FooterHtml { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;
    public string CcEmails { get; set; } = string.Empty;
    public bool SendReportOfLogs { get; set; }
    public string Email2SendReportOfLogs { get; set; } = string.Empty;
    public string DryRunEmail { get; set; } = string.Empty;
    public string DryRunName { get; set; } = string.Empty;
}

public sealed class JiraFilterConfig
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
}
