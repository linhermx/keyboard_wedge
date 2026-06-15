using Microsoft.Win32;

namespace LinherKeyboardWedge.App.Services;

internal sealed class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LINHERKeyboardWedge";
    private static readonly string StableLauncherPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "LINHER Keyboard Wedge",
        "LinherKeyboardWedgeLauncher.exe");

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var executablePath = File.Exists(StableLauncherPath)
                ? StableLauncherPath
                : (Environment.ProcessPath ?? Application.ExecutablePath);
            key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
