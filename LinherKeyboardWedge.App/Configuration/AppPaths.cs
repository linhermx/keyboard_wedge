namespace LinherKeyboardWedge.App.Configuration;

internal static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string DataDirectory { get; } = ResolveDataDirectory();
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");
    public static string ConfigPath { get; } = Path.Combine(DataDirectory, "config.json");

    private static string ResolveDataDirectory()
    {
        var currentDirectory = Path.Combine(LocalAppData, "LINHER", "KeyboardWedge");

        foreach (var legacyDirectory in GetLegacyDirectories())
        {
            if (!Directory.Exists(currentDirectory) && Directory.Exists(legacyDirectory))
            {
                CopyDirectory(legacyDirectory, currentDirectory);
            }
        }

        Directory.CreateDirectory(currentDirectory);
        return currentDirectory;
    }

    private static IEnumerable<string> GetLegacyDirectories()
    {
        yield return Path.Combine(LocalAppData, "RhinoKeyboardWedge");
        yield return Path.Combine(LocalAppData, "LinherKeyboardWedge");
        yield return Path.Combine(LocalAppData, "LinherKeyboardWedger");
        yield return Path.Combine(LocalAppData, "LINHER", "KeyboardWedger");
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
