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
    public string FooterHtml { get; set; } = "This is an automated email. For any questions please reachout to <a href=\"mailto:tiaandra@cisco.com\">Tiago Andrade e Silva</a>. This is part of data hygiene process. The goal is that your name does not show up in the <a href=\"https://thousandeyes.atlassian.net/jira/dashboards/15665\">Data Hygiene dashboard</a>.";
    public string FooterText { get; set; } = "This is an automated email. For any questions please reachout to tiaandra@cisco.com. This is part of data hygiene process. The goal is that your name does not show up in the Data Hygiene dashboard: https://thousandeyes.atlassian.net/jira/dashboards/15665";
    public string CcEmails { get; set; } = string.Empty;
}

public sealed class JiraFilterConfig
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
}
