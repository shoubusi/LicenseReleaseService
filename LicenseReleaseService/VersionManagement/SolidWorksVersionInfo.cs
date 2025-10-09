using System;
using System.Collections.Generic;

namespace LicenseReleaseService.VersionManagement
{
    /// <summary>
    /// Represents comprehensive information about a detected SolidWorks version installation
    /// </summary>
    public class SolidWorksVersionInfo
    {
        /// <summary>
        /// Gets the version identifier (e.g., "2025", "2024")
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets the full version string (e.g., "SolidWorks 2025 SP1")
        /// </summary>
        public string FullVersion { get; set; }

        /// <summary>
        /// Gets the main installation directory
        /// </summary>
        public string InstallationPath { get; set; }

        /// <summary>
        /// Gets the executable path
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Gets the license manager utility path
        /// </summary>
        public string LmutilPath { get; set; }

        /// <summary>
        /// Gets the registry key path where this version was found
        /// </summary>
        public string RegistryKeyPath { get; set; }

        /// <summary>
        /// Gets the installation date
        /// </summary>
        public DateTime? InstallationDate { get; set; }

        /// <summary>
        /// Gets the service pack level if available
        /// </summary>
        public string ServicePack { get; set; }

        /// <summary>
        /// Gets the build number if available
        /// </summary>
        public string BuildNumber { get; set; }

        /// <summary>
        /// Gets a value indicating whether this is a 64-bit installation
        /// </summary>
        public bool Is64Bit { get; set; }

        /// <summary>
        /// Gets the installation status
        /// </summary>
        public InstallationStatus Status { get; set; }

        /// <summary>
        /// Gets additional metadata about the installation
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// Gets the health status of this version
        /// </summary>
        public VersionHealth Health { get; set; }

        /// <summary>
        /// Gets the last time this version was detected
        /// </summary>
        public DateTime LastDetected { get; set; }

        /// <summary>
        /// Gets a value indicating whether this version is currently available for use
        /// </summary>
        public bool IsAvailable => Status == InstallationStatus.Installed &&
                                 Health != VersionHealth.Unavailable &&
                                 Health != VersionHealth.Corrupted;

        /// <summary>
        /// Gets a value indicating whether this version is supported by the service
        /// </summary>
        public bool IsSupported => IsInSupportedRange(Version);

        /// <summary>
        /// Initializes a new instance of the SolidWorksVersionInfo class
        /// </summary>
        public SolidWorksVersionInfo()
        {
            Status = InstallationStatus.Unknown;
            Health = VersionHealth.Unknown;
            Metadata = new Dictionary<string, string>();
            LastDetected = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the SolidWorksVersionInfo class with required parameters
        /// </summary>
        /// <param name="version">Version identifier</param>
        /// <param name="installationPath">Installation path</param>
        public SolidWorksVersionInfo(string version, string installationPath) : this()
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            InstallationPath = installationPath ?? throw new ArgumentNullException(nameof(installationPath));
            Status = InstallationStatus.Installed;
        }

        /// <summary>
        /// Updates the health status of this version
        /// </summary>
        /// <param name="newHealth">New health status</param>
        public void UpdateHealth(VersionHealth newHealth)
        {
            Health = newHealth;
            LastDetected = DateTime.UtcNow;
        }

        /// <summary>
        /// Validates that this version information is complete and valid
        /// </summary>
        /// <returns>Validation result with any errors</returns>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(Version))
            {
                result.Errors.Add("Version cannot be null or empty");
            }
            else if (!IsInSupportedRange(Version))
            {
                result.Warnings.Add($"Version {Version} is not in the supported range (2020-2025)");
            }

            if (string.IsNullOrWhiteSpace(InstallationPath))
            {
                result.Errors.Add("Installation path cannot be null or empty");
            }
            else if (!System.IO.Directory.Exists(InstallationPath))
            {
                result.Warnings.Add($"Installation path does not exist: {InstallationPath}");
            }

            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                result.Warnings.Add("Executable path is not specified");
            }
            else if (!System.IO.File.Exists(ExecutablePath))
            {
                result.Warnings.Add($"Executable file does not exist: {ExecutablePath}");
            }

            if (string.IsNullOrWhiteSpace(LmutilPath))
            {
                result.Warnings.Add("License manager utility path is not specified");
            }
            else if (!System.IO.File.Exists(LmutilPath))
            {
                result.Warnings.Add($"License manager utility does not exist: {LmutilPath}");
            }

