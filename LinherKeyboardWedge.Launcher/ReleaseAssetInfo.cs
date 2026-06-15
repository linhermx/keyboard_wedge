namespace LinherKeyboardWedge.Launcher;

internal sealed record ReleaseAssetInfo(
    Version Version,
    string Tag,
    string AssetName,
    string DownloadUrl);
