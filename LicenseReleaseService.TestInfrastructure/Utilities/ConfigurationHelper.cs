using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace LicenseReleaseService.TestInfrastructure.Utilities;

/// <summary>
/// Helper class for managing test configurations
/// </summary>
public static class ConfigurationHelper
{
    private static readonly Dictionary<string, IConfiguration> _configurationCache = new();

    /// <summary>
    /// Gets or creates a test configuration with specified settings
    /// </summary>
    /// <param name="settings">Optional settings to override defaults</param>
    /// <param name="configName">Name for caching the configuration</param>
    /// <returns>Configuration instance</returns>
    public static IConfiguration GetTestConfiguration(
        Dictionary<string, string>? settings = null,
        string configName = "default")
    {
        if (_configurationCache.TryGetValue(configName, out var cachedConfig))
        {
            return cachedConfig;
        }

        var configurationBuilder = new ConfigurationBuilder();

        // Add default test settings
        var defaultSettings = TestDataBuilder.CreateServiceConfiguration();

        // Override with provided settings
        if (settings != null)
        {
            foreach (var setting in settings)
            {
                defaultSettings[setting.Key] = setting.Value;
            }
        }

        // Add in-memory collection
        configurationBuilder.AddInMemoryCollection(defaultSettings);

        // Add environment variables (optional for testing)
        configurationBuilder.AddEnvironmentVariables();

        var configuration = configurationBuilder.Build();
        _configurationCache[configName] = configuration;

        return configuration;
    }

    /// <summary>
    /// Creates a configuration for integration tests
    /// </summary>
    /// <param name="useRealServices">Whether to use real external services</param>
    /// <returns>Integration test configuration</returns>
    public static IConfiguration GetIntegrationTestConfiguration(bool useRealServices = false)
    {
        var settings = new Dictionary<string, string>
        {
            {"TestEnvironment", "Integration"},
            {"UseRealServices", useRealServices.ToString()},
            {"ServiceSettings:LicenseServerAddress", useRealServices ? "license-server.company.com" : "localhost"},
            {"ServiceSettings:PollIntervalMs", useRealServices ? "30000" : "5000"},
            {"ServiceSettings:HealthCheckIntervalMs", useRealServices ? "60000" : "10000"},
            {"Logging:LogLevel:Default", useRealServices ? "Information" : "Debug"},
            {"Logging:EnableFileLogging", "true"},
            {"Logging:FilePath", Path.Combine(Path.GetTempPath(), "test-integration-logs.txt")}
        };

        return GetTestConfiguration(settings, "integration");
    }

    /// <summary>
    /// Creates a configuration for performance tests
    /// </summary>
    /// <param name="concurrentUsers">Number of concurrent users to simulate</param>
    /// <returns>Performance test configuration</returns>
    public static IConfiguration GetPerformanceTestConfiguration(int concurrentUsers = 10)
    {
        var settings = new Dictionary<string, string>
        {
            {"TestEnvironment", "Performance"},
            {"ConcurrentUsers", concurrentUsers.ToString()},
            {"ServiceSettings:PollIntervalMs", "1000"},
            {"ServiceSettings:HealthCheckIntervalMs", "5000"},
            {"ServiceSettings:MaxConcurrentQueries", concurrentUsers.ToString()},
            {"ServiceSettings:LicenseQueryTimeoutMs", "30000"},
            {"Logging:LogLevel:Default", "Warning"}, // Reduce logging overhead during performance tests
            {"Logging:EnableFileLogging", "false"}
        };

        return GetTestConfiguration(settings, $"performance_{concurrentUsers}");
    }

    /// <summary>
    /// Creates a configuration for stress testing
    /// </summary>
    /// <param name="loadFactor">Multiplier for normal load (e.g., 5x, 10x)</param>
    /// <returns>Stress test configuration</returns>
    public static IConfiguration GetStressTestConfiguration(int loadFactor = 5)
    {
        var settings = new Dictionary<string, string>
        {
            {"TestEnvironment", "Stress"},
            {"LoadFactor", loadFactor.ToString()},
            {"ServiceSettings:PollIntervalMs", "500"},
            {"ServiceSettings:HealthCheckIntervalMs", "2000"},
            {"ServiceSettings:MaxConcurrentQueries", (50 * loadFactor).ToString()},
            {"ServiceSettings:MaxRetryAttempts", "1"}, // Reduce retries during stress testing
            {"ServiceSettings:RetryDelayMs", "100"},
            {"Logging:LogLevel:Default", "Error"}, // Minimal logging during stress tests
            {"Logging:EnableFileLogging", "false"}
        };

        return GetTestConfiguration(settings, $"stress_{loadFactor}x");
    }

