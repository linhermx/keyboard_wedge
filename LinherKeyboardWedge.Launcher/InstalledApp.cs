namespace LinherKeyboardWedge.Launcher;

internal sealed record InstalledApp(
    Version Version,
    string DirectoryPath,
    string ExecutablePath);
