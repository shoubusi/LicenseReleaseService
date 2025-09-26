using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Provides comprehensive detection of installed SolidWorks versions through registry scanning and file system validation
    /// </summary>
    public class SolidWorksVersionDetector : IDisposable
    {
        private readonly ILogger<SolidWorksVersionDetector> _logger;
        private readonly List<SolidWorksVersionInfo> _detectedVersions;
        private readonly object _detectionLock = new object();
        private bool _disposed;
        private DateTime _lastDetectionTime;

        // Registry key paths for SolidWorks detection
        private static readonly string[] SolidWorksRegistryPaths =
        {
            @"SOFTWARE\SolidWorks",
            @"SOFTWARE\SolidWorks\Applications",
            @"SOFTWARE\Wow6432Node\SolidWorks",
            @"SOFTWARE\Wow6432Node\SolidWorks\Applications",
            @"SOFTWARE\SolidWorks Corp",
            @"SOFTWARE\Wow6432Node\SolidWorks Corp"
        };

        // Uninstall registry key paths
        private static readonly string[] UninstallRegistryPaths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        /// <summary>
        /// Gets the list of currently detected SolidWorks versions
        /// </summary>
        public IReadOnlyList<SolidWorksVersionInfo> DetectedVersions
        {
            get
            {
                lock (_detectionLock)
                {
                    return new List<SolidWorksVersionInfo>(_detectedVersions);
                }
            }
        }

        /// <summary>
        /// Gets the last time version detection was performed
        /// </summary>
        public DateTime LastDetectionTime => _lastDetectionTime;

        /// <summary>
        /// Initializes a new instance of the SolidWorksVersionDetector class
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public SolidWorksVersionDetector(ILogger<SolidWorksVersionDetector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _detectedVersions = new List<SolidWorksVersionInfo>();
            _lastDetectionTime = DateTime.MinValue;
        }

        /// <summary>
        /// Performs comprehensive detection of installed SolidWorks versions
        /// </summary>
        /// <returns>Result of the detection operation</returns>
        public async Task<VersionDetectionResult> DetectInstalledVersionsAsync()
        {
            try
            {
                _logger.LogInformation("Starting SolidWorks version detection");

                var result = new VersionDetectionResult();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Clear previous results
                lock (_detectionLock)
                {
                    _detectedVersions.Clear();
                }

                // Perform detection using multiple methods
                var registryVersions = await DetectFromRegistryAsync();
                var uninstallVersions = await DetectFromUninstallRegistryAsync();
                var filesystemVersions = await DetectFromFileSystemAsync();

                // Merge and deduplicate results
                var mergedVersions = MergeDetectionResults(registryVersions, uninstallVersions, filesystemVersions);

                // Validate each detected version
                foreach (var version in mergedVersions)
                {
                    await ValidateVersionAsync(version, result);
                }

                // Store results
                lock (_detectionLock)
                {
                    _detectedVersions.AddRange(mergedVersions);
                    _lastDetectionTime = DateTime.UtcNow;
                }

                stopwatch.Stop();
                result.DetectionTime = stopwatch.Elapsed;
                result.TotalVersions = mergedVersions.Count;
                result.AvailableVersions = mergedVersions.Count(v => v.IsAvailable);
                result.SupportedVersions = mergedVersions.Count(v => v.IsSupported);

                _logger.LogInformation("SolidWorks version detection completed in {Duration}ms. Found {Total} versions ({Available} available, {Supported} supported)",
                    stopwatch.ElapsedMilliseconds, result.TotalVersions, result.AvailableVersions, result.SupportedVersions);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SolidWorks version detection");
                return new VersionDetectionResult { Error = ex.Message };
            }
        }

        /// <summary>
        /// Detects SolidWorks versions from registry keys
        /// </summary>
        /// <returns>List of detected versions from registry</returns>
        private async Task<List<SolidWorksVersionInfo>> DetectFromRegistryAsync()
        {
            var versions = new List<SolidWorksVersionInfo>();

            foreach (var registryPath in SolidWorksRegistryPaths)
            {
                try
                {
                    await ScanRegistryKeyAsync(registryPath, versions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning registry path {RegistryPath}", registryPath);
                }
            }

            return versions;
        }

        /// <summary>
        /// Detects SolidWorks versions from uninstall registry
        /// </summary>
        /// <returns>List of detected versions from uninstall registry</returns>
        private async Task<List<SolidWorksVersionInfo>> DetectFromUninstallRegistryAsync()
        {
            var versions = new List<SolidWorksVersionInfo>();

            foreach (var registryPath in UninstallRegistryPaths)
            {
                try
                {
                    await ScanUninstallRegistryKeyAsync(registryPath, versions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning uninstall registry path {RegistryPath}", registryPath);
                }
            }

            return versions;
        }

        /// <summary>
        /// Detects SolidWorks versions from common file system locations
        /// </summary>
        /// <returns>List of detected versions from file system</returns>
        private async Task<List<SolidWorksVersionInfo>> DetectFromFileSystemAsync()
        {
            var versions = new List<SolidWorksVersionInfo>();

            // Common installation directories
            var commonPaths = new[]
            {
                @"C:\Program Files\SolidWorks Corp",
                @"C:\Program Files (x86)\SolidWorks Corp",
                @"D:\Program Files\SolidWorks Corp",
                @"D:\Program Files (x86)\SolidWorks Corp"
            };

            foreach (var basePath in commonPaths)
            {
                try
                {
                    if (Directory.Exists(basePath))
                    {
                        await ScanFileSystemAsync(basePath, versions);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning file system path {BasePath}", basePath);
                }
            }

            return versions;
        }

        /// <summary>
        /// Scans a registry key for SolidWorks installations
        /// </summary>
        /// <param name="registryPath">Registry path to scan</param>
        /// <param name="versions">List to populate with found versions</param>
        private async Task ScanRegistryKeyAsync(string registryPath, List<SolidWorksVersionInfo> versions)
        {
            using (var baseKey = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                if (baseKey == null) return;

                foreach (var keyName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        // Look for version patterns (e.g., "2025", "2024")
                        if (System.Text.RegularExpressions.Regex.IsMatch(keyName, @"^20[0-9]{2}$"))
                        {
                            var versionKey = baseKey.OpenSubKey(keyName);
                            if (versionKey != null)
                            {
                                var versionInfo = ExtractVersionFromRegistryKey(versionKey, keyName, registryPath);
                                if (versionInfo != null)
                                {
                                    versions.Add(versionInfo);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing registry key {KeyName} in {RegistryPath}", keyName, registryPath);
                    }
                }
            }
        }

        /// <summary>
        /// Scans uninstall registry for SolidWorks installations
        /// </summary>
        /// <param name="registryPath">Registry path to scan</param>
        /// <param name="versions">List to populate with found versions</param>
        private async Task ScanUninstallRegistryKeyAsync(string registryPath, List<SolidWorksVersionInfo> versions)
        {
            using (var baseKey = Registry.LocalMachine.OpenSubKey(registryPath))
            {
                if (baseKey == null) return;

                foreach (var keyName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        // Look for SolidWorks in the key name or display name
                        if (keyName.Contains("SolidWorks", StringComparison.OrdinalIgnoreCase))
                        {
                            var uninstallKey = baseKey.OpenSubKey(keyName);
                            if (uninstallKey != null)
                            {
                                var versionInfo = ExtractVersionFromUninstallKey(uninstallKey, keyName);
                                if (versionInfo != null)
                                {
                                    versions.Add(versionInfo);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing uninstall key {KeyName} in {RegistryPath}", keyName, registryPath);
                    }
                }
            }
        }

        /// <summary>
        /// Scans file system for SolidWorks installations
        /// </summary>
        /// <param name="basePath">Base path to scan</param>
        /// <param name="versions">List to populate with found versions</param>
        private async Task ScanFileSystemAsync(string basePath, List<SolidWorksVersionInfo> versions)
        {
            try
            {
                var directories = Directory.GetDirectories(basePath);
                foreach (var directory in directories)
                {
                    var dirName = Path.GetFileName(directory);

                    // Look for version patterns in directory names
                    if (System.Text.RegularExpressions.Regex.IsMatch(dirName, @"^SolidWorks.*20[0-9]{2}"))
                    {
                        var versionMatch = System.Text.RegularExpressions.Regex.Match(dirName, @"20[0-9]{2}");
                        if (versionMatch.Success)
                        {
                            var version = versionMatch.Value;
                            var versionInfo = new SolidWorksVersionInfo(version, directory);

                            // Try to find the executable
                            var exePath = Path.Combine(directory, "SLDWORKS.exe");
                            if (File.Exists(exePath))
                            {
                                versionInfo.ExecutablePath = exePath;
                                versionInfo.Status = InstallationStatus.Installed;
                                versions.Add(versionInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning file system directory {BasePath}", basePath);
            }
        }

        /// <summary>
        /// Extracts version information from a registry key
        /// </summary>
        /// <param name="key">Registry key containing version information</param>
        /// <param name="version">Version string</param>
        /// <param name="registryPath">Full registry path</param>
        /// <returns>Version information or null if not found</returns>
        private SolidWorksVersionInfo ExtractVersionFromRegistryKey(RegistryKey key, string version, string registryPath)
        {
            try
            {
                var installPath = key.GetValue("InstallPath") as string;
                if (string.IsNullOrWhiteSpace(installPath))
                    return null;

                var versionInfo = new SolidWorksVersionInfo(version, installPath)
                {
                    RegistryKeyPath = registryPath,
                    Is64Bit = registryPath.Contains("Wow6432Node") == false
                };

                // Extract additional information
                versionInfo.FullVersion = key.GetValue("ProductName") as string ?? $"SolidWorks {version}";
                versionInfo.ServicePack = key.GetValue("ServicePack") as string;
                versionInfo.BuildNumber = key.GetValue("BuildNumber") as string;

                // Parse installation date
                if (key.GetValue("InstallDate") is string installDateString)
                {
                    if (DateTime.TryParse(installDateString, out var installDate))
                    {
                        versionInfo.InstallationDate = installDate;
                    }
                }

                // Set executable and lmutil paths
                versionInfo.ExecutablePath = Path.Combine(installPath, "SLDWORKS.exe");

                // Look for license manager in common locations
                var lmutilPaths = new[]
                {
                    Path.Combine(installPath, "..", "..", "SolidNetWork License Manager", "utils", "lmutil.exe"),
                    Path.Combine(installPath, "..", "SolidNetWork License Manager", "utils", "lmutil.exe"),
                    Path.Combine(Path.GetDirectoryName(installPath), "SolidNetWork License Manager", "utils", "lmutil.exe")
                };

                foreach (var lmutilPath in lmutilPaths)
                {
                    if (File.Exists(lmutilPath))
                    {
                        versionInfo.LmutilPath = lmutilPath;
                        break;
                    }
                }

                // Add metadata
                versionInfo.Metadata["RegistryPath"] = registryPath;
                versionInfo.Metadata["Source"] = "Registry";
                versionInfo.Metadata["Architecture"] = versionInfo.Is64Bit ? "64-bit" : "32-bit";

                return versionInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting version information from registry key for version {Version}", version);
                return null;
            }
        }

        /// <summary>
        /// Extracts version information from an uninstall registry key
        /// </summary>
        /// <param name="key">Uninstall registry key</param>
        /// <param name="keyName">Key name</param>
        /// <returns>Version information or null if not found</returns>
        private SolidWorksVersionInfo ExtractVersionFromUninstallKey(RegistryKey key, string keyName)
        {
            try
            {
                var displayName = key.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName) || !displayName.Contains("SolidWorks"))
                    return null;

                // Extract version from display name
                var versionMatch = System.Text.RegularExpressions.Regex.Match(displayName, @"20[0-9]{2}");
                if (!versionMatch.Success)
                    return null;

                var version = versionMatch.Value;
                var installPath = key.GetValue("InstallLocation") as string;

                if (string.IsNullOrWhiteSpace(installPath))
                    return null;

                var versionInfo = new SolidWorksVersionInfo(version, installPath)
                {
                    FullVersion = displayName,
                    RegistryKeyPath = keyName
                };

                // Extract additional information
                versionInfo.ExecutablePath = Path.Combine(installPath, "SLDWORKS.exe");
                versionInfo.InstallationDate = ParseInstallDate(key.GetValue("InstallDate") as string);
                versionInfo.Version = version;

                // Add metadata
                versionInfo.Metadata["UninstallKey"] = keyName;
                versionInfo.Metadata["Source"] = "UninstallRegistry";
                versionInfo.Metadata["Publisher"] = key.GetValue("Publisher") as string;
                versionInfo.Metadata["DisplayVersion"] = key.GetValue("DisplayVersion") as string;

                return versionInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting version information from uninstall key {KeyName}", keyName);
                return null;
            }
        }

        /// <summary>
        /// Validates a detected version and checks its health
        /// </summary>
        /// <param name="version">Version to validate</param>
        /// <param name="result">Detection result to update</param>
        private async Task ValidateVersionAsync(SolidWorksVersionInfo version, VersionDetectionResult result)
        {
            try
            {
                var validation = version.Validate();

                if (!validation.IsValid)
                {
                    result.Errors.AddRange(validation.Errors);
                }

                if (validation.HasIssues)
                {
                    result.Warnings.AddRange(validation.Warnings);
                }

                // Check file system accessibility
                if (Directory.Exists(version.InstallationPath))
                {
                    version.Status = InstallationStatus.Installed;
                    version.UpdateHealth(VersionHealth.Healthy);
                }
                else
                {
                    version.Status = InstallationStatus.NotInstalled;
                    version.UpdateHealth(VersionHealth.Unavailable);
                    result.Warnings.Add($"Installation path not accessible: {version.InstallationPath}");
                }

                // Check executable accessibility
                if (!string.IsNullOrWhiteSpace(version.ExecutablePath) && !File.Exists(version.ExecutablePath))
                {
                    version.UpdateHealth(VersionHealth.Degraded);
                    result.Warnings.Add($"Executable not found: {version.ExecutablePath}");
                }

                // Check license manager accessibility
                if (!string.IsNullOrWhiteSpace(version.LmutilPath) && !File.Exists(version.LmutilPath))
                {
                    version.UpdateHealth(VersionHealth.LicenseIssues);
                    result.Warnings.Add($"License manager utility not found: {version.LmutilPath}");
                }

                // Add to result statistics
                if (version.IsAvailable)
                {
                    result.AvailableVersions++;
                }

                if (version.IsSupported)
                {
                    result.SupportedVersions++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating version {Version}", version.Version);
                result.Warnings.Add($"Validation error for version {version.Version}: {ex.Message}");
                version.UpdateHealth(VersionHealth.Corrupted);
            }
        }

        /// <summary>
        /// Merges detection results from multiple sources, removing duplicates
        /// </summary>
        /// <param name="registryVersions">Versions from registry</param>
        /// <param name="uninstallVersions">Versions from uninstall registry</param>
        /// <param name="filesystemVersions">Versions from file system</param>
        /// <returns>Merged and deduplicated list of versions</returns>
        private List<SolidWorksVersionInfo> MergeDetectionResults(
            List<SolidWorksVersionInfo> registryVersions,
            List<SolidWorksVersionInfo> uninstallVersions,
            List<SolidWorksVersionInfo> filesystemVersions)
        {
            var merged = new List<SolidWorksVersionInfo>();
            var processedVersions = new HashSet<string>();

            // Process all versions from all sources
            var allVersions = registryVersions.Concat(uninstallVersions).Concat(filesystemVersions);

            foreach (var version in allVersions)
            {
                var key = $"{version.Version}_{version.InstallationPath}";

                if (!processedVersions.Contains(key))
                {
                    // Check if we already have this version with more complete information
                    var existing = merged.FirstOrDefault(v => v.Version == version.Version &&
                                                           v.InstallationPath.Equals(version.InstallationPath, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        merged.Add(version);
                    }
                    else
                    {
                        // Merge information - use the most complete version
                        MergeVersionInformation(existing, version);
                    }

                    processedVersions.Add(key);
                }
            }

            // Sort by version (newest first)
            merged.Sort((a, b) => b.Version.CompareTo(a.Version));

            return merged;
        }

        /// <summary>
        /// Merges information from two version objects
        /// </summary>
        /// <param name="target">Target version to merge into</param>
        /// <param name="source">Source version to merge from</param>
        private void MergeVersionInformation(SolidWorksVersionInfo target, SolidWorksVersionInfo source)
        {
            // Prefer registry information over file system information
            if (!string.IsNullOrWhiteSpace(source.RegistryKeyPath))
            {
                target.RegistryKeyPath = source.RegistryKeyPath;
            }

            // Fill in missing information
            if (string.IsNullOrWhiteSpace(target.ExecutablePath) && !string.IsNullOrWhiteSpace(source.ExecutablePath))
            {
                target.ExecutablePath = source.ExecutablePath;
            }

            if (string.IsNullOrWhiteSpace(target.LmutilPath) && !string.IsNullOrWhiteSpace(source.LmutilPath))
            {
                target.LmutilPath = source.LmutilPath;
            }

            if (string.IsNullOrWhiteSpace(target.FullVersion) && !string.IsNullOrWhiteSpace(source.FullVersion))
            {
                target.FullVersion = source.FullVersion;
            }

            if (string.IsNullOrWhiteSpace(target.ServicePack) && !string.IsNullOrWhiteSpace(source.ServicePack))
            {
                target.ServicePack = source.ServicePack;
            }

            if (string.IsNullOrWhiteSpace(target.BuildNumber) && !string.IsNullOrWhiteSpace(source.BuildNumber))
            {
                target.BuildNumber = source.BuildNumber;
            }

            if (!target.InstallationDate.HasValue && source.InstallationDate.HasValue)
            {
                target.InstallationDate = source.InstallationDate;
            }

            // Merge metadata
            foreach (var kvp in source.Metadata)
            {
                if (!target.Metadata.ContainsKey(kvp.Key))
                {
                    target.Metadata[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// Parses installation date from various formats
        /// </summary>
        /// <param name="installDateString">Installation date string</param>
        /// <returns>Parsed date or null</returns>
        private DateTime? ParseInstallDate(string installDateString)
        {
            if (string.IsNullOrWhiteSpace(installDateString))
                return null;

            // Try various date formats
            var formats = new[]
            {
                "yyyyMMdd",
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "dd/MM/yyyy",
                "yyyy/MM/dd"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(installDateString, format, null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a specific version by its version string
        /// </summary>
        /// <param name="version">Version to retrieve</param>
        /// <returns>Version information or null if not found</returns>
        public SolidWorksVersionInfo GetVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return null;

            lock (_detectionLock)
            {
                return _detectedVersions.FirstOrDefault(v => v.Version == version);
            }
        }

        /// <summary>
        /// Gets all available (healthy and installed) versions
        /// </summary>
        /// <returns>List of available versions</returns>
        public List<SolidWorksVersionInfo> GetAvailableVersions()
        {
            lock (_detectionLock)
            {
                return _detectedVersions.Where(v => v.IsAvailable).ToList();
            }
        }

        /// <summary>
        /// Gets all supported versions within the supported range
        /// </summary>
        /// <returns>List of supported versions</returns>
        public List<SolidWorksVersionInfo> GetSupportedVersions()
        {
            lock (_detectionLock)
            {
                return _detectedVersions.Where(v => v.IsSupported).ToList();
            }
        }

        /// <summary>
        /// Refreshes the version detection
        /// </summary>
        /// <returns>Updated detection result</returns>
        public async Task<VersionDetectionResult> RefreshDetectionAsync()
        {
            return await DetectInstalledVersionsAsync();
        }

        /// <summary>
        /// Disposes the detector and cleans up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the detector and cleans up resources
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_detectionLock)
                    {
                        _detectedVersions.Clear();
                    }
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents the result of a version detection operation
    /// </summary>
    public class VersionDetectionResult
    {
        /// <summary>
        /// Gets the time taken for detection
        /// </summary>
        public TimeSpan DetectionTime { get; set; }

        /// <summary>
        /// Gets the total number of versions detected
        /// </summary>
        public int TotalVersions { get; set; }

        /// <summary>
        /// Gets the number of available versions
        /// </summary>
        public int AvailableVersions { get; set; }

        /// <summary>
        /// Gets the number of supported versions
        /// </summary>
        public int SupportedVersions { get; set; }

        /// <summary>
        /// Gets the list of detection errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of detection warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets the error message if detection failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets a value indicating whether detection was successful
        /// </summary>
        public bool Success => string.IsNullOrWhiteSpace(Error);

        /// <summary>
        /// Gets a value indicating whether detection has any issues
        /// </summary>
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Initializes a new instance of the VersionDetectionResult class
        /// </summary>
        public VersionDetectionResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            DetectionTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Gets a summary string of the detection result
        /// </summary>
        /// <returns>Summary string</returns>
        public string GetSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Version Detection Result ({DetectionTime.TotalMilliseconds:F0}ms):");
            summary.AppendLine($"  Total Versions: {TotalVersions}");
            summary.AppendLine($"  Available Versions: {AvailableVersions}");
            summary.AppendLine($"  Supported Versions: {SupportedVersions}");
            summary.AppendLine($"  Success: {Success}");

            if (HasIssues)
            {
                summary.AppendLine();
                if (Errors.Count > 0)
                {
                    summary.AppendLine("Errors:");
                    foreach (var error in Errors)
                    {
                        summary.AppendLine($"  - {error}");
                    }
                }

                if (Warnings.Count > 0)
                {
                    summary.AppendLine("Warnings:");
                    foreach (var warning in Warnings)
                    {
                        summary.AppendLine($"  - {warning}");
                    }
                }
            }

            return summary.ToString();
        }
    }
}