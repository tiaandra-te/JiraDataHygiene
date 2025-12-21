namespace JiraDataHygiene.Utils;

public static class UrlHelper
{
    public static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
}
