namespace LinherKeyboardWedge.Launcher;

internal sealed class LauncherPaths
{
    public LauncherPaths(string root)
    {
        Root = root;
        AppDirectory = Path.Combine(root, "app");
        DownloadsDirectory = Path.Combine(root, "downloads");
        TempDirectory = Path.Combine(root, "temp");
    }

    public string Root { get; }

    public string AppDirectory { get; }

    public string DownloadsDirectory { get; }

    public string TempDirectory { get; }

    public static LauncherPaths Create()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(LauncherConstants.RuntimeRootEnvVar)?.Trim();
        var root = string.IsNullOrWhiteSpace(overrideRoot)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LINHER",
                "KeyboardWedge")
            : overrideRoot;

        var paths = new LauncherPaths(root);
        Directory.CreateDirectory(paths.Root);
        Directory.CreateDirectory(paths.AppDirectory);
        Directory.CreateDirectory(paths.DownloadsDirectory);
        Directory.CreateDirectory(paths.TempDirectory);
        return paths;
    }
}
