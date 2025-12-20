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
