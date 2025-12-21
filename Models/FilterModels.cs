namespace JiraDataHygiene.Models;

public sealed record IssueEntry(string Key, string Summary, int FilterId, string IssueUrl);

public sealed class AssigneeBucket
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

public sealed class FilterIssues
{
    public FilterIssues(int filterId, string filterName, string description, List<JiraIssue> issues)
    {
        FilterId = filterId;
        FilterName = filterName;
        Description = description;
        Issues = issues;
    }

    public int FilterId { get; }
    public string FilterName { get; }
    public string Description { get; }
    public List<JiraIssue> Issues { get; }
}

public sealed class FilterBucket
{
    public FilterBucket(int filterId, string filterName, string description)
    {
        FilterId = filterId;
        FilterName = filterName;
        Description = description;
    }

    public int FilterId { get; }
    public string FilterName { get; }
    public string Description { get; }
    public List<IssueEntry> Issues { get; } = [];
}
