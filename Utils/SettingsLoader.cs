using System.Text.Json;
using JiraDataHygiene.Config;

namespace JiraDataHygiene.Utils;

public static class SettingsLoader
{
    public static string? ResolveSettingsPath(string fileName)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        if (File.Exists(fileName))
        {
            return fileName;
        }

        return null;
    }

    public static async Task<AppSettings?> LoadAsync(string settingsPath, JsonSerializerOptions jsonOptions)
    {
        var settingsJson = await File.ReadAllTextAsync(settingsPath);
        return JsonSerializer.Deserialize<AppSettings>(settingsJson, jsonOptions);
    }
}
