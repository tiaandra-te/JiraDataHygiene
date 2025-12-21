namespace JiraDataHygiene.Models;

public sealed class JiraSearchResponse
{
    public int Total { get; set; }
    public List<JiraIssue> Issues { get; set; } = [];
}

public sealed class JiraFilterResponse
{
    public string Name { get; set; } = string.Empty;
}

public sealed class JiraIssue
{
    public string Key { get; set; } = string.Empty;
    public JiraIssueFields Fields { get; set; } = new();
}

public sealed class JiraIssueFields
{
    public string Summary { get; set; } = string.Empty;
    public JiraUser? Assignee { get; set; }
}

public sealed class JiraUser
{
    public string? DisplayName { get; set; }
    public string? EmailAddress { get; set; }
    public string? AccountId { get; set; }
}
