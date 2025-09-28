using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using System.Diagnostics;
using LicenseReleaseService.Configuration;
using System.Collections.Generic;

namespace LicenseReleaseService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Initialize ServiceProcessInstaller
            serviceProcessInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem,
                Password = null,
                Username = null
            };

            // Initialize ServiceInstaller
            serviceInstaller = new ServiceInstaller
            {
                ServiceName = "LicenseReleaseService",
                DisplayName = "License Release Service",
                Description = "Manages software license releases and monitoring for enterprise environments",
                StartType = ServiceStartMode.Automatic,
                ServicesDependedOn = new string[] { "EventLog" }
            };

            // Add installers to the collection
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);

            // Set installer properties
            this.AfterInstall += new InstallEventHandler(ProjectInstaller_AfterInstall);
            this.AfterRollback += new InstallEventHandler(ProjectInstaller_AfterRollback);
            this.AfterUninstall += new InstallEventHandler(ProjectInstaller_AfterUninstall);
            this.BeforeInstall += new InstallEventHandler(ProjectInstaller_BeforeInstall);
        }

        private void ProjectInstaller_BeforeInstall(object sender, InstallEventArgs e)
        {
            try
            {
                // Validate configuration before installation
                ValidateConfigurationBeforeInstall();
                LogEvent("Configuration validation passed for installation", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                LogEvent($"Configuration validation failed for installation: {ex.Message}", EventLogEntryType.Error);
                throw new InvalidOperationException($"Cannot install service due to configuration errors: {ex.Message}", ex);
            }
        }

        private void ProjectInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            try
            {
                // Configure service recovery options after installation
                ConfigureServiceRecovery();

                // Create initial configuration backup
                CreateConfigurationBackup();

                // Configure advanced features if enabled
                ConfigureAdvancedFeatures();

                // Log successful installation
                LogEvent("License Release Service installed successfully", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                LogEvent($"Failed to configure service after installation: {ex.Message}", EventLogEntryType.Error);
                throw;
            }
        }

        private void ProjectInstaller_AfterRollback(object sender, InstallEventArgs e)
        {
            try
            {
                LogEvent("License Release Service installation was rolled back", EventLogEntryType.Warning);
            }
            catch (Exception)
            {
                // Ignore logging errors during rollback
            }
        }

        private void ProjectInstaller_AfterUninstall(object sender, InstallEventArgs e)
        {
            try
            {
                LogEvent("License Release Service uninstalled successfully", EventLogEntryType.Information);
            }
            catch (Exception)
            {
                // Ignore logging errors during uninstall
            }
        }

        private void ValidateConfigurationBeforeInstall()
        {
            try
            {
                var configManager = ConfigurationManager.Instance;

                // Validate configuration
                var validationErrors = configManager.ValidateConfiguration();
                if (validationErrors.Count > 0)
                {
                    var errorString = string.Join("; ", validationErrors);
                    throw new InvalidOperationException($"Configuration validation failed: {errorString}");
                }

                // Check configuration health
                if (configManager.AdvancedFeaturesEnabled && configManager.HealthStatus == ConfigurationHealthStatus.Error)
                {
                    throw new InvalidOperationException("Configuration health check failed - cannot install with configuration errors");
                }

                // Verify configuration file exists and is accessible
                var configPath = configManager.ConfigFilePath;
                if (string.IsNullOrEmpty(configPath) || !System.IO.File.Exists(configPath))
                {
                    throw new InvalidOperationException("Configuration file is not accessible");
                }

                // Test file access
                using (var stream = System.IO.File.OpenRead(configPath))
                {
                    // Just test access
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Configuration validation failed: {ex.Message}", ex);
            }
        }

        private void CreateConfigurationBackup()
        {
            try
            {
                var configManager = ConfigurationManager.Instance;
                if (configManager.AdvancedFeaturesEnabled)
                {
                    var backupPath = configManager.CreateBackup("initial_install");
                    LogEvent($"Initial configuration backup created at: {backupPath}", EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                LogEvent($"Failed to create initial configuration backup: {ex.Message}", EventLogEntryType.Warning);
            }
        }

        private void ConfigureAdvancedFeatures()
        {
            try
            {
                var configManager = ConfigurationManager.Instance;
                if (configManager.AdvancedFeaturesEnabled)
                {
                    // Enable advanced features if configured
                    LogEvent("Advanced features enabled for service", EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                LogEvent($"Failed to configure advanced features: {ex.Message}", EventLogEntryType.Warning);
            }
        }

        private void ConfigureServiceRecovery()
        {
            try
            {
                // This would typically use WMI or P/Invoke to configure recovery options
                // For now, we'll log that recovery configuration would happen here
                LogEvent("Service recovery options would be configured here", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                LogEvent($"Failed to configure service recovery: {ex.Message}", EventLogEntryType.Warning);
            }
        }

        private void LogEvent(string message, EventLogEntryType entryType)
        {
            try
            {
                if (!EventLog.SourceExists("LicenseReleaseService"))
                {
                    EventLog.CreateEventSource("LicenseReleaseService", "Application");
                }
                EventLog.WriteEntry("LicenseReleaseService", message, entryType);
            }
            catch (Exception)
            {
                // Fail silently if event logging is not available
                // This prevents installer failures due to event log issues
            }
        }

        public override void Commit(IDictionary savedState)
        {
            base.Commit(savedState);
            LogEvent("License Release Service installation committed successfully", EventLogEntryType.Information);
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);
            LogEvent("License Release Service installation in progress", EventLogEntryType.Information);
        }

        public override void Uninstall(IDictionary savedState)
        {
            base.Uninstall(savedState);
            LogEvent("License Release Service uninstallation in progress", EventLogEntryType.Information);
        }

        public override void Rollback(IDictionary savedState)
        {
            base.Rollback(savedState);
            LogEvent("License Release Service installation rollback in progress", EventLogEntryType.Warning);
        }
    }
}