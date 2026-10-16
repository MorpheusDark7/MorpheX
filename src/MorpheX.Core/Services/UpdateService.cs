using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Serilog;

namespace MorpheX.Core.Services;

public sealed class UpdateInfo
{
    public required Version Version { get; init; }
    public required string TagName { get; init; }
    public required string Title { get; init; }
    public string? Changelog { get; init; }
    public required string DownloadUrl { get; init; }
    public required string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    /// <summary>
    /// URL of the .sha256 sidecar asset published alongside the installer.
    /// Presence is required — download is aborted if the asset is missing.
    /// </summary>
    public string? Sha256SidecarUrl { get; init; }
    /// <summary>Hex hash parsed from the .sha256 sidecar (set after fetch, not during check).</summary>
    public string? ExpectedSha256 { get; init; }

    public string FormattedSize => FileSizeBytes > 0
        ? $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB"
        : "Unknown size";
}

public interface IUpdateService
{
    Version CurrentVersion { get; }
    Task<UpdateInfo?> CheckForUpdatesAsync(string? authToken = null, CancellationToken ct = default);
    Task<string> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default);
    void LaunchInstaller(string installerPath, bool silent = false);
}

public sealed class UpdateService : IUpdateService
{
    private const string GitHubRepo = "MorpheusDark7/MorpheX";
    private readonly HttpClient _httpClient;

    public Version CurrentVersion { get; }

    public UpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MorpheX-Updater", "1.0"));

        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        CurrentVersion = assembly.GetName().Version ?? new Version(0, 1, 0);
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(string? authToken = null, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("GitHub update check returned {StatusCode} for {Url}", response.StatusCode, url);
                throw new HttpRequestException($"GitHub API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagProp))
            {
                return null;
            }

            string rawTag = tagProp.GetString() ?? "";
            string cleanTag = rawTag.TrimStart('v', 'V');
            int suffixIndex = cleanTag.IndexOfAny(new[] { '-', '+' });
            if (suffixIndex > 0)
            {
                cleanTag = cleanTag.Substring(0, suffixIndex);
            }

            if (!Version.TryParse(cleanTag, out var releaseVersion))
            {
                if (cleanTag.Count(c => c == '.') == 1)
                {
                    Version.TryParse(cleanTag + ".0", out releaseVersion);
                }
            }

            if (releaseVersion == null || releaseVersion <= CurrentVersion)
            {
                Log.Information("Current version {Current} is up to date (Latest: {Latest})",
                    CurrentVersion, releaseVersion ?? (object)"None");
                return null;
            }

            string? downloadUrl = null;
            string? fileName = null;
            long fileSize = 0;
            string? sha256SidecarUrl = null;

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                var assets = assetsProp.EnumerateArray().ToList();

                foreach (var asset in assets)
                {
                    var name = asset.GetProperty("name").GetString() ?? "";

                    // Security check: Match genuine installer executable naming
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        (name.StartsWith("MorpheX", StringComparison.OrdinalIgnoreCase) || name.Contains("Setup", StringComparison.OrdinalIgnoreCase)))
                    {
                        var rawUrl = asset.GetProperty("browser_download_url").GetString();
                        if (IsValidDownloadUrl(rawUrl))
                        {
                            downloadUrl = rawUrl;
                            fileName = name;
                            if (asset.TryGetProperty("size", out var sizeProp))
                            {
                                fileSize = sizeProp.GetInt64();
                            }
                        }
                        else
                        {
                            Log.Warning("Rejected installer download URL from untrusted domain: {Url}", rawUrl);
                        }
                    }
                }

                // Look for a .sha256 sidecar asset that matches the chosen installer file
                if (fileName != null)
                {
                    var sidecarName = fileName + ".sha256";
                    foreach (var asset in assets)
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (string.Equals(name, sidecarName, StringComparison.OrdinalIgnoreCase))
                        {
                            var rawUrl = asset.GetProperty("browser_download_url").GetString();
                            if (IsValidDownloadUrl(rawUrl))
                                sha256SidecarUrl = rawUrl;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
            {
                Log.Warning("New release {Tag} found, but no verified setup .exe asset found", rawTag);
                return null;
            }

            if (sha256SidecarUrl == null)
            {
                Log.Warning("New release {Tag}: installer found but no .sha256 sidecar asset. Update blocked for security.", rawTag);
                return null;
            }

            string title = root.TryGetProperty("name", out var titleProp) && !string.IsNullOrEmpty(titleProp.GetString())
                ? titleProp.GetString()!
                : $"MorpheX Live {rawTag}";

            string? changelog = root.TryGetProperty("body", out var bodyProp)
                ? bodyProp.GetString()
                : null;

            DateTimeOffset publishedAt = root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTimeOffset(out var dto)
                ? dto
                : DateTimeOffset.UtcNow;

            Log.Information("Update available: {Tag} (Current: {Current}). Asset: {Asset}",
                rawTag, CurrentVersion, fileName);

            return new UpdateInfo
            {
                Version = releaseVersion,
                TagName = rawTag,
                Title = title,
                Changelog = changelog,
                DownloadUrl = downloadUrl,
                FileName = fileName,
                FileSizeBytes = fileSize,
                PublishedAt = publishedAt,
                Sha256SidecarUrl = sha256SidecarUrl,
                ExpectedSha256 = null   // resolved in DownloadInstallerAsync after fetching sidecar
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates from GitHub");
            return null;
        }
    }

    private static bool IsValidDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;

        // Ensure host is either github.com or official GitHub release asset CDN
        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (!IsValidDownloadUrl(update.DownloadUrl))
            throw new InvalidOperationException($"Untrusted download URL: {update.DownloadUrl}");

        if (string.IsNullOrEmpty(update.Sha256SidecarUrl))
            throw new InvalidOperationException("No SHA-256 sidecar URL available. Cannot verify installer integrity.");

        // ── Step 1: Fetch the .sha256 sidecar first ───────────────────────────
        Log.Information("Fetching SHA-256 sidecar from {Url}", update.Sha256SidecarUrl);
        using var sidecarResp = await _httpClient.GetAsync(update.Sha256SidecarUrl, ct);
        sidecarResp.EnsureSuccessStatusCode();
        var sidecarText = (await sidecarResp.Content.ReadAsStringAsync(ct)).Trim();

        // Sidecar may be "<hex>  filename" (sha256sum format) or just the bare hex
        var hexMatch = Regex.Match(sidecarText, @"[a-fA-F0-9]{64}");
        if (!hexMatch.Success)
            throw new InvalidDataException($"SHA-256 sidecar did not contain a valid 64-char hex hash: '{sidecarText}'");

        var expectedHash = hexMatch.Value.ToLowerInvariant();
        Log.Information("Expected SHA-256 from sidecar: {Hash}", expectedHash);

        // ── Step 2: Download installer while computing its hash inline ─────────
        var tempDir = Path.Combine(Path.GetTempPath(), "MorpheX", "Updates");
        Directory.CreateDirectory(tempDir);
        var targetFile = Path.Combine(tempDir, update.FileName);

        if (File.Exists(targetFile))
        {
            try { File.Delete(targetFile); } catch { }
        }

        Log.Information("Downloading installer from {Url} to {Path}", update.DownloadUrl, targetFile);

        using var response = await _httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.FileSizeBytes;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            sha256.AppendData(buffer, 0, bytesRead);
            totalRead += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                double pct = (double)totalRead / totalBytes * 100.0;
                progress.Report(Math.Min(100.0, pct));
            }
        }

