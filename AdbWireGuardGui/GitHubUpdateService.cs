using System.Net.Http.Headers;
using System.Text.Json;

namespace AdbWireGuardGui;

internal sealed class GitHubUpdateService
{
    private const string Owner = "kazek5p-git";
    private const string Repository = "AdbWireGuard";
    private const string ComponentsAssetName = "adb-wireguard-components.zip";

    private readonly HttpClient _httpClient;

    public GitHubUpdateService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AdbWireGuardGui", "1.0.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<GitHubReleaseInfo?> GetLatestComponentsReleaseAsync(CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("tag_name", out var tagElement))
        {
            return null;
        }

        var tagName = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        var version = ParseVersion(tagName);
        if (version is null)
        {
            return null;
        }

        if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assetsElement.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (!string.Equals(name, ComponentsAssetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var downloadUrl = asset.TryGetProperty("browser_download_url", out var urlElement)
                ? urlElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            var publishedAt = root.TryGetProperty("published_at", out var publishedElement)
                ? publishedElement.GetString()
                : string.Empty;

            return new GitHubReleaseInfo(version, tagName, ComponentsAssetName, downloadUrl, publishedAt ?? string.Empty);
        }

        return null;
    }

    public async Task<string> DownloadReleaseAssetAsync(GitHubReleaseInfo releaseInfo, CancellationToken cancellationToken)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AdbWireGuardGui", "downloads", releaseInfo.Version.ToString());
        Directory.CreateDirectory(tempDirectory);

        var destinationPath = Path.Combine(tempDirectory, releaseInfo.AssetName);
        using var response = await _httpClient.GetAsync(releaseInfo.DownloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        return destinationPath;
    }

    private static Version? ParseVersion(string tagName)
    {
        var trimmed = tagName.Trim();
        trimmed = trimmed.TrimStart('v', 'V');

        if (Version.TryParse(trimmed, out var version))
        {
            return version;
        }

        return null;
    }
}

internal sealed record GitHubReleaseInfo(
    Version Version,
    string TagName,
    string AssetName,
    string DownloadUrl,
    string PublishedAt);
