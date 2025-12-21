using System.Net;
using System.Text;
using JiraDataHygiene.Models;
using JiraDataHygiene.Utils;

namespace JiraDataHygiene.Services;

public sealed class EmailTemplateBuilder
{
    private readonly string _baseUrl;
    private readonly bool _useHtml;
    private readonly string _footerHtml;
    private readonly string _footerText;

    public EmailTemplateBuilder(string baseUrl, bool useHtml, string footerHtml, string footerText)
    {
        _baseUrl = baseUrl;
        _useHtml = useHtml;
        _footerHtml = footerHtml;
        _footerText = footerText;
    }

    public string Build(
        string template,
        AssigneeBucket bucket,
        int issueCount,
        bool appendFiltersWhenMissing,
        bool includeFooter)
    {
        var resolved = template
            .Replace("{Assignee}", bucket.DisplayName ?? bucket.Email, StringComparison.OrdinalIgnoreCase)
            .Replace("{IssueCount}", issueCount.ToString(), StringComparison.OrdinalIgnoreCase);

        var filtersBlock = BuildFiltersBlock(bucket);
        var hasFiltersToken = resolved.Contains("{Filters}", StringComparison.OrdinalIgnoreCase);
        if (hasFiltersToken)
        {
            resolved = resolved.Replace("{Filters}", filtersBlock, StringComparison.OrdinalIgnoreCase);
        }

        if (appendFiltersWhenMissing && !hasFiltersToken)
        {
            resolved = _useHtml ? $"{resolved}<br/><br/>{filtersBlock}" : $"{resolved}\n\n{filtersBlock}";
        }

        if (!includeFooter)
        {
            return resolved;
        }

        var footer = BuildFooter();
        return _useHtml ? $"{resolved}<br/><br/>{footer}" : $"{resolved}\n\n{footer}";
    }

    private string BuildFiltersBlock(AssigneeBucket bucket)
    {
        var builder = new StringBuilder();
        foreach (var filter in bucket.Filters.Values.OrderBy(f => f.FilterName, StringComparer.OrdinalIgnoreCase))
        {
            var filterUrl = $"{UrlHelper.EnsureTrailingSlash(_baseUrl)}issues/?filter={filter.FilterId}";
            if (_useHtml)
            {
                builder.AppendLine($"<a href=\"{filterUrl}\">{WebUtility.HtmlEncode(filter.FilterName)} ({filter.Issues.Count})</a><br/>");
                if (!string.IsNullOrWhiteSpace(filter.Description))
                {
                    builder.AppendLine($"<div><em>{WebUtility.HtmlEncode(filter.Description)}</em></div><br/>");
                }
                builder.AppendLine("<ul>");
            }
            else
            {
                builder.AppendLine($"Filter: {filter.FilterName} ({filter.FilterId}) {filterUrl}");
                if (!string.IsNullOrWhiteSpace(filter.Description))
                {
                    builder.AppendLine($"Description: {filter.Description}");
                }
            }

            foreach (var issue in filter.Issues)
            {
                if (_useHtml)
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

            if (_useHtml)
            {
                builder.AppendLine("</ul>");
                builder.AppendLine("<br/>");
            }
            else
            {
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildFooter() => _useHtml ? _footerHtml : _footerText;
}
