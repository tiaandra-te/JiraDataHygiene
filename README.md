# Jira Data Hygiene

Console app that loads Jira issues from multiple filter IDs, aggregates them per assignee, and sends a single email per user with issues grouped by filter.

## Requirements
- .NET SDK 9+
- Jira Cloud email + API token
- SendGrid API key

## Setup
1. Copy settings and fill in your values:
   - `appsettings.example.json` -> `appsettings.json`
2. Add your Jira filter IDs. For [example](https://thousandeyes.atlassian.net/issues/?filter=31552)
3. Get your [Jira token](https://id.atlassian.com/manage-profile/security/api-tokens)
4. Set `SendGrid.DryRun` to `true` while testing.

## Run
```bash
dotnet run --project JiraDAtaHygiene.sln
```

## Build
```bash
dotnet build JiraDAtaHygiene.sln
```

## Email Example
```html
Hello Jane Doe,<br><br>
Please review the following 6 issues that are in an incorrect state, grouped by Exception:<br><br>
<a href="https://your-domain.atlassian.net/issues/?filter=31553">DataHygiene - Engineering Sub Teams Required is empty (2)</a><br>
<div><em>Missing the sub teams that will be needed to implement this feature.</em></div><br>
<ul>
  <li><a href="https://your-domain.atlassian.net/browse/PR-1718">PR-1718</a>: Forwarding Loss support in Path viz</li>
  <li><a href="https://your-domain.atlassian.net/browse/PR-1577">PR-1577</a>: Pagination support in datapoints API</li>
</ul>
<a href="https://your-domain.atlassian.net/issues/?filter=31552">DataHygiene - Engineering Teams Required is empty (2)</a><br>
<div><em>Please identify the engineering group that will implement this feature.</em></div><br>
<ul>
  <li><a href="https://your-domain.atlassian.net/browse/PR-1718">PR-1718</a>: Forwarding Loss support in Path viz</li>
  <li><a href="https://your-domain.atlassian.net/browse/PR-1577">PR-1577</a>: Pagination support in datapoints API</li>
</ul>
<a href="https://your-domain.atlassian.net/issues/?filter=31554">DataHygiene - No Request Type (2)</a><br>
<div><em>Missing Request Type. Update to reflect Product Roadmap, Engineering Enabler, or KTLO.</em></div><br>
<ul>
  <li><a href="https://your-domain.atlassian.net/browse/PR-1718">PR-1718</a>: Forwarding Loss support in Path viz</li>
  <li><a href="https://your-domain.atlassian.net/browse/PR-1577">PR-1577</a>: Pagination support in datapoints API</li>
</ul><br><br>
This is an automated email. For any questions please reach out to
<a href="mailto:tiaandra@cisco.com">Tiago Andrade e Silva</a>. This is part of data hygiene process.
The goal is that your name does not show up in the
<a href="https://your-domain.atlassian.net/jira/dashboards/15665">Data Hygiene dashboard</a>.
```

## Configuration
`appsettings.json` settings:
- `Jira.BaseUrl`: Jira Cloud base URL, e.g. `https://your-domain.atlassian.net/`.
- `Jira.Email`: Jira user email for API auth.
- `Jira.ApiToken`: Jira API token.
- `Jira.Filters`: List of filter entries with `Id` and `Description`.
- `SendGrid.ApiKey`: SendGrid API key.
- `SendGrid.FromEmail`: From address for outbound email.
- `SendGrid.FromName`: From display name.
- `SendGrid.SubjectTemplate`: Subject template supporting `{Assignee}` and `{IssueCount}`.
- `SendGrid.BodyTemplate`: Body template supporting `{Assignee}`, `{IssueCount}`, `{Filters}`.
- `SendGrid.ContentType`: `text/plain` or `text/html` (HTML enables links).
- `SendGrid.DryRun`: If `true`, emails send to the dry-run recipient.
- `SendGrid.DryRunMaxEmails`: Max emails to send in dry-run mode (0 = no limit).
- `SendGrid.FooterHtml`: Footer for HTML emails.
- `SendGrid.FooterText`: Footer for plain-text emails.
- `SendGrid.CcEmails`: Comma-separated CC list applied to all emails.

Template placeholders:
- `{Assignee}`, `{IssueCount}`, `{Filters}`

## Notes
- Jira Cloud may not expose assignee email addresses depending on privacy settings.
- `appsettings.json` is ignored by git for safety.
