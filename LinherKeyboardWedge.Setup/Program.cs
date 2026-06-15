using System.Diagnostics;
using System.Reflection;

namespace LinherKeyboardWedge.Setup;

internal static class Program
{
    private const string AppExeName = "LinherKeyboardWedgeLauncher.exe";
    private const string AppDisplayName = "LINHER Keyboard Wedge";
    private const string PayloadPrefix = "Payload/";

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "LINHER Keyboard Wedge");
            var startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Start Menu",
                "Programs",
                "LINHER Keyboard Wedge");

            Directory.CreateDirectory(installDir);
            Directory.CreateDirectory(startMenuDir);

            ExtractPayload(installDir);

            var exePath = Path.Combine(installDir, AppExeName);
            CreateShortcut(Path.Combine(startMenuDir, $"{AppDisplayName}.lnk"), exePath, installDir);

            Process.Start(new ProcessStartInfo(exePath)
            {
                WorkingDirectory = installDir,
                UseShellExecute = true
            });

            MessageBox.Show(
                $"{AppDisplayName} se instaló correctamente.",
                "Instalación completada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Error de instalación",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void ExtractPayload(string installDir)
    {
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(PayloadPrefix, StringComparison.Ordinal)))
        {
            var relativePath = resourceName[PayloadPrefix.Length..]
                .Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.Combine(installDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            using var resource = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"No se encontró el recurso {resourceName}.");
            using var output = File.Create(destinationPath);
            resource.CopyTo(output);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("No se pudo crear WScript.Shell.");
            dynamic shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("No se pudo crear WScript.Shell.");
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = targetPath;
            shortcut.Save();
        }
        catch
        {
            var fallbackPath = Path.ChangeExtension(shortcutPath, ".cmd");
            File.WriteAllText(fallbackPath, $"@echo off\r\nstart \"\" \"{targetPath}\"\r\n");
        }
    }
}