        await fileStream.FlushAsync(ct);
        fileStream.Close();

        // ── Step 3: Sanity-check minimum size ─────────────────────────────────
        if (totalRead < 500_000)
        {
            try { File.Delete(targetFile); } catch { }
            throw new InvalidDataException("Downloaded installer is suspiciously small or incomplete.");
        }

        // ── Step 4: Verify SHA-256 — mandatory, not optional ──────────────────
        var actualHash = Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant();
        Log.Information("Downloaded installer ({Size:F1} MB). SHA256: {Hash}",
            totalRead / (1024.0 * 1024.0), actualHash);

        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(targetFile); } catch { }
            throw new InvalidDataException(
                $"SHA-256 integrity check FAILED.\nExpected : {expectedHash}\nActual   : {actualHash}\nInstaller deleted.");
        }

        Log.Information("SHA-256 integrity verified. Installer is authentic.");
        progress?.Report(100.0);
        return targetFile;
    }

    public void LaunchInstaller(string installerPath, bool silent = false)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installer file not found", installerPath);
        }

        var fullPath = Path.GetFullPath(installerPath);
        // Append separator so "..\UpdatesEvil\" sibling paths can never pass the containment check
        var expectedDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MorpheX", "Updates"))
                          + Path.DirectorySeparatorChar;

        // Security check: installer must reside INSIDE the designated update directory and be an .exe
        if (!fullPath.StartsWith(expectedDir, StringComparison.OrdinalIgnoreCase) ||
            !fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Attempted to launch untrusted executable: {installerPath}");
        }

        var args = silent
            ? "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS=no"
            : "/CLOSEAPPLICATIONS /RESTARTAPPLICATIONS=no";

        Log.Information("Launching update installer: {Path} {Args}", installerPath, args);

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = args,
            UseShellExecute = true
        };

        Process.Start(psi);

        Environment.Exit(0);
    }
}
