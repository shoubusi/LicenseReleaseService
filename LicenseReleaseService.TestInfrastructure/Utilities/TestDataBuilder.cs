using System;
using System.Collections.Generic;

namespace LicenseReleaseService.TestInfrastructure.Utilities;

/// <summary>
/// Utility class for building test data scenarios
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates a license server response for testing
    /// </summary>
    /// <param name="serverAddress">The license server address</param>
    /// <param name="totalLicenses">Total number of licenses</param>
    /// <param name="usedLicenses">Number of used licenses</param>
    /// <param name="features">List of licensed features</param>
    /// <returns>Mock lmstat output</returns>
    public static string CreateLicenseServerResponse(
        string serverAddress = "license-server.company.com",
        int totalLicenses = 10,
        int usedLicenses = 4,
        params string[] features)
    {
        var timestamp = DateTime.UtcNow.ToString("ddd MMM dd HH:mm:ss yyyy");
        var featuresList = features.Length > 0 ? features : new[] { "SolidWorks", "SolidWorks_PDM", "SolidWorks_Viewer" };

        var response = $"Users of {featuresList[0]}:\n" +
                      $"(Total of {totalLicenses} licenses issued;  Total of {usedLicenses} licenses in use)\n\n";

        for (int i = 0; i < usedLicenses; i++)
        {
            response += $"user{i} computer{i}{i % 10}.company.com {timestamp} (v2025.0) (SERVER-{serverAddress.ToUpper()})\n";
        }

        for (int i = 1; i < featuresList.Length; i++)
        {
            response += $"\nUsers of {featuresList[i]}:\n" +
                         $"(Total of {totalLicenses} licenses issued;  Total of {Math.Min(usedLicenses / 2, totalLicenses)} licenses in use)\n\n";

            for (int j = 0; j < Math.Min(usedLicenses / 2, totalLicenses); j++)
            {
                response += $"user{j} computer{j}{j % 10}.company.com {timestamp} (v2025.0) (SERVER-{serverAddress.ToUpper()})\n";
            }
        }

        return response;
    }

    /// <summary>
    /// Creates configuration data for testing
    /// </summary>
    /// <param name="serverAddress">License server address</param>
    /// <param name="pollInterval">Poll interval in milliseconds</param>
    /// <param name="healthCheckInterval">Health check interval in milliseconds</param>
    /// <returns>Test configuration dictionary</returns>
    public static Dictionary<string, string> CreateServiceConfiguration(
        string serverAddress = "license-server.company.com",
        int pollInterval = 30000,
        int healthCheckInterval = 60000)
    {
        return new Dictionary<string, string>
        {
            {"ServiceSettings:LicenseServerAddress", serverAddress},
            {"ServiceSettings:PollIntervalMs", pollInterval.ToString()},
            {"ServiceSettings:HealthCheckIntervalMs", healthCheckInterval.ToString()},
            {"ServiceSettings:MaxRetryAttempts", "3"},
            {"ServiceSettings:RetryDelayMs", "1000"},
            {"ServiceSettings:EnableLogging", "true"},
            {"ServiceSettings:LogLevel", "Information"},
            {"ServiceSettings:LicenseQueryTimeoutMs", "10000"},
            {"ServiceSettings:MaxConcurrentQueries", "5"}
        };
    }

    /// <summary>
    /// Creates test data for configuration validation scenarios
    /// </summary>
    /// <returns>Test configuration scenarios</returns>
    public static IEnumerable<ConfigurationTestCase> CreateConfigurationTestCases()
    {
        yield return new ConfigurationTestCase
        {
            Name = "Valid Configuration",
            Configuration = CreateServiceConfiguration(),
            ShouldBeValid = true,
            Description = "Standard valid configuration"
        };

        yield return new ConfigurationTestCase
        {
            Name = "Empty Server Address",
            Configuration = CreateServiceConfiguration(serverAddress: ""),
            ShouldBeValid = false,
            Description = "Invalid configuration with empty server address"
        };

        yield return new ConfigurationTestCase
        {
            Name = "Negative Poll Interval",
            Configuration = CreateServiceConfiguration(pollInterval: -1000),
            ShouldBeValid = false,
            Description = "Invalid configuration with negative poll interval"
        };

        yield return new ConfigurationTestCase
        {
            Name = "Zero Health Check Interval",
            Configuration = CreateServiceConfiguration(healthCheckInterval: 0),
            ShouldBeValid = false,
            Description = "Invalid configuration with zero health check interval"
        };

        yield return new ConfigurationTestCase
        {
            Name = "Large Poll Interval",
            Configuration = CreateServiceConfiguration(pollInterval: 300000),
            ShouldBeValid = true,
            Description = "Valid configuration with large poll interval (5 minutes)"
        };
    }

    /// <summary>
    /// Creates test scenarios for license management operations
    /// </summary>
    /// <returns>License management test scenarios</returns>
    public static IEnumerable<LicenseManagementTestCase> CreateLicenseManagementTestCases()
    {
        yield return new LicenseManagementTestCase
        {
            Name = "Available Licenses",
            ServerResponse = CreateLicenseServerResponse(totalLicenses: 10, usedLicenses: 3),
            ExpectedAvailableLicenses = 7,
            ExpectedUsedLicenses = 3,
            ShouldSucceed = true,
            Description = "License query should return available licenses"
        };

        yield return new LicenseManagementTestCase
        {
            Name = "All Licenses In Use",
            ServerResponse = CreateLicenseServerResponse(totalLicenses: 5, usedLicenses: 5),
            ExpectedAvailableLicenses = 0,
            ExpectedUsedLicenses = 5,
            ShouldSucceed = true,
            Description = "All licenses are currently in use"
        };

        yield return new LicenseManagementTestCase
        {
            Name = "No Licenses Available",
            ServerResponse = CreateLicenseServerResponse(totalLicenses: 0, usedLicenses: 0),
            ExpectedAvailableLicenses = 0,
            ExpectedUsedLicenses = 0,
            ShouldSucceed = true,
            Description = "No licenses are configured"
        };

        yield return new LicenseManagementTestCase
        {
            Name = "Server Unavailable",
            ServerResponse = "",
            ExpectedAvailableLicenses = 0,
            ExpectedUsedLicenses = 0,
            ShouldSucceed = false,
            Description = "License server is not responding"
        };
    }

    /// <summary>
    /// Creates performance test data scenarios
    /// </summary>
    /// <returns>Performance test scenarios</returns>
    public static IEnumerable<PerformanceTestCase> CreatePerformanceTestCases()
    {
        yield return new PerformanceTestCase
        {
            Name = "Light Load",
            ConcurrentQueries = 1,
            QueryCount = 10,
            ExpectedMaxResponseTimeMs = 1000,
            Description = "Single user with occasional queries"
        };

        yield return new PerformanceTestCase
        {
            Name = "Medium Load",
            ConcurrentQueries = 5,
            QueryCount = 50,
            ExpectedMaxResponseTimeMs = 2000,
            Description = "Multiple users with regular queries"
        };

        yield return new PerformanceTestCase
        {
            Name = "Heavy Load",
            ConcurrentQueries = 10,
            QueryCount = 100,
            ExpectedMaxResponseTimeMs = 5000,
            Description = "High concurrent query load"
        };
    }
}

/// <summary>
/// Test case for configuration validation
/// </summary>
public class ConfigurationTestCase
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Configuration { get; set; } = new();
    public bool ShouldBeValid { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Test case for license management operations
/// </summary>
public class LicenseManagementTestCase
{
    public string Name { get; set; } = string.Empty;
    public string ServerResponse { get; set; } = string.Empty;
    public int ExpectedAvailableLicenses { get; set; }
    public int ExpectedUsedLicenses { get; set; }
    public bool ShouldSucceed { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Test case for performance testing
/// </summary>
public class PerformanceTestCase
{
    public string Name { get; set; } = string.Empty;
    public int ConcurrentQueries { get; set; }
    public int QueryCount { get; set; }
    public int ExpectedMaxResponseTimeMs { get; set; }
    public string Description { get; set; } = string.Empty;
}