    /// <summary>
    /// Creates a configuration with invalid settings for negative testing
    /// </summary>
    /// <param name="invalidityType">Type of invalidity to introduce</param>
    /// <returns>Invalid configuration</returns>
    public static IConfiguration GetInvalidConfiguration(InvalidConfigurationType invalidityType)
    {
        var settings = new Dictionary<string, string>();

        switch (invalidityType)
        {
            case InvalidConfigurationType.EmptyServerAddress:
                settings["ServiceSettings:LicenseServerAddress"] = "";
                break;

            case InvalidConfigurationType.NullServerAddress:
                settings.Remove("ServiceSettings:LicenseServerAddress");
                break;

            case InvalidConfigurationType.NegativePollInterval:
                settings["ServiceSettings:PollIntervalMs"] = "-1000";
                break;

            case InvalidConfigurationType.ZeroTimeout:
                settings["ServiceSettings:LicenseQueryTimeoutMs"] = "0";
                break;

            case InvalidConfigurationType.InvalidRetryCount:
                settings["ServiceSettings:MaxRetryAttempts"] = "-1";
                break;

            case InvalidConfigurationType.InvalidLogLevel:
                settings["Logging:LogLevel:Default"] = "InvalidLogLevel";
                break;
        }

        return GetTestConfiguration(settings, $"invalid_{invalidityType}");
    }

    /// <summary>
    /// Validates a configuration object
    /// </summary>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>Validation result</returns>
    public static ConfigurationValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new ConfigurationValidationResult { IsValid = true };

        try
        {
            // Validate required settings
            var serverAddress = configuration["ServiceSettings:LicenseServerAddress"];
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                result.IsValid = false;
                result.Errors.Add("License server address is required");
            }

            // Validate numeric settings
            if (int.TryParse(configuration["ServiceSettings:PollIntervalMs"], out var pollInterval))
            {
                if (pollInterval <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Poll interval must be greater than 0");
                }
            }
            else
            {
                result.IsValid = false;
                result.Errors.Add("Invalid poll interval format");
            }

            if (int.TryParse(configuration["ServiceSettings:HealthCheckIntervalMs"], out var healthCheckInterval))
            {
                if (healthCheckInterval <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Health check interval must be greater than 0");
                }
            }
            else
            {
                result.IsValid = false;
                result.Errors.Add("Invalid health check interval format");
            }

            if (int.TryParse(configuration["ServiceSettings:MaxRetryAttempts"], out var maxRetries))
            {
                if (maxRetries < 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Max retry attempts cannot be negative");
                }
            }
            else
            {
                result.IsValid = false;
                result.Errors.Add("Invalid max retry attempts format");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Configuration validation failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Clears the configuration cache
    /// </summary>
    public static void ClearCache()
    {
        _configurationCache.Clear();
    }

    /// <summary>
    /// Gets configuration value with type safety
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="configuration">Configuration to read from</param>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Typed configuration value</returns>
    public static T GetValue<T>(IConfiguration configuration, string key, T defaultValue = default!)
    {
        var value = configuration[key];
        if (value == null)
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}

/// <summary>
/// Types of invalid configurations for testing
/// </summary>
public enum InvalidConfigurationType
{
    EmptyServerAddress,
    NullServerAddress,
    NegativePollInterval,
    ZeroTimeout,
    InvalidRetryCount,
    InvalidLogLevel
}

/// <summary>
/// Result of configuration validation
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public string GetCombinedErrors()
    {
        return string.Join("; ", Errors);
    }

    public string GetCombinedWarnings()
    {
        return string.Join("; ", Warnings);
    }
}