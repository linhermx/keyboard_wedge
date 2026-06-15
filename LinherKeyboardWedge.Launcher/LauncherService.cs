using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinherKeyboardWedge.Launcher;

internal sealed class LauncherService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly LauncherPaths _paths;
    private readonly string _baseDirectory;

    public LauncherService()
    {
        _paths = LauncherPaths.Create();
        _baseDirectory = AppContext.BaseDirectory;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("LINHER-Keyboard-Wedge-Launcher", "1.0.1"));
    }

    public async Task<InstalledApp> EnsureLatestInstalledAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Buscando instalación disponible...");
        var installed = FindInstalledApp();

        progress.Report("Consultando actualización...");
        ReleaseAssetInfo? latestRelease = null;

        try
        {
            latestRelease = await TryGetLatestReleaseAsync(cancellationToken);
        }
        catch
        {
            latestRelease = null;
        }

        if (latestRelease is not null && (installed is null || latestRelease.Version > installed.Version))
        {
            progress.Report($"Descargando {latestRelease.Tag}...");
            var downloadedZip = await DownloadReleaseAssetAsync(latestRelease, cancellationToken);
            progress.Report($"Instalando {latestRelease.Tag}...");
            return InstallFromZip(downloadedZip, latestRelease.Version);
        }

        if (installed is not null)
        {
            progress.Report($"Abriendo versión instalada {FormatTag(installed.Version)}...");
            return installed;
        }

        var bundled = LoadBundledRelease();
        if (bundled is not null)
        {
            progress.Report($"Instalando versión incluida {bundled.Tag}...");
            return InstallFromZip(bundled.ZipPath, bundled.Version);
        }

        throw new InvalidOperationException(
            "No se encontró una versión instalada ni un paquete incluido, y no fue posible obtener un release desde GitHub.");
    }

    public void Launch(InstalledApp app)
    {
        if (!File.Exists(app.ExecutablePath))
        {
            throw new FileNotFoundException("No se encontró el ejecutable de la aplicación.", app.ExecutablePath);
        }

        Process.Start(new ProcessStartInfo(app.ExecutablePath)
        {
            WorkingDirectory = app.DirectoryPath,
            UseShellExecute = true
        });
    }

    private InstalledApp? FindInstalledApp()
    {
        if (!Directory.Exists(_paths.AppDirectory))
        {
            return null;
        }

        var candidates = new List<InstalledApp>();

        foreach (var directory in Directory.GetDirectories(_paths.AppDirectory))
        {
            var name = Path.GetFileName(directory);
            if (!TryParseVersionFromDirectory(name, out var version))
            {
                continue;
            }

            var executablePath = Path.Combine(directory, LauncherConstants.AppExeName);
            if (!File.Exists(executablePath))
            {
                continue;
            }

            candidates.Add(new InstalledApp(version, directory, executablePath));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();
    }

    private BundledReleaseInfo? LoadBundledRelease()
    {
        var metadataPath = Path.Combine(
            _baseDirectory,
            LauncherConstants.BundledAssetsDirectoryName,
            LauncherConstants.BundledReleaseMetadataFileName);

        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var metadata = JsonSerializer.Deserialize<BundledReleaseMetadata>(
            File.ReadAllText(metadataPath),
            JsonOptions);

        if (metadata is null ||
            string.IsNullOrWhiteSpace(metadata.Tag) ||
            string.IsNullOrWhiteSpace(metadata.AssetName) ||
            !TryParseVersionTag(metadata.Tag, out var version))
        {
            return null;
        }

        var zipPath = Path.Combine(
            _baseDirectory,
            LauncherConstants.BundledAssetsDirectoryName,
            metadata.AssetName);

        return File.Exists(zipPath)
            ? new BundledReleaseInfo(version, metadata.Tag, metadata.AssetName, zipPath)
            : null;
    }

    private async Task<ReleaseAssetInfo?> TryGetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(LauncherConstants.LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(stream, JsonOptions, cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.TagName))
        {
            return null;
        }

        if (!TryParseVersionTag(payload.TagName, out var version))
        {
            return null;
        }

        var asset = payload.Assets?
            .FirstOrDefault(item => string.Equals(
                item.Name,
                LauncherConstants.RequiredReleaseAssetName,
                StringComparison.OrdinalIgnoreCase));

        if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            return null;
        }

        return new ReleaseAssetInfo(
            version,
            payload.TagName,
            asset.Name,
            asset.BrowserDownloadUrl);
    }

    private async Task<string> DownloadReleaseAssetAsync(ReleaseAssetInfo release, CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(_paths.DownloadsDirectory, release.AssetName);
        var tempPath = $"{targetPath}.part";

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        using var response = await _httpClient.GetAsync(
            release.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = File.Create(tempPath))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        File.Move(tempPath, targetPath, overwrite: true);
        return targetPath;
    }

    private InstalledApp InstallFromZip(string zipPath, Version version)
    {
        var targetDirectory = Path.Combine(
            _paths.AppDirectory,
            $"{LauncherConstants.AppDirectoryPrefix}{version.Major}.{version.Minor}.{version.Build}");
        var executablePath = Path.Combine(targetDirectory, LauncherConstants.AppExeName);

        if (File.Exists(executablePath))
        {
            return new InstalledApp(version, targetDirectory, executablePath);
        }

        var stagingDirectory = Path.Combine(
            _paths.TempDirectory,
            $"install-{Guid.NewGuid():N}");

        Directory.CreateDirectory(stagingDirectory);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, stagingDirectory);

            var extractedExecutable = Directory
                .GetFiles(stagingDirectory, LauncherConstants.AppExeName, SearchOption.AllDirectories)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"El paquete no contiene {LauncherConstants.AppExeName}.");

            var extractedRoot = Path.GetDirectoryName(extractedExecutable)
                ?? throw new InvalidOperationException("No se pudo determinar la carpeta del paquete.");

            Directory.CreateDirectory(targetDirectory);
            CopyDirectory(extractedRoot, targetDirectory);
            return new InstalledApp(version, targetDirectory, Path.Combine(targetDirectory, LauncherConstants.AppExeName));
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
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
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static bool TryParseVersionFromDirectory(string name, out Version version)
    {
        version = new Version(0, 0, 0);
        if (!name.StartsWith(LauncherConstants.AppDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var versionText = name[LauncherConstants.AppDirectoryPrefix.Length..];
        if (!Version.TryParse(versionText, out var parsedVersion) || parsedVersion is null)
        {
            return false;
        }

        version = parsedVersion;
        return true;
    }

    private static bool TryParseVersionTag(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        var normalized = tag.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        if (!Version.TryParse(normalized, out var parsedVersion) || parsedVersion is null)
        {
            return false;
        }

        version = parsedVersion;
        return true;
    }

    private static string FormatTag(Version version)
    {
        return $"v{version.Major}.{version.Minor}.{version.Build}";
    }

    private sealed class BundledReleaseMetadata
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("asset_name")]
        public string AssetName { get; set; } = string.Empty;
    }

    private sealed class GitHubReleaseResponse
    {
        public string TagName { get; set; } = string.Empty;

        public GitHubReleaseAsset[] Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        public string Name { get; set; } = string.Empty;

        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
