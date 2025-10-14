using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace LicenseReleaseService.TestInfrastructure.Logging;

/// <summary>
/// Provides test-specific logging configuration that mirrors production patterns
/// </summary>
public static class TestLoggingConfiguration
{
    /// <summary>
    /// Configures logging services for test environments
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">Optional configuration for logging settings</param>
    /// <returns>The configured service collection for chaining</returns>
    public static IServiceCollection ConfigureTestLogging(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Configure logging options
        services.Configure<LoggerFilterOptions>(options =>
        {
            options.AddFilter("Default", LogLevel.Debug);
            options.AddFilter("Microsoft", LogLevel.Warning);
            options.AddFilter("System", LogLevel.Warning);
            options.AddFilter("LicenseReleaseService", LogLevel.Debug);
            options.AddFilter("LicenseReleaseService.Tests", LogLevel.Debug);
        });

        // Add logging with console provider for test output
        services.AddLogging(builder =>
        {
            builder.ClearProviders();

            // Add console provider for test output
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Error;
            });

            // Add debug provider for Visual Studio output
            builder.AddDebug();

            // Note: File logging can be added later if needed
            // if (configuration?.GetValue<bool>("Logging:EnableFileLogging") == true)
            // {
            //     // Custom file logging implementation would go here
            // }
        });

        return services;
    }

    /// <summary>
    /// Creates a test logger instance for a specific type
    /// </summary>
    /// <typeparam name="T">The type to create logger for</typeparam>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>Configured logger instance</returns>
    public static ILogger<T> CreateTestLogger<T>(this System.IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        if (loggerFactory == null)
            throw new InvalidOperationException("ILoggerFactory is not registered");
        return loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a test logger instance with a specific category name
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="categoryName">The logger category name</param>
    /// <returns>Configured logger instance</returns>
    public static ILogger CreateTestLogger(this System.IServiceProvider serviceProvider, string categoryName)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));
        if (string.IsNullOrEmpty(categoryName))
            throw new ArgumentException("Category name cannot be null or empty", nameof(categoryName));

        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        if (loggerFactory == null)
            throw new InvalidOperationException("ILoggerFactory is not registered");
        return loggerFactory.CreateLogger(categoryName);
    }
}