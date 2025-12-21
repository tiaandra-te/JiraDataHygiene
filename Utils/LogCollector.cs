namespace JiraDataHygiene.Utils;

public static class LogCollector
{
    private static readonly List<string> Entries = [];
    private static readonly object Sync = new();

    public static void Info(string message) => Write(message, isError: false);

    public static void Error(string message) => Write(message, isError: true);

    public static void Write(string message, bool isError)
    {
        if (isError)
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }

        lock (Sync)
        {
            Entries.Add(message);
        }
    }

    public static List<string> Snapshot()
    {
        lock (Sync)
        {
            return [.. Entries];
        }
    }
}
