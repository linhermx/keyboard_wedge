namespace LinherKeyboardWedge.Launcher;

internal sealed record BundledReleaseInfo(
    Version Version,
    string Tag,
    string AssetName,
    string ZipPath);
