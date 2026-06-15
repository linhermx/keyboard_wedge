using System.Text.Json;
using RhinoKeyboardWedge.App.Logging;

namespace RhinoKeyboardWedge.App.Configuration;

internal sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly DailyFileLogger _logger;

    public ConfigService(string path, DailyFileLogger logger)
    {
        _path = path;
        _logger = logger;
    }

    public AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            if (!File.Exists(_path))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception exception)
        {
            _logger.LogError("CONFIG_LOAD", exception);
            TryBackupInvalidConfig();
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }

    private void TryBackupInvalidConfig()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var backupPath = $"{_path}.invalid-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(_path, backupPath);
        }
        catch
        {
            // The application can continue with defaults even when this backup fails.
        }
    }
}
