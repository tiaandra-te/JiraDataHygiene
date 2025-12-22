# Jira Data Hygiene

Console app that loads Jira issues from multiple filter IDs, aggregates them per assignee, and sends a single email per user with issues grouped by filter so they know what issues need to be addressed.

## Requirements
- .NET SDK 9+
- Jira Cloud email + API token
- SendGrid API key

## Setup
1. Copy settings and fill in your values:
   - `appsettings.example.json` -> `appsettings.json`
2. Add your Jira filter IDs. For [example](https://xpto.atlassian.net/issues/?filter=31552)
3. Get your [Jira token](https://id.atlassian.com/manage-profile/security/api-tokens)
4. Set `SendGrid.DryRun` to `true` while testing.


## Configuration
`appsettings.json` settings:
- `Jira.BaseUrl`: Jira Cloud base URL, e.g. `https://your-domain.atlassian.net/`.
- `Jira.Email`: Jira user email for API auth.
- `Jira.ApiToken`: Jira API token.
- `Jira.EnableComments`: If `true`, add a Jira comment per issue using the filter description.
- `Jira.LogComments`: If `true`, log comment text before posting.
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
- `SendGrid.SendLogEmail`: If `true`, send a run log email at the end of execution.
- `SendGrid.LogEmail`: Recipient for the run log email.

Template placeholders:
- `{Assignee}`, `{IssueCount}`, `{Filters}`

## Build
```bash
dotnet build JiraDAtaHygiene.sln
```

## Run
```bash
dotnet run --project JiraDAtaHygiene.sln
```



## Example of Email that users receive
Hello Jane Doe,

Please review the following 6 issues that are in an incorrect state, grouped by Exception:

[DataHygiene - Engineering Sub Teams Required is empty](https://xpto.atlassian.net/issues/?filter=31553) (2)

Missing the sub teams that will be needed to implement this feature.
- PR-1718: Forwarding Loss support in xpto viz
- PR-1577: Pagination support in xpto API

[DataHygiene - Engineering Teams Required is empty](https://xpto.atlassian.net/issues/?filter=31552) (2)

Please identify the engineering group that will implement this feature.
- PR-1718: Forwarding Loss support in xpto viz
- PR-1577: Pagination support in datapoints API

[DataHygiene - No Request Type](https://xpto.atlassian.net/issues/?filter=31554) (2)

Missing Request Type. Update to reflect Product Roadmap, Engineering Enabler, or KTLO.
- PR-1718: Forwarding Loss support in xpto viz
- PR-1577: Pagination support in xpto API

This is an automated email. For any questions please reach out to Tiago Andrade e Silva.
This is part of data hygiene process. The goal is that your name does not show up in the Data Hygiene dashboard.


## Code Structure
**Flow overview**
- Load settings from `appsettings.json`.
- Fetch all Jira filters + issues and group them by assignee.
- Build one email per assignee (grouped by filter + optional footer).
- Send via SendGrid (or dry-run limit).

**Root**
- `Program.cs`: App entry point and orchestration (settings, fetch, aggregate, send).

**Config**
- `Config/Settings.cs`: Configuration models for Jira and SendGrid settings.

**Models**
- `Models/JiraModels.cs`: Jira API response and issue models.
- `Models/SendGridModels.cs`: SendGrid request payload models.
- `Models/FilterModels.cs`: Aggregation models for filters, assignees, and issues.

**Services**
- `Services/JiraService.cs`: Jira API client wrapper (filter names + issues).
- `Services/SendGridService.cs`: SendGrid API client wrapper (send + error logging).
- `Services/EmailTemplateBuilder.cs`: Email body/subject templating and formatting.

**Utils**
- `Utils/SettingsLoader.cs`: Settings file resolution and JSON deserialization.
- `Utils/UrlHelper.cs`: URL helpers (e.g., ensure trailing slash).

## Notes
- Jira Cloud may not expose assignee email addresses depending on privacy settings.
- `appsettings.json` is ignored by git for safety.
