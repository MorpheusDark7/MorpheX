using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        fileName = name;
                        if (asset.TryGetProperty("size", out var sizeProp))
                        {
                            fileSize = sizeProp.GetInt64();
                        }
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
            {
                Log.Warning("New release {Tag} found, but no setup .exe asset attached", rawTag);
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
                PublishedAt = publishedAt
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check for updates from GitHub");
            return null;
        }
    }

    public async Task<string> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MorpheX", "Updates");
        Directory.CreateDirectory(tempDir);
        var targetFile = Path.Combine(tempDir, update.FileName);

        if (File.Exists(targetFile))
        {
            try { File.Delete(targetFile); } catch { }
        }

        Log.Information("Downloading update from {Url} to {Path}", update.DownloadUrl, targetFile);

        using var response = await _httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.FileSizeBytes;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                double pct = (double)totalRead / totalBytes * 100.0;
                progress.Report(Math.Min(100.0, pct));
            }
        }

        progress?.Report(100.0);
        Log.Information("Downloaded update installer ({Size:F1} MB)", totalRead / (1024.0 * 1024.0));
        return targetFile;
    }

    public void LaunchInstaller(string installerPath, bool silent = false)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installer file not found", installerPath);
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
