using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace CryptoSelfBot.Updater
{
    public class UpdateService
    {
        private const string GitHubOwner = "TU_USUARIO";
        private const string GitHubRepo = "CryptoSelfBot";
        private const string ReleasesUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

        private readonly HttpClient _client = new();

        public async Task<ReleaseInfo?> CheckForUpdateAsync()
        {
            try
            {
                _client.DefaultRequestHeaders.UserAgent.TryParseAdd("CryptoSelfBot-Updater/1.0");
                string json = await _client.GetStringAsync(ReleasesUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                if (release == null) return null;

                Version? latest = ParseVersion(release.TagName);
                Version? current = GetCurrentVersion();

                if (latest == null || current == null) return null;
                if (latest <= current) return null;

                var asset = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".msi"));
                if (asset == null) return null;

                return new ReleaseInfo
                {
                    Version = latest.ToString(),
                    DownloadUrl = asset.BrowserDownloadUrl,
                    FileName = asset.Name,
                    Size = asset.Size,
                    Hash = release.Body?.Split('\n').FirstOrDefault(l => l.StartsWith("SHA256:"))?.Replace("SHA256:", "").Trim()
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> DownloadUpdateAsync(ReleaseInfo info, IProgress<int> progress)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "CryptoSelfBot_Update");
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, info.FileName);

            using var response = await _client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            var buffer = new byte[8192];
            int bytesRead;
            long totalRead = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;
                if (totalBytes > 0)
                    progress.Report((int)(totalRead * 100 / totalBytes));
            }

            // Verificar hash si está disponible
            if (!string.IsNullOrEmpty(info.Hash))
            {
                string fileHash = ComputeSha256(filePath);
                if (!string.Equals(fileHash, info.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(filePath);
                    return null;
                }
            }

            return filePath;
        }

        public void ApplyUpdate(string msiPath)
        {
            System.Diagnostics.Process.Start("msiexec.exe", $"/i \"{msiPath}\" /quiet /norestart");
        }

        private Version? GetCurrentVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version;
            }
            catch { return new Version(1, 0, 0); }
        }

        private Version? ParseVersion(string? tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return null;
            string v = tagName.StartsWith("v") ? tagName[1..] : tagName;
            return Version.TryParse(v, out var ver) ? ver : null;
        }

        private string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }
    }

    public class ReleaseInfo
    {
        public string Version { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string FileName { get; set; } = "";
        public long Size { get; set; }
        public string? Hash { get; set; }
    }

    public class GitHubRelease
    {
        public string? TagName { get; set; }
        public string? Body { get; set; }
        public List<GitHubAsset>? Assets { get; set; }
    }

    public class GitHubAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
    }
}