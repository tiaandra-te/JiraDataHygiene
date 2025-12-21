namespace JiraDataHygiene.Models;

public sealed class SendGridEmailRequest
{
    public List<SendGridPersonalization> Personalizations { get; set; } = [];
    public SendGridEmailAddress From { get; set; } = new();
    public List<SendGridContent> Content { get; set; } = [];
}

public sealed class SendGridPersonalization
{
    public List<SendGridEmailAddress> To { get; set; } = [];
    public List<SendGridEmailAddress>? Cc { get; set; }
    public string Subject { get; set; } = string.Empty;
}

public sealed class SendGridEmailAddress
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public sealed class SendGridContent
{
    public string Type { get; set; } = "text/plain";
    public string Value { get; set; } = string.Empty;
}
