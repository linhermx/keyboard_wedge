namespace RhinoKeyboardWedge.App.Configuration;

internal static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string LegacyDataDirectory { get; } = Path.Combine(LocalAppData, "RhinoKeyboardWedge");
    public static string DataDirectory { get; } = ResolveDataDirectory();
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");
    public static string ConfigPath { get; } = Path.Combine(DataDirectory, "config.json");

    private static string ResolveDataDirectory()
    {
        var currentDirectory = Path.Combine(LocalAppData, "LINHER", "KeyboardWedge");

        if (!Directory.Exists(currentDirectory) && Directory.Exists(LegacyDataDirectory))
        {
            CopyDirectory(LegacyDataDirectory, currentDirectory);
        }

        Directory.CreateDirectory(currentDirectory);
        return currentDirectory;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationFile = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: false);
        }
    }
}