            return result;
        }

        /// <summary>
        /// Gets a summary string representation of this version info
        /// </summary>
        /// <returns>Summary string</returns>
        public override string ToString()
        {
            return $"SolidWorks {Version} - {Status} - {Health}";
        }

        /// <summary>
        /// Gets a detailed string representation of this version info
        /// </summary>
        /// <returns>Detailed string</returns>
        public string ToDetailedString()
        {
            return $"SolidWorks {Version} ({FullVersion})\n" +
                   $"  Path: {InstallationPath}\n" +
                   $"  Executable: {ExecutablePath}\n" +
                   $"  License Manager: {LmutilPath}\n" +
                   $"  Status: {Status}\n" +
                   $"  Health: {Health}\n" +
                   $"  Installed: {InstallationDate?.ToString("yyyy-MM-dd") ?? "Unknown"}\n" +
                   $"  Service Pack: {ServicePack ?? "None"}\n" +
                   $"  Build: {BuildNumber ?? "Unknown"}\n" +
                   $"  Architecture: {(Is64Bit ? "64-bit" : "32-bit")}\n" +
                   $"  Available: {IsAvailable}\n" +
                   $"  Supported: {IsSupported}\n" +
                   $"  Last Detected: {LastDetected:yyyy-MM-dd HH:mm:ss UTC}";
        }

        /// <summary>
        /// Checks if a version is within the supported range
        /// </summary>
        /// <param name="version">Version to check</param>
        /// <returns>True if supported, false otherwise</returns>
        private static bool IsInSupportedRange(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            if (int.TryParse(version, out int year))
            {
                return year >= 2020 && year <= 2025;
            }

            return false;
        }

        /// <summary>
        /// Updates this instance with data from another SolidWorksVersionInfo
        /// </summary>
        /// <param name="other">The source version info to update from</param>
        public void UpdateFrom(SolidWorksVersionInfo other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            this.Version = other.Version;
            this.FullVersion = other.FullVersion;
            this.InstallationPath = other.InstallationPath;
            this.ExecutablePath = other.ExecutablePath;
            this.LmutilPath = other.LmutilPath;
            this.RegistryKeyPath = other.RegistryKeyPath;
            this.InstallationDate = other.InstallationDate;
            this.ServicePack = other.ServicePack;
            this.BuildNumber = other.BuildNumber;
            this.Is64Bit = other.Is64Bit;
            this.Status = other.Status;
            this.Health = other.Health;
            this.LastDetected = other.LastDetected;

            // Update metadata dictionary
            if (other.Metadata != null)
            {
                this.Metadata = new Dictionary<string, string>(other.Metadata);
            }
        }
    }

    /// <summary>
    /// Represents the installation status of a SolidWorks version
    /// </summary>
    public enum InstallationStatus
    {
        /// <summary>
        /// Installation status is unknown
        /// </summary>
        Unknown,

        /// <summary>
        /// Version is properly installed
        /// </summary>
        Installed,

        /// <summary>
        /// Installation is incomplete
        /// </summary>
        PartiallyInstalled,

        /// <summary>
        /// Installation is corrupted
        /// </summary>
        Corrupted,

        /// <summary>
        /// Version is not installed
        /// </summary>
        NotInstalled,

        /// <summary>
        /// Version is configured but not accessible
        /// </summary>
        Inaccessible,

        /// <summary>
        /// Version is being installed
        /// </summary>
        Installing
    }

    /// <summary>
    /// Represents the health status of a SolidWorks version
    /// </summary>
    public enum VersionHealth
    {
        /// <summary>
        /// Health status is unknown
        /// </summary>
        Unknown,

        /// <summary>
        /// Version is healthy and fully functional
        /// </summary>
        Healthy,

        /// <summary>
        /// Version has minor issues but is still functional
        /// </summary>
        Degraded,

        /// <summary>
        /// Version has performance issues
        /// </summary>
        PerformanceIssues,

        /// <summary>
        /// Version is corrupted or damaged
        /// </summary>
        Corrupted,

        /// <summary>
        /// Version is unavailable (e.g., network issues)
        /// </summary>
        Unavailable,

        /// <summary>
        /// Version license is invalid or expired
        /// </summary>
        LicenseIssues,

        /// <summary>
        /// Version configuration is invalid
        /// </summary>
        ConfigurationIssues
    }

    /// <summary>
    /// Represents the result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets the list of validation errors
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the list of validation warnings
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets a value indicating whether validation passed
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets a value indicating whether validation has any issues
        /// </summary>
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Initializes a new instance of the ValidationResult class
        /// </summary>
        public ValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        /// <summary>
        /// Gets a summary string of all validation issues
        /// </summary>
        /// <returns>Summary string</returns>
        public string GetSummary()
        {
            var summary = new System.Text.StringBuilder();

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
                if (Errors.Count > 0) summary.AppendLine();
                summary.AppendLine("Warnings:");
                foreach (var warning in Warnings)
                {
                    summary.AppendLine($"  - {warning}");
                }
            }

            return summary.ToString();
        }
    }
}