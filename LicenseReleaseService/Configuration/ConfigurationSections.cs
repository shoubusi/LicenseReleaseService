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
        /// Gets or sets the process execution configuration
        /// </summary>
        [ConfigurationProperty("processExecution")]
        public ProcessExecutionElement ProcessExecution
        {
            get { return (ProcessExecutionElement)this["processExecution"] ?? new ProcessExecutionElement(); }
            set { this["processExecution"] = value; }
        }

        /// <summary>
        /// Gets or sets the license query configuration
        /// </summary>
        [ConfigurationProperty("licenseQuery")]
        public LicenseQueryElement LicenseQuery
        {
            get { return (LicenseQueryElement)this["licenseQuery"] ?? new LicenseQueryElement(); }
            set { this["licenseQuery"] = value; }
        }

        /// <summary>
        /// Gets or sets the timer execution configuration
        /// </summary>
        [ConfigurationProperty("timer")]
        public TimerConfigurationElement Timer
        {
            get { return (TimerConfigurationElement)this["timer"] ?? new TimerConfigurationElement(); }
            set { this["timer"] = value; }
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

                    // Validate process execution section
                    errors.AddRange(ProcessExecution.Validate());

                    // Validate license query section
                    errors.AddRange(LicenseQuery.Validate());

                    // Validate timer execution section
                    errors.AddRange(Timer.Validate());
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
            sb.AppendLine($"  ProcessExecution: {ProcessExecution}");
            sb.AppendLine($"  LicenseQuery: {LicenseQuery}");
            sb.AppendLine($"  Timer: {Timer}");
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

    /// <summary>
    /// Configuration element for process execution settings
    /// </summary>
    public class ProcessExecutionElement : ConfigurationElement
    {
        [ConfigurationProperty("timeout", DefaultValue = 30)]
        [IntegerValidator(MinValue = 1, MaxValue = 300)]
        public int Timeout
        {
            get { return (int)this["timeout"]; }
            set { this["timeout"] = value; }
        }

        [ConfigurationProperty("maxRetries", DefaultValue = 3)]
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int MaxRetries
        {
            get { return (int)this["maxRetries"]; }
            set { this["maxRetries"] = value; }
        }

        [ConfigurationProperty("retryDelay", DefaultValue = 5)]
        [IntegerValidator(MinValue = 1, MaxValue = 60)]
        public int RetryDelay
        {
            get { return (int)this["retryDelay"]; }
            set { this["retryDelay"] = value; }
        }

        [ConfigurationProperty("commandTimeout", DefaultValue = 60)]
        [IntegerValidator(MinValue = 1, MaxValue = 600)]
        public int CommandTimeout
        {
            get { return (int)this["commandTimeout"]; }
            set { this["commandTimeout"] = value; }
        }

        [ConfigurationProperty("createNoWindow", DefaultValue = true)]
        public bool CreateNoWindow
        {
            get { return (bool)this["createNoWindow"]; }
            set { this["createNoWindow"] = value; }
        }

        [ConfigurationProperty("useShellExecute", DefaultValue = false)]
        public bool UseShellExecute
        {
            get { return (bool)this["useShellExecute"]; }
            set { this["useShellExecute"] = value; }
        }

        [ConfigurationProperty("redirectStandardInput", DefaultValue = false)]
        public bool RedirectStandardInput
        {
            get { return (bool)this["redirectStandardInput"]; }
            set { this["redirectStandardInput"] = value; }
        }

        [ConfigurationProperty("enableDetailedLogging", DefaultValue = true)]
        public bool EnableDetailedLogging
        {
            get { return (bool)this["enableDetailedLogging"]; }
            set { this["enableDetailedLogging"] = value; }
        }

        [ConfigurationProperty("killProcessTreeOnTimeout", DefaultValue = true)]
        public bool KillProcessTreeOnTimeout
        {
            get { return (bool)this["killProcessTreeOnTimeout"]; }
            set { this["killProcessTreeOnTimeout"] = value; }
        }

        [ConfigurationProperty("bufferSize", DefaultValue = 4096)]
        [IntegerValidator(MinValue = 1024, MaxValue = 65536)]
        public int BufferSize
        {
            get { return (int)this["bufferSize"]; }
            set { this["bufferSize"] = value; }
        }

        [ConfigurationProperty("maxOutputSize", DefaultValue = 0)]
        [LongValidator(MinValue = 0, MaxValue = 1073741824)]
        public long MaxOutputSize
        {
            get { return (long)this["maxOutputSize"]; }
            set { this["maxOutputSize"] = value; }
        }

        [ConfigurationProperty("throwOnNonZeroExitCode", DefaultValue = false)]
        public bool ThrowOnNonZeroExitCode
        {
            get { return (bool)this["throwOnNonZeroExitCode"]; }
            set { this["throwOnNonZeroExitCode"] = value; }
        }

        [ConfigurationProperty("workingDirectory", DefaultValue = "")]
        public string WorkingDirectory
        {
            get { return (string)this["workingDirectory"]; }
            set { this["workingDirectory"] = value; }
        }

        [ConfigurationProperty("enablePerformanceMonitoring", DefaultValue = true)]
        public bool EnablePerformanceMonitoring
        {
            get { return (bool)this["enablePerformanceMonitoring"]; }
            set { this["enablePerformanceMonitoring"] = value; }
        }

        [ConfigurationProperty("performanceMetricsInterval", DefaultValue = 30)]
        [IntegerValidator(MinValue = 5, MaxValue = 300)]
        public int PerformanceMetricsInterval
        {
            get { return (int)this["performanceMetricsInterval"]; }
            set { this["performanceMetricsInterval"] = value; }
        }

        [ConfigurationProperty("enableHealthMonitoring", DefaultValue = true)]
        public bool EnableHealthMonitoring
        {
            get { return (bool)this["enableHealthMonitoring"]; }
            set { this["enableHealthMonitoring"] = value; }
        }

        [ConfigurationProperty("healthCheckInterval", DefaultValue = 60)]
        [IntegerValidator(MinValue = 10, MaxValue = 600)]
        public int HealthCheckInterval
        {
            get { return (int)this["healthCheckInterval"]; }
            set { this["healthCheckInterval"] = value; }
        }

        [ConfigurationProperty("enableErrorRecovery", DefaultValue = true)]
        public bool EnableErrorRecovery
        {
            get { return (bool)this["enableErrorRecovery"]; }
            set { this["enableErrorRecovery"] = value; }
        }

        [ConfigurationProperty("errorRecoveryTimeout", DefaultValue = 120)]
        [IntegerValidator(MinValue = 10, MaxValue = 600)]
        public int ErrorRecoveryTimeout
        {
            get { return (int)this["errorRecoveryTimeout"]; }
            set { this["errorRecoveryTimeout"] = value; }
        }

        [ConfigurationProperty("enableAlerting", DefaultValue = true)]
        public bool EnableAlerting
        {
            get { return (bool)this["enableAlerting"]; }
            set { this["enableAlerting"] = value; }
        }

        [ConfigurationProperty("alertThreshold", DefaultValue = 90)]
        [IntegerValidator(MinValue = 50, MaxValue = 100)]
        public int AlertThreshold
        {
            get { return (int)this["alertThreshold"]; }
            set { this["alertThreshold"] = value; }
        }

        [ConfigurationProperty("encoding", DefaultValue = "UTF8")]
        [StringValidator(MinLength = 1)]
        public string Encoding
        {
            get { return (string)this["encoding"]; }
            set { this["encoding"] = value; }
        }

        /// <summary>
        /// Validates the process execution configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate timeout settings
                if (Timeout <= 0)
                {
                    errors.Add("Timeout must be greater than zero");
                }

                if (CommandTimeout <= 0)
                {
                    errors.Add("Command timeout must be greater than zero");
                }

                if (CommandTimeout < Timeout)
                {
                    errors.Add("Command timeout should be greater than or equal to timeout");
                }

                // Validate retry settings
                if (MaxRetries < 0)
                {
                    errors.Add("Max retries cannot be negative");
                }

                if (RetryDelay <= 0)
                {
                    errors.Add("Retry delay must be greater than zero");
                }

                // Validate buffer settings
                if (BufferSize <= 0)
                {
                    errors.Add("Buffer size must be greater than zero");
                }

                if (MaxOutputSize < 0)
                {
                    errors.Add("Max output size cannot be negative");
                }

                // Validate working directory if specified
                if (!string.IsNullOrWhiteSpace(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
                {
                    errors.Add($"Working directory does not exist: {WorkingDirectory}");
                }

                // Validate monitoring intervals
                if (PerformanceMetricsInterval <= 0)
                {
                    errors.Add("Performance metrics interval must be greater than zero");
                }

                if (HealthCheckInterval <= 0)
                {
                    errors.Add("Health check interval must be greater than zero");
                }

                if (ErrorRecoveryTimeout <= 0)
                {
                    errors.Add("Error recovery timeout must be greater than zero");
                }

                // Validate alerting threshold
                if (AlertThreshold < 50 || AlertThreshold > 100)
                {
                    errors.Add("Alert threshold must be between 50 and 100");
                }

                // Validate encoding
                if (!IsValidEncoding(Encoding))
                {
                    errors.Add($"Invalid encoding: {Encoding}. Valid values are: UTF8, ASCII, Unicode, UTF32, UTF7");
                }

                // Validate logical consistency
                if (EnablePerformanceMonitoring && PerformanceMetricsInterval > HealthCheckInterval)
                {
                    errors.Add("Performance metrics interval should be less than or equal to health check interval");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Process execution validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Validates if the encoding name is valid
        /// </summary>
        private bool IsValidEncoding(string encodingName)
        {
            if (string.IsNullOrWhiteSpace(encodingName))
                return false;

            var validEncodings = new[] { "UTF8", "ASCII", "Unicode", "UTF32", "UTF7" };
            return Array.Exists(validEncodings, enc => enc.Equals(encodingName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"ProcessExecution[Timeout={Timeout}s, MaxRetries={MaxRetries}, RetryDelay={RetryDelay}s, CommandTimeout={CommandTimeout}s, CreateNoWindow={CreateNoWindow}, EnableDetailedLogging={EnableDetailedLogging}]";
        }

        /// <summary>
        /// Converts the configuration to a ProcessExecutionOptions object
        /// </summary>
        /// <returns>ProcessExecutionOptions object</returns>
        public ProcessExecutionOptions ToProcessExecutionOptions()
        {
            var options = new ProcessExecutionOptions
            {
                Timeout = TimeSpan.FromSeconds(Timeout),
                MaxRetries = MaxRetries,
                RetryDelay = TimeSpan.FromSeconds(RetryDelay),
                CreateNoWindow = CreateNoWindow,
                UseShellExecute = UseShellExecute,
                RedirectStandardInput = RedirectStandardInput,
                EnableDetailedLogging = EnableDetailedLogging,
                KillProcessTreeOnTimeout = KillProcessTreeOnTimeout,
                BufferSize = BufferSize,
                MaxOutputSize = MaxOutputSize,
                ThrowOnNonZeroExitCode = ThrowOnNonZeroExitCode,
                WorkingDirectory = WorkingDirectory ?? string.Empty
            };

            // Set encoding
            switch (Encoding?.ToUpperInvariant())
            {
                case "ASCII":
                    options.Encoding = System.Text.Encoding.ASCII;
                    break;
                case "UNICODE":
                    options.Encoding = System.Text.Encoding.Unicode;
                    break;
                case "UTF32":
                    options.Encoding = System.Text.Encoding.UTF32;
                    break;
                case "UTF7":
                    options.Encoding = System.Text.Encoding.UTF7;
                    break;
                case "UTF8":
                default:
                    options.Encoding = System.Text.Encoding.UTF8;
                    break;
            }

            return options;
        }
    }

    /// <summary>
    /// Configuration element for license query settings
    /// </summary>
    public class LicenseQueryElement : ConfigurationElement
    {
        [ConfigurationProperty("cacheExpiration", DefaultValue = "00:05:00")]
        public TimeSpan CacheExpiration
        {
            get { return (TimeSpan)this["cacheExpiration"]; }
            set { this["cacheExpiration"] = value; }
        }

        [ConfigurationProperty("queryTimeout", DefaultValue = "00:00:30")]
        public TimeSpan QueryTimeout
        {
            get { return (TimeSpan)this["queryTimeout"]; }
            set { this["queryTimeout"] = value; }
        }

        [ConfigurationProperty("parsingTimeout", DefaultValue = "00:00:15")]
        public TimeSpan ParsingTimeout
        {
            get { return (TimeSpan)this["parsingTimeout"]; }
            set { this["parsingTimeout"] = value; }
        }

        [ConfigurationProperty("serverResponseTimeout", DefaultValue = "00:00:20")]
        public TimeSpan ServerResponseTimeout
        {
            get { return (TimeSpan)this["serverResponseTimeout"]; }
            set { this["serverResponseTimeout"] = value; }
        }

        [ConfigurationProperty("retryDelay", DefaultValue = "00:00:02")]
        public TimeSpan RetryDelay
        {
            get { return (TimeSpan)this["retryDelay"]; }
            set { this["retryDelay"] = value; }
        }

        [ConfigurationProperty("maxRetries", DefaultValue = 3)]
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        public int MaxRetries
        {
            get { return (int)this["maxRetries"]; }
            set { this["maxRetries"] = value; }
        }

        [ConfigurationProperty("maxCacheSize", DefaultValue = 1000)]
        [IntegerValidator(MinValue = 1, MaxValue = 100000)]
        public int MaxCacheSize
        {
            get { return (int)this["maxCacheSize"]; }
            set { this["maxCacheSize"] = value; }
        }

        [ConfigurationProperty("maxMemoryUsage", DefaultValue = 52428800)]
        [LongValidator(MinValue = 1048576, MaxValue = 1073741824)]
        public long MaxMemoryUsage
        {
            get { return (long)this["maxMemoryUsage"]; }
            set { this["maxMemoryUsage"] = value; }
        }

        [ConfigurationProperty("maxConcurrentQueries", DefaultValue = 10)]
        [IntegerValidator(MinValue = 1, MaxValue = 100)]
        public int MaxConcurrentQueries
        {
            get { return (int)this["maxConcurrentQueries"]; }
            set { this["maxConcurrentQueries"] = value; }
        }

        [ConfigurationProperty("maxOutputSize", DefaultValue = 10485760)]
        [LongValidator(MinValue = 0, MaxValue = 1073741824)]
        public long MaxOutputSize
        {
            get { return (long)this["maxOutputSize"]; }
            set { this["maxOutputSize"] = value; }
        }

        [ConfigurationProperty("statisticsRetentionDays", DefaultValue = 30)]
        [IntegerValidator(MinValue = 1, MaxValue = 365)]
        public int StatisticsRetentionDays
        {
            get { return (int)this["statisticsRetentionDays"]; }
            set { this["statisticsRetentionDays"] = value; }
        }

        [ConfigurationProperty("maxStatisticsEntries", DefaultValue = 10000)]
        [IntegerValidator(MinValue = 1, MaxValue = 100000)]
        public int MaxStatisticsEntries
        {
            get { return (int)this["maxStatisticsEntries"]; }
            set { this["maxStatisticsEntries"] = value; }
        }

        [ConfigurationProperty("enableCaching", DefaultValue = true)]
        public bool EnableCaching
        {
            get { return (bool)this["enableCaching"]; }
            set { this["enableCaching"] = value; }
        }

        [ConfigurationProperty("enableStatistics", DefaultValue = true)]
        public bool EnableStatistics
        {
            get { return (bool)this["enableStatistics"]; }
            set { this["enableStatistics"] = value; }
        }

        [ConfigurationProperty("enableVerboseOutput", DefaultValue = false)]
        public bool EnableVerboseOutput
        {
            get { return (bool)this["enableVerboseOutput"]; }
            set { this["enableVerboseOutput"] = value; }
        }

        [ConfigurationProperty("enableDetailedParsing", DefaultValue = true)]
        public bool EnableDetailedParsing
        {
            get { return (bool)this["enableDetailedParsing"]; }
            set { this["enableDetailedParsing"] = value; }
        }

        [ConfigurationProperty("enableErrorRecovery", DefaultValue = true)]
        public bool EnableErrorRecovery
        {
            get { return (bool)this["enableErrorRecovery"]; }
            set { this["enableErrorRecovery"] = value; }
        }

        [ConfigurationProperty("enableIncrementalUpdates", DefaultValue = true)]
        public bool EnableIncrementalUpdates
        {
            get { return (bool)this["enableIncrementalUpdates"]; }
            set { this["enableIncrementalUpdates"] = value; }
        }

        [ConfigurationProperty("enableFeatureBatching", DefaultValue = true)]
        public bool EnableFeatureBatching
        {
            get { return (bool)this["enableFeatureBatching"]; }
            set { this["enableFeatureBatching"] = value; }
        }

        [ConfigurationProperty("enableUserActivityTracking", DefaultValue = true)]
        public bool EnableUserActivityTracking
        {
            get { return (bool)this["enableUserActivityTracking"]; }
            set { this["enableUserActivityTracking"] = value; }
        }

        [ConfigurationProperty("enableBorrowingTracking", DefaultValue = true)]
        public bool EnableBorrowingTracking
        {
            get { return (bool)this["enableBorrowingTracking"]; }
            set { this["enableBorrowingTracking"] = value; }
        }

        [ConfigurationProperty("enableHealthMonitoring", DefaultValue = true)]
        public bool EnableHealthMonitoring
        {
            get { return (bool)this["enableHealthMonitoring"]; }
            set { this["enableHealthMonitoring"] = value; }
        }

        [ConfigurationProperty("enablePerformanceMetrics", DefaultValue = true)]
        public bool EnablePerformanceMetrics
        {
            get { return (bool)this["enablePerformanceMetrics"]; }
            set { this["enablePerformanceMetrics"] = value; }
        }

        [ConfigurationProperty("enableAlerting", DefaultValue = true)]
        public bool EnableAlerting
        {
            get { return (bool)this["enableAlerting"]; }
            set { this["enableAlerting"] = value; }
        }

        [ConfigurationProperty("failFastOnInvalidData", DefaultValue = false)]
        public bool FailFastOnInvalidData
        {
            get { return (bool)this["failFastOnInvalidData"]; }
            set { this["failFastOnInvalidData"] = value; }
        }

        [ConfigurationProperty("enableAutoCleanup", DefaultValue = true)]
        public bool EnableAutoCleanup
        {
            get { return (bool)this["enableAutoCleanup"]; }
            set { this["enableAutoCleanup"] = value; }
        }

        [ConfigurationProperty("enableCompactOutput", DefaultValue = false)]
        public bool EnableCompactOutput
        {
            get { return (bool)this["enableCompactOutput"]; }
            set { this["enableCompactOutput"] = value; }
        }

        [ConfigurationProperty("defaultOutputFormat", DefaultValue = "Standard")]
        [StringValidator(MinLength = 1)]
        public string DefaultOutputFormat
        {
            get { return (string)this["defaultOutputFormat"]; }
            set { this["defaultOutputFormat"] = value; }
        }

        [ConfigurationProperty("preferredLanguage", DefaultValue = "en-US")]
        [StringValidator(MinLength = 1)]
        public string PreferredLanguage
        {
            get { return (string)this["preferredLanguage"]; }
            set { this["preferredLanguage"] = value; }
        }

        [ConfigurationProperty("alertThreshold", DefaultValue = 90)]
        [IntegerValidator(MinValue = 0, MaxValue = 100)]
        public int AlertThreshold
        {
            get { return (int)this["alertThreshold"]; }
            set { this["alertThreshold"] = value; }
        }

        [ConfigurationProperty("cleanupInterval", DefaultValue = 3600)]
        [IntegerValidator(MinValue = 1, MaxValue = 86400)]
        public int CleanupInterval
        {
            get { return (int)this["cleanupInterval"]; }
            set { this["cleanupInterval"] = value; }
        }

        [ConfigurationProperty("healthCheckInterval", DefaultValue = 60)]
        [IntegerValidator(MinValue = 1, MaxValue = 3600)]
        public int HealthCheckInterval
        {
            get { return (int)this["healthCheckInterval"]; }
            set { this["healthCheckInterval"] = value; }
        }

        [ConfigurationProperty("performanceMetricsInterval", DefaultValue = 30)]
        [IntegerValidator(MinValue = 1, MaxValue = 3600)]
        public int PerformanceMetricsInterval
        {
            get { return (int)this["performanceMetricsInterval"]; }
            set { this["performanceMetricsInterval"] = value; }
        }

        /// <summary>
        /// Validates the license query configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            try
            {
                // Validate timeout relationships
                if (QueryTimeout < ParsingTimeout)
                {
                    errors.Add("Query timeout should be greater than or equal to parsing timeout");
                }

                if (ServerResponseTimeout > QueryTimeout)
                {
                    errors.Add("Server response timeout should be less than or equal to query timeout");
                }

                // Validate retry logic
                if (MaxRetries > 0 && RetryDelay.TotalSeconds == 0)
                {
                    errors.Add("Retry delay should be greater than zero when max retries is greater than zero");
                }

                // Validate memory limits
                if (MaxMemoryUsage > 1073741824) // 1GB
                {
                    errors.Add("Max memory usage exceeds recommended limit of 1GB");
                }

                // Validate concurrent queries
                if (MaxConcurrentQueries > 100)
                {
                    errors.Add("Max concurrent queries exceeds recommended limit of 100");
                }

                // Validate output size
                if (MaxOutputSize > 104857600) // 100MB
                {
                    errors.Add("Max output size exceeds recommended limit of 100MB");
                }

                // Validate statistics settings
                if (StatisticsRetentionDays > 365)
                {
                    errors.Add("Statistics retention days exceeds recommended limit of 365");
                }

                if (MaxStatisticsEntries > 100000)
                {
                    errors.Add("Max statistics entries exceeds recommended limit of 100000");
                }

                // Validate interval relationships
                if (PerformanceMetricsInterval > HealthCheckInterval)
                {
                    errors.Add("Performance metrics interval should be less than or equal to health check interval");
                }

                if (CleanupInterval < HealthCheckInterval)
                {
                    errors.Add("Cleanup interval should be greater than or equal to health check interval");
                }

                // Validate alert threshold
                if (AlertThreshold < 50 && EnableAlerting)
                {
                    errors.Add("Alert threshold less than 50% may generate excessive notifications");
                }

                // Validate output format
                var validFormats = new[] { "Standard", "Verbose", "Compact", "JSON", "XML" };
                if (!Array.Exists(validFormats, format => format.Equals(DefaultOutputFormat, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Invalid default output format: {DefaultOutputFormat}. Valid formats are: {string.Join(", ", validFormats)}");
                }

                // Validate language format
                if (!System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                    .Any(c => c.Name.Equals(PreferredLanguage, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Invalid preferred language: {PreferredLanguage}");
                }

                // Validate cache settings
                if (EnableCaching && MaxCacheSize <= 0)
                {
                    errors.Add("Max cache size must be greater than zero when caching is enabled");
                }

                if (EnableCaching && CacheExpiration.TotalSeconds <= 0)
                {
                    errors.Add("Cache expiration must be greater than zero when caching is enabled");
                }

                // Validate statistics settings
                if (EnableStatistics && StatisticsRetentionDays <= 0)
                {
                    errors.Add("Statistics retention days must be greater than zero when statistics are enabled");
                }

                if (EnableStatistics && MaxStatisticsEntries <= 0)
                {
                    errors.Add("Max statistics entries must be greater than zero when statistics are enabled");
                }

                // Validate monitoring settings
                if (EnableHealthMonitoring && HealthCheckInterval <= 0)
                {
                    errors.Add("Health check interval must be greater than zero when health monitoring is enabled");
                }

                if (EnablePerformanceMetrics && PerformanceMetricsInterval <= 0)
                {
                    errors.Add("Performance metrics interval must be greater than zero when performance metrics are enabled");
                }

                if (EnableAutoCleanup && CleanupInterval <= 0)
                {
                    errors.Add("Cleanup interval must be greater than zero when auto cleanup is enabled");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"License query validation error: {ex.Message}");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the configuration
        /// </summary>
        public override string ToString()
        {
            return $"LicenseQuery[CacheExp={CacheExpiration.TotalMinutes:F1}m, Timeout={QueryTimeout.TotalSeconds:F1}s, MaxRetries={MaxRetries}, Caching={EnableCaching}, DetailedParsing={EnableDetailedParsing}]";
        }

        /// <summary>
        /// Converts the configuration to a LicenseQueryOptions object
        /// </summary>
        /// <returns>LicenseQueryOptions object</returns>
        public LicenseQueryOptions ToLicenseQueryOptions()
        {
            var options = new LicenseQueryOptions
            {
                CacheExpiration = CacheExpiration,
                QueryTimeout = QueryTimeout,
                ParsingTimeout = ParsingTimeout,
                ServerResponseTimeout = ServerResponseTimeout,
                RetryDelay = RetryDelay,
                MaxRetries = MaxRetries,
                MaxCacheSize = MaxCacheSize,
                MaxMemoryUsage = (int)MaxMemoryUsage,
                MaxConcurrentQueries = MaxConcurrentQueries,
                MaxOutputSize = (int)MaxOutputSize,
                StatisticsRetentionDays = StatisticsRetentionDays,
                MaxStatisticsEntries = MaxStatisticsEntries,
                EnableCaching = EnableCaching,
                EnableStatistics = EnableStatistics,
                EnableVerboseOutput = EnableVerboseOutput,
                EnableDetailedParsing = EnableDetailedParsing,
                EnableErrorRecovery = EnableErrorRecovery,
                EnableIncrementalUpdates = EnableIncrementalUpdates,
                EnableFeatureBatching = EnableFeatureBatching,
                EnableUserActivityTracking = EnableUserActivityTracking,
                EnableBorrowingTracking = EnableBorrowingTracking,
                EnableHealthMonitoring = EnableHealthMonitoring,
                EnablePerformanceMetrics = EnablePerformanceMetrics,
                EnableAlerting = EnableAlerting,
                FailFastOnInvalidData = FailFastOnInvalidData,
                EnableAutoCleanup = EnableAutoCleanup,
                EnableCompactOutput = EnableCompactOutput,
                DefaultOutputFormat = DefaultOutputFormat,
                PreferredLanguage = PreferredLanguage,
                AlertThreshold = AlertThreshold,
                CleanupInterval = CleanupInterval,
                HealthCheckInterval = HealthCheckInterval,
                PerformanceMetricsInterval = PerformanceMetricsInterval
            };

            return options;
        }
    }
}