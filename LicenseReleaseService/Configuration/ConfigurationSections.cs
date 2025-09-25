using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LicenseReleaseService.Configuration
{
    /// <summary>
    /// Main configuration section for the license release service
    /// </summary>
    public class LicenseReleaseServiceSection : ConfigurationSection
    {
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets or sets the license manager configuration
        /// </summary>
        [ConfigurationProperty("licenseManager", IsRequired = true)]
        public LicenseManagerElement LicenseManager
        {
            get { return (LicenseManagerElement)this["licenseManager"]; }
            set { this["licenseManager"] = value; }
        }

        /// <summary>
        /// Gets or sets the logging configuration
        /// </summary>
        [ConfigurationProperty("logging", IsRequired = true)]
        public LoggingElement Logging
        {
            get { return (LoggingElement)this["logging"]; }
            set { this["logging"] = value; }
        }

        /// <summary>
        /// Gets or sets the monitoring configuration
        /// </summary>
        [ConfigurationProperty("monitoring", IsRequired = true)]
        public MonitoringElement Monitoring
        {
            get { return (MonitoringElement)this["monitoring"]; }
            set { this["monitoring"] = value; }
        }

        /// <summary>
        /// Gets or sets the security configuration
        /// </summary>
        [ConfigurationProperty("security")]
        public SecurityElement Security
        {
            get { return (SecurityElement)this["security"] ?? new SecurityElement(); }
            set { this["security"] = value; }
        }

        /// <summary>
        /// Validates the configuration section
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            lock (_lock)
            {
                try
                {
                    // Validate license manager section
                    errors.AddRange(LicenseManager.Validate());

                    // Validate logging section
                    errors.AddRange(Logging.Validate());

                    // Validate monitoring section
                    errors.AddRange(Monitoring.Validate());

                    // Validate security section
                    errors.AddRange(Security.Validate());
                }
                catch (Exception ex)
                {
                    errors.Add($"Configuration validation error: {ex.Message}");
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        /// <returns>Configuration summary</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("LicenseReleaseService Configuration:");
            sb.AppendLine($"  LicenseManager: {LicenseManager}");
            sb.AppendLine($"  Logging: {Logging}");
            sb.AppendLine($"  Monitoring: {Monitoring}");
            sb.AppendLine($"  Security: {Security}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Configuration element for license manager settings
    /// </summary>
    public class LicenseManagerElement : ConfigurationElement
    {
        [ConfigurationProperty("lmutilPath", IsRequired = true)]
        [StringValidator(MinLength = 1)]
        public string LmutilPath
        {
            get { return (string)this["lmutilPath"]; }
            set { this["lmutilPath"] = value; }
        }

        [ConfigurationProperty("licenseServer", IsRequired = true)]
        [StringValidator(MinLength = 1)]
        public string LicenseServer
        {
            get { return (string)this["licenseServer"]; }
            set { this["licenseServer"] = value; }
        }

        [ConfigurationProperty("port", DefaultValue = 27000)]
        [IntegerValidator(MinValue = 1, MaxValue = 65535)]
        public int Port
        {
            get { return (int)this["port"]; }
            set { this["port"] = value; }
        }

        [ConfigurationProperty("timeout", DefaultValue = 30)]
        [IntegerValidator(MinValue = 1, MaxValue = 300)]
        public int Timeout
        {
            get { return (int)this["timeout"]; }
            set { this["timeout"] = value; }
        }

        [ConfigurationProperty("retryCount", DefaultValue = 3)]
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int RetryCount
        {
            get { return (int)this["retryCount"]; }
            set { this["retryCount"] = value; }
        }

        [ConfigurationProperty("commandTimeout", DefaultValue = 60)]
        [IntegerValidator(MinValue = 1, MaxValue = 600)]
        public int CommandTimeout
        {
            get { return (int)this["commandTimeout"]; }
            set { this["commandTimeout"] = value; }
        }

        [ConfigurationProperty("maxConcurrentLicenses", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int MaxConcurrentLicenses
        {
            get { return (int)this["maxConcurrentLicenses"]; }
            set { this["maxConcurrentLicenses"] = value; }
        }

        [ConfigurationProperty("releaseDelay", DefaultValue = 1000)]
        [IntegerValidator(MinValue = 0, MaxValue = 10000)]
        public int ReleaseDelay
        {
            get { return (int)this["releaseDelay"]; }
            set { this["releaseDelay"] = value; }
        }

        [ConfigurationProperty("validatePath", DefaultValue = true)]
        public bool ValidatePath
        {
            get { return (bool)this["validatePath"]; }
            set { this["validatePath"] = value; }
        }

        [ConfigurationProperty("enableHealthCheck", DefaultValue = true)]
        public bool EnableHealthCheck
        {
            get { return (bool)this["enableHealthCheck"]; }
            set { this["enableHealthCheck"] = value; }
        }

        [ConfigurationProperty("healthCheckInterval", DefaultValue = 300)]
        [IntegerValidator(MinValue = 30, MaxValue = 3600)]
        public int HealthCheckInterval
        {
            get { return (int)this["healthCheckInterval"]; }
            set { this["healthCheckInterval"] = value; }
        }

        /// <summary>
        /// Validates the license manager configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate lmutil.exe exists if path validation is enabled
                if (ValidatePath && !File.Exists(LmutilPath))
                {
                    errors.Add($"lmutil.exe not found at: {LmutilPath}");
                }

                // Validate license server address format
                if (!IsValidServerAddress(LicenseServer))
                {
                    errors.Add($"Invalid license server address: {LicenseServer}");
                }

                // Validate port connectivity (optional validation)
                if (EnableHealthCheck && !IsPortAvailable(LicenseServer, Port))
                {
                    errors.Add($"Cannot connect to license server {LicenseServer}:{Port}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"LicenseManager validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Validates if the server address format is valid
        /// </summary>
        private bool IsValidServerAddress(string serverAddress)
        {
            if (string.IsNullOrWhiteSpace(serverAddress))
                return false;

            try
            {
                // Check if it's a valid hostname or IP address
                return IPAddress.TryParse(serverAddress, out _) ||
                       Uri.CheckHostName(serverAddress) != UriHostNameType.Unknown;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the port is available/connectable
        /// </summary>
        private bool IsPortAvailable(string host, int port)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var result = tcpClient.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                    if (success)
                    {
                        tcpClient.EndConnect(result);
                        tcpClient.Close();
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"LicenseManager[Server={LicenseServer}:{Port}, Timeout={Timeout}s, Retries={RetryCount}, MaxConcurrent={MaxConcurrentLicenses}]";
        }
    }

    /// <summary>
    /// Configuration element for logging settings
    /// </summary>
    public class LoggingElement : ConfigurationElement
    {
        [ConfigurationProperty("logLevel", DefaultValue = "Information")]
        [StringValidator(MinLength = 1)]
        public string LogLevel
        {
            get { return (string)this["logLevel"]; }
            set { this["logLevel"] = value; }
        }

        [ConfigurationProperty("logFilePath", DefaultValue = "C:\\Logs\\LicenseReleaseService")]
        [StringValidator(MinLength = 1)]
        public string LogFilePath
        {
            get { return (string)this["logFilePath"]; }
            set { this["logFilePath"] = value; }
        }

        [ConfigurationProperty("maxFileSize", DefaultValue = 10485760)]
        [IntegerValidator(MinValue = 1024, MaxValue = 1073741824)]
        public long MaxFileSize
        {
            get { return (long)this["maxFileSize"]; }
            set { this["maxFileSize"] = value; }
        }

        [ConfigurationProperty("maxFiles", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int MaxFiles
        {
            get { return (int)this["maxFiles"]; }
            set { this["maxFiles"] = value; }
        }

        [ConfigurationProperty("enableFileLogging", DefaultValue = true)]
        public bool EnableFileLogging
        {
            get { return (bool)this["enableFileLogging"]; }
            set { this["enableFileLogging"] = value; }
        }

        [ConfigurationProperty("enableEventLog", DefaultValue = true)]
        public bool EnableEventLog
        {
            get { return (bool)this["enableEventLog"]; }
            set { this["enableEventLog"] = value; }
        }

        [ConfigurationProperty("enableConsoleLogging", DefaultValue = false)]
        public bool EnableConsoleLogging
        {
            get { return (bool)this["enableConsoleLogging"]; }
            set { this["enableConsoleLogging"] = value; }
        }

        [ConfigurationProperty("logRetentionDays", DefaultValue = 30)]
        [IntegerValidator(MinValue = 1, MaxValue = 365)]
        public int LogRetentionDays
        {
            get { return (int)this["logRetentionDays"]; }
            set { this["logRetentionDays"] = value; }
        }

        [ConfigurationProperty("includeTimestamp", DefaultValue = true)]
        public bool IncludeTimestamp
        {
            get { return (bool)this["includeTimestamp"]; }
            set { this["includeTimestamp"] = value; }
        }

        [ConfigurationProperty("includeThreadId", DefaultValue = true)]
        public bool IncludeThreadId
        {
            get { return (bool)this["includeThreadId"]; }
            set { this["includeThreadId"] = value; }
        }

        [ConfigurationProperty("logTemplate", DefaultValue = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")]
        [StringValidator(MinLength = 1)]
        public string LogTemplate
        {
            get { return (string)this["logTemplate"]; }
            set { this["logTemplate"] = value; }
        }

        [ConfigurationProperty("errorLogTemplate", DefaultValue = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}")]
        [StringValidator(MinLength = 1)]
        public string ErrorLogTemplate
        {
            get { return (string)this["errorLogTemplate"]; }
            set { this["errorLogTemplate"] = value; }
        }

        /// <summary>
        /// Validates the logging configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate log level
                var validLevels = new[] { "Debug", "Information", "Warning", "Error", "Critical", "None" };
                if (!Array.Exists(validLevels, level => level.Equals(LogLevel, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Invalid log level: {LogLevel}. Valid values are: {string.Join(", ", validLevels)}");
                }

                // Validate log directory access
                if (EnableFileLogging && !EnsureLogDirectory(LogFilePath))
                {
                    errors.Add($"Cannot access or create log directory: {LogFilePath}");
                }

                // Validate log template contains required placeholders
                if (!LogTemplate.Contains("{Message}"))
                {
                    errors.Add("Log template must contain {Message} placeholder");
                }

                if (!ErrorLogTemplate.Contains("{Message}"))
                {
                    errors.Add("Error log template must contain {Message} placeholder");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Logging validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Ensures the log directory exists or can be created
        /// </summary>
        private bool EnsureLogDirectory(string logPath)
        {
            try
            {
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                // Test write access
                var testFile = Path.Combine(logPath, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"Logging[Level={LogLevel}, Path={LogFilePath}, FileLogging={EnableFileLogging}, EventLog={EnableEventLog}]";
        }
    }

    /// <summary>
    /// Configuration element for monitoring settings
    /// </summary>
    public class MonitoringElement : ConfigurationElement
    {
        [ConfigurationProperty("healthCheckInterval", DefaultValue = 60)]
        [IntegerValidator(MinValue = 10, MaxValue = 3600)]
        public int HealthCheckInterval
        {
            get { return (int)this["healthCheckInterval"]; }
            set { this["healthCheckInterval"] = value; }
        }

        [ConfigurationProperty("performanceCountersEnabled", DefaultValue = true)]
        public bool PerformanceCountersEnabled
        {
            get { return (bool)this["performanceCountersEnabled"]; }
            set { this["performanceCountersEnabled"] = value; }
        }

        [ConfigurationProperty("enableMetrics", DefaultValue = true)]
        public bool EnableMetrics
        {
            get { return (bool)this["enableMetrics"]; }
            set { this["enableMetrics"] = value; }
        }

        [ConfigurationProperty("metricsInterval", DefaultValue = 30)]
        [IntegerValidator(MinValue = 5, MaxValue = 300)]
        public int MetricsInterval
        {
            get { return (int)this["metricsInterval"]; }
            set { this["metricsInterval"] = value; }
        }

        [ConfigurationProperty("alertThreshold", DefaultValue = 90)]
        [IntegerValidator(MinValue = 50, MaxValue = 100)]
        public int AlertThreshold
        {
            get { return (int)this["alertThreshold"]; }
            set { this["alertThreshold"] = value; }
        }

        [ConfigurationProperty("enableHeartbeat", DefaultValue = true)]
        public bool EnableHeartbeat
        {
            get { return (bool)this["enableHeartbeat"]; }
            set { this["enableHeartbeat"] = value; }
        }

        [ConfigurationProperty("heartbeatInterval", DefaultValue = 120)]
        [IntegerValidator(MinValue = 30, MaxValue = 600)]
        public int HeartbeatInterval
        {
            get { return (int)this["heartbeatInterval"]; }
            set { this["heartbeatInterval"] = value; }
        }

        [ConfigurationProperty("enableStatistics", DefaultValue = true)]
        public bool EnableStatistics
        {
            get { return (bool)this["enableStatistics"]; }
            set { this["enableStatistics"] = value; }
        }

        [ConfigurationProperty("statisticsInterval", DefaultValue = 300)]
        [IntegerValidator(MinValue = 60, MaxValue = 3600)]
        public int StatisticsInterval
        {
            get { return (int)this["statisticsInterval"]; }
            set { this["statisticsInterval"] = value; }
        }

        [ConfigurationProperty("maxStatisticsHistory", DefaultValue = 1000)]
        [IntegerValidator(MinValue = 100, MaxValue = 10000)]
        public int MaxStatisticsHistory
        {
            get { return (int)this["maxStatisticsHistory"]; }
            set { this["maxStatisticsHistory"] = value; }
        }

        [ConfigurationProperty("enableDiagnostics", DefaultValue = true)]
        public bool EnableDiagnostics
        {
            get { return (bool)this["enableDiagnostics"]; }
            set { this["enableDiagnostics"] = value; }
        }

        [ConfigurationProperty("memoryLimit", DefaultValue = 1073741824)]
        [LongValidator(MinValue = 1048576, MaxValue = 8589934592)]
        public long MemoryLimit
        {
            get { return (long)this["memoryLimit"]; }
            set { this["memoryLimit"] = value; }
        }

        [ConfigurationProperty("cpuLimit", DefaultValue = 80)]
        [IntegerValidator(MinValue = 50, MaxValue = 100)]
        public int CpuLimit
        {
            get { return (int)this["cpuLimit"]; }
            set { this["cpuLimit"] = value; }
        }

        /// <summary>
        /// Validates the monitoring configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate intervals are reasonable
                if (HealthCheckInterval < 10)
                {
                    errors.Add("Health check interval must be at least 10 seconds");
                }

                if (MetricsInterval > HealthCheckInterval)
                {
                    errors.Add("Metrics interval should be less than or equal to health check interval");
                }

                // Validate thresholds
                if (AlertThreshold < 50 || AlertThreshold > 100)
                {
                    errors.Add("Alert threshold must be between 50 and 100");
                }

                // Validate resource limits
                if (MemoryLimit < 1048576) // 1MB
                {
                    errors.Add("Memory limit must be at least 1MB");
                }

                if (CpuLimit < 50 || CpuLimit > 100)
                {
                    errors.Add("CPU limit must be between 50 and 100");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Monitoring validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"Monitoring[HealthCheck={HealthCheckInterval}s, Counters={PerformanceCountersEnabled}, Metrics={EnableMetrics}]";
        }
    }

    /// <summary>
    /// Configuration element for security settings
    /// </summary>
    public class SecurityElement : ConfigurationElement
    {
        [ConfigurationProperty("enableAuthentication", DefaultValue = false)]
        public bool EnableAuthentication
        {
            get { return (bool)this["enableAuthentication"]; }
            set { this["enableAuthentication"] = value; }
        }

        [ConfigurationProperty("enableAuthorization", DefaultValue = false)]
        public bool EnableAuthorization
        {
            get { return (bool)this["enableAuthorization"]; }
            set { this["enableAuthorization"] = value; }
        }

        [ConfigurationProperty("encryptionKey", DefaultValue = "")]
        public string EncryptionKey
        {
            get { return (string)this["encryptionKey"]; }
            set { this["encryptionKey"] = value; }
        }

        [ConfigurationProperty("enableSecureCommunication", DefaultValue = false)]
        public bool EnableSecureCommunication
        {
            get { return (bool)this["enableSecureCommunication"]; }
            set { this["enableSecureCommunication"] = value; }
        }

        [ConfigurationProperty("certificatePath", DefaultValue = "")]
        public string CertificatePath
        {
            get { return (string)this["certificatePath"]; }
            set { this["certificatePath"] = value; }
        }

        [ConfigurationProperty("certificatePassword", DefaultValue = "")]
        public string CertificatePassword
        {
            get { return (string)this["certificatePassword"]; }
            set { this["certificatePassword"] = value; }
        }

        [ConfigurationProperty("allowedHosts", DefaultValue = "*")]
        public string AllowedHosts
        {
            get { return (string)this["allowedHosts"]; }
            set { this["allowedHosts"] = value; }
        }

        [ConfigurationProperty("enableIpWhitelist", DefaultValue = false)]
        public bool EnableIpWhitelist
        {
            get { return (bool)this["enableIpWhitelist"]; }
            set { this["enableIpWhitelist"] = value; }
        }

        [ConfigurationProperty("allowedIps", DefaultValue = "")]
        public string AllowedIps
        {
            get { return (string)this["allowedIps"]; }
            set { this["allowedIps"] = value; }
        }

        [ConfigurationProperty("enableAuditLogging", DefaultValue = true)]
        public bool EnableAuditLogging
        {
            get { return (bool)this["enableAuditLogging"]; }
            set { this["enableAuditLogging"] = value; }
        }

        [ConfigurationProperty("auditLogPath", DefaultValue = "C:\\Logs\\LicenseReleaseService\\Audit")]
        public string AuditLogPath
        {
            get { return (string)this["auditLogPath"]; }
            set { this["auditLogPath"] = value; }
        }

        [ConfigurationProperty("sessionTimeout", DefaultValue = 1800)]
        [IntegerValidator(MinValue = 60, MaxValue = 86400)]
        public int SessionTimeout
        {
            get { return (int)this["sessionTimeout"]; }
            set { this["sessionTimeout"] = value; }
        }

        [ConfigurationProperty("maxFailedAttempts", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 20)]
        public int MaxFailedAttempts
        {
            get { return (int)this["maxFailedAttempts"]; }
            set { this["maxFailedAttempts"] = value; }
        }

        [ConfigurationProperty("lockoutDuration", DefaultValue = 900)]
        [IntegerValidator(MinValue = 60, MaxValue = 3600)]
        public int LockoutDuration
        {
            get { return (int)this["lockoutDuration"]; }
            set { this["lockoutDuration"] = value; }
        }

        /// <summary>
        /// Validates the security configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate certificate configuration
                if (EnableSecureCommunication)
                {
                    if (string.IsNullOrEmpty(CertificatePath) || !File.Exists(CertificatePath))
                    {
                        errors.Add($"Certificate file not found: {CertificatePath}");
                    }
                }

                // Validate encryption key length if provided
                if (!string.IsNullOrEmpty(EncryptionKey) && EncryptionKey.Length < 16)
                {
                    errors.Add("Encryption key must be at least 16 characters long");
                }

                // Validate audit log directory
                if (EnableAuditLogging && !string.IsNullOrEmpty(AuditLogPath))
                {
                    if (!Directory.Exists(AuditLogPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(AuditLogPath);
                        }
                        catch
                        {
                            errors.Add($"Cannot create audit log directory: {AuditLogPath}");
                        }
                    }
                }

                // Validate IP whitelist format if enabled
                if (EnableIpWhitelist && !string.IsNullOrEmpty(AllowedIps))
                {
                    var ips = AllowedIps.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var ip in ips)
                    {
                        var trimmedIp = ip.Trim();
                        if (!IsValidIpAddress(trimmedIp) && !IsValidIpRange(trimmedIp))
                        {
                            errors.Add($"Invalid IP address or range: {trimmedIp}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Security validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Validates if the string is a valid IP address
        /// </summary>
        private bool IsValidIpAddress(string ipAddress)
        {
            return IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// Validates if the string is a valid IP range (e.g., 192.168.1.0/24)
        /// </summary>
        private bool IsValidIpRange(string ipRange)
        {
            if (string.IsNullOrEmpty(ipRange))
                return false;

            var parts = ipRange.Split('/');
            if (parts.Length != 2)
                return false;

            return IPAddress.TryParse(parts[0], out _) &&
                   int.TryParse(parts[1], out int prefixLength) &&
                   prefixLength >= 0 && prefixLength <= 32;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"Security[Auth={EnableAuthentication}, Secure={EnableSecureCommunication}, Audit={EnableAuditLogging}]";
        }
    }
}