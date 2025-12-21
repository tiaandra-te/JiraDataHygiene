using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraDataHygiene.Config;
using JiraDataHygiene.Models;
using JiraDataHygiene.Utils;

namespace JiraDataHygiene.Services;

public sealed class SendGridService
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public SendGridService(HttpClient client, JsonSerializerOptions jsonOptions)
    {
        _client = client;
        _jsonOptions = jsonOptions;
    }

    public static HttpClient CreateClient(SendGridSettings settings)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.sendgrid.com/")
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        return client;
    }

    public async Task<bool> SendAsync(
        SendGridSettings settings,
        string toEmail,
        string toName,
        string subject,
        string body)
    {
        var ccList = BuildCcList(settings);
        var payload = new SendGridEmailRequest
        {
            From = new SendGridEmailAddress
            {
                Email = settings.FromEmail,
                Name = settings.FromName
            },
            Personalizations =
            [
                new SendGridPersonalization
                {
                    To =
                    [
                        new SendGridEmailAddress
                        {
                            Email = toEmail,
                            Name = toName
                        }
                    ],
                    Cc = ccList,
                    Subject = subject
                }
            ],
            Content =
            [
                new SendGridContent
                {
                    Type = settings.ContentType,
                    Value = body
                }
            ]
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await _client.PostAsync("v3/mail/send", content);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var errorBody = await response.Content.ReadAsStringAsync();
        LogCollector.Error($"SendGrid error {response.StatusCode}: {errorBody}");
        return false;
    }

    private static List<SendGridEmailAddress>? BuildCcList(SendGridSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CcEmails))
        {
            return null;
        }

        var items = settings.CcEmails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => new SendGridEmailAddress { Email = email })
            .ToList();

        return items.Count == 0 ? null : items;
    }

    public Task<bool> SendLogAsync(SendGridSettings settings, string toEmail, string subject, string body)
    {
        var logSettings = new SendGridSettings
        {
            ApiKey = settings.ApiKey,
            FromEmail = settings.FromEmail,
            FromName = settings.FromName,
            ContentType = "text/plain",
            CcEmails = string.Empty
        };

        return SendAsync(logSettings, toEmail, toEmail, subject, body);
    }
}
