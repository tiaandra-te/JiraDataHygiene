# Jira Data Hygiene

Console app that loads Jira issues from a list of filter IDs and emails each assignee via SendGrid.

## Requirements
- .NET SDK 9+
- Jira Cloud email + API token
- SendGrid API key

## Setup
1. Copy settings and fill in your values:
   - `appsettings.example.json` -> `appsettings.json`
2. Add your Jira filter IDs.
3. Set `SendGrid.DryRun` to `true` while testing.

## Run
```bash
dotnet run --project JiraDAtaHygiene.sln
```

## Configuration
Key settings in `appsettings.json`:
- `Jira.BaseUrl`
- `Jira.Email`
- `Jira.ApiToken`
- `Jira.FilterIds`
- `SendGrid.ApiKey`
- `SendGrid.FromEmail`
- `SendGrid.FromName`
- `SendGrid.SubjectTemplate`
- `SendGrid.BodyTemplate`
- `SendGrid.ContentType` (`text/plain` or `text/html`)
- `SendGrid.DryRun` (logs only)

Template placeholders:
- `{Key}`, `{Summary}`, `{Assignee}`, `{FilterId}`, `{IssueUrl}`

## Notes
- Jira Cloud may not expose assignee email addresses depending on privacy settings.
- `appsettings.json` is ignored by git for safety.
