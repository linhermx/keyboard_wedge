namespace RhinoKeyboardWedge.App.Logging;

internal sealed class DailyFileLogger : IDisposable
{
    private readonly object _sync = new();
    private readonly string _logDirectory;

    public DailyFileLogger(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public void LogReading(DateTime timestamp, string raw, string? quantity, string result)
    {
        var normalizedRaw = Normalize(raw);
        var qty = string.IsNullOrWhiteSpace(quantity) ? "-" : quantity;
        WriteLine($"{timestamp:yyyy-MM-dd HH:mm:ss} | RAW=\"{normalizedRaw}\" | QTY={qty} | {result}");
    }

    public void LogInfo(string source, string message)
    {
        WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {source} | {Normalize(message)}");
    }

    public void LogError(string source, Exception exception)
    {
        WriteLine(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {source} | TYPE=\"{exception.GetType().FullName}\" | HResult=0x{exception.HResult:X8} | ERROR=\"{Normalize(exception.Message)}\"");
    }

    public void Dispose()
    {
    }

    private void WriteLine(string line)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private static string Normalize(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\"", "'", StringComparison.Ordinal)
            .Trim();
    }
}
