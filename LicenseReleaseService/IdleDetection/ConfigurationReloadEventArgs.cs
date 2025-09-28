using System;

namespace LicenseReleaseService.IdleDetection
{
    /// <summary>
    /// Event arguments for configuration reload events
    /// </summary>
    public class ConfigurationReloadEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the timestamp when the configuration was reloaded
        /// </summary>
        public DateTime ReloadTimestamp { get; set; }

        /// <summary>
        /// Gets a value indicating whether the reload was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets the error message if the reload failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets the path to the configuration file that was reloaded
        /// </summary>
        public string ConfigurationPath { get; set; }

        /// <summary>
        /// Initializes a new instance of the ConfigurationReloadEventArgs class
        /// </summary>
        public ConfigurationReloadEventArgs()
        {
            ReloadTimestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of the ConfigurationReloadEventArgs class
        /// </summary>
        /// <param name="isSuccessful">Whether the reload was successful</param>
        /// <param name="configurationPath">Path to the configuration file</param>
        /// <param name="errorMessage">Error message if reload failed</param>
        public ConfigurationReloadEventArgs(bool isSuccessful, string configurationPath, string errorMessage = null)
        {
            ReloadTimestamp = DateTime.UtcNow;
            IsSuccessful = isSuccessful;
            ConfigurationPath = configurationPath ?? throw new ArgumentNullException(nameof(configurationPath));
            ErrorMessage = errorMessage ?? string.Empty;
        }
    }
}