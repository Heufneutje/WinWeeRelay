﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace WinWeelay.Utils
{
    public class UpdateHelper : IDisposable
    {
        private const string _GITHUB_BASE_URL = "https://api.github.com";
        public string InstallerFilePath { get; private set; } = Path.Combine(Path.GetTempPath(), "WinWeelaySetup.exe");

        private WebClient _client;

        public event DownloadProgressChangedEventHandler ProgressChanged;
        public event AsyncCompletedEventHandler DownloadCompleted;

        public UpdateHelper()
        {
            _client = new WebClient();
            SetHeaders(_client);
        }

        public UpdateCheckResult CheckForUpdate()
        {
            try
            {
                string json = _client.DownloadString($"{_GITHUB_BASE_URL}/repos/heufneutje/winweelay/releases");

                IEnumerable<GitHubRelease> releases = JsonUtils.DeserializeObject<List<GitHubRelease>>(json).Where(x => !x.Prerelease);
                if (!releases.Any())
                    return null;

                GitHubRelease latestVersion = releases.First();
                string latestVersionStr = latestVersion.TagName.Substring(1);
                FileVersionInfo fvi = GetCurrentVersion();
                string currentVersion = string.Join(".", new int[3] { fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart });
                if (VersionsEqual(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, latestVersionStr))
                    return new UpdateCheckResult(UpdateResultType.NoUpdateAvailable, $"Current version: {currentVersion}{Environment.NewLine}{Environment.NewLine}You're running the latest version of WinWeelay.", "No update available", null);

                StringBuilder updateTextBuilder = new StringBuilder();
                updateTextBuilder.AppendLine($"Current version: {currentVersion}");
                updateTextBuilder.AppendLine($"Latest version: {latestVersionStr}");
                updateTextBuilder.AppendLine();
                updateTextBuilder.AppendLine($"Changelog:");
                updateTextBuilder.AppendLine(latestVersion.Body);
                updateTextBuilder.AppendLine();
                updateTextBuilder.AppendLine("Would you like to download this version now?");

                return new UpdateCheckResult(UpdateResultType.UpdateAvailable, updateTextBuilder.ToString(), "Update available", latestVersion.Assets.First().BrowserDownloadUrl);
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult(UpdateResultType.Error, $"An error occurred while downloading the update file.{Environment.NewLine}{ex.Message}", "Error", null);
            }
        }

        public void DownloadUpdate(string downloadUrl)
        {
            string filePath = InstallerFilePath;
            if (File.Exists(filePath))
                File.Delete(filePath);

            _client.DownloadProgressChanged += Client_DownloadProgressChanged;
            _client.DownloadFileCompleted += Client_DownloadFileCompleted;
            _client.DownloadFileAsync(new Uri(downloadUrl), filePath); 
        }

        private void Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            DownloadCompleted?.Invoke(sender, e);
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgressChanged?.Invoke(sender, e);
        }

        public string GetApplicationDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        }

        public static FileVersionInfo GetCurrentVersion()
        {
            return FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
        }

        private bool VersionsEqual(int curMajor, int curMinor, int curPatch, string latest)
        {
            string[] parts = latest.Split('.');
            if (Convert.ToInt32(parts[0]) > curMajor)
                return false;

            if (Convert.ToInt32(parts[1]) > curMinor)
                return false;

            if (Convert.ToInt32(parts[2]) > curPatch)
                return false;

            return true;
        }

        private void SetHeaders(WebClient client)
        {
            client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
        }

        #region IDisposable Support
        private bool _disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                    _client.Dispose();

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
