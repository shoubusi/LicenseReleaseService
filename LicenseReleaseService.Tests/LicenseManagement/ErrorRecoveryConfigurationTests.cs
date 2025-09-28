using System;
using System.Collections.Generic;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for ErrorRecoveryConfiguration classes
    /// </summary>
    public class ErrorRecoveryConfigurationTests
    {
        [Fact]
        public void GracefulDegradationConfiguration_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new GracefulDegradationConfiguration();

            // Assert
            Assert.True(config.IsEnabled);
            Assert.Equal(DegradationLevel.Full, config.DefaultDegradationLevel);
            Assert.Equal(TimeSpan.FromMinutes(5), config.AutoRecoveryInterval);
            Assert.NotNull(config.DegradationRules);
            Assert.Empty(config.DegradationRules);
            Assert.Equal(3, config.ConcurrentDegradationLimit);
        }

        [Fact]
        public void GracefulDegradationConfiguration_WithValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new GracefulDegradationConfiguration
            {
                IsEnabled = false,
                DefaultDegradationLevel = DegradationLevel.Severe,
                AutoRecoveryInterval = TimeSpan.FromMinutes(10),
                DegradationRules = new List<DegradationRule>
                {
                    new DegradationRule
                    {
                        ComponentName = "test_component",
                        OperationName = "test_operation",
                        DegradationLevel = DegradationLevel.Minimal,
                        IsEnabled = true
                    }
                },
                ConcurrentDegradationLimit = 5
            };

            // Assert
            Assert.False(config.IsEnabled);
            Assert.Equal(DegradationLevel.Severe, config.DefaultDegradationLevel);
            Assert.Equal(TimeSpan.FromMinutes(10), config.AutoRecoveryInterval);
            Assert.Single(config.DegradationRules);
            Assert.Equal(5, config.ConcurrentDegradationLimit);
        }

        [Fact]
        public void FallbackStrategyConfiguration_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new FallbackStrategyConfiguration();

            // Assert
            Assert.True(config.IsEnabled);
            Assert.NotNull(config.Strategies);
            Assert.Empty(config.Strategies);
            Assert.Equal(FallbackExecutionOrder.Sequential, config.DefaultExecutionOrder);
            Assert.Equal(TimeSpan.FromSeconds(30), config.DefaultTimeout);
            Assert.Equal(3, config.MaxConcurrentFallbacks);
        }

        [Fact]
        public void FallbackStrategyConfiguration_WithValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new FallbackStrategyConfiguration
            {
                IsEnabled = false,
                Strategies = new List<FallbackStrategy>
                {
                    new FallbackStrategy
                    {
                        Name = "test_strategy",
                        OperationName = "test_operation",
                        FallbackOperation = async () => { await System.Threading.Tasks.Task.Delay(10); return "fallback_result"; },
                        ExecutionOrder = FallbackExecutionOrder.Parallel,
                        Timeout = TimeSpan.FromSeconds(60),
                        IsEnabled = true
                    }
                },
                DefaultExecutionOrder = FallbackExecutionOrder.Priority,
                DefaultTimeout = TimeSpan.FromSeconds(60),
                MaxConcurrentFallbacks = 5
            };

            // Assert
            Assert.False(config.IsEnabled);
            Assert.Single(config.Strategies);
            Assert.Equal(FallbackExecutionOrder.Priority, config.DefaultExecutionOrder);
            Assert.Equal(TimeSpan.FromSeconds(60), config.DefaultTimeout);
            Assert.Equal(5, config.MaxConcurrentFallbacks);
        }

        [Fact]
        public void CircuitBreakerConfiguration_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new CircuitBreakerConfiguration();

            // Assert
            Assert.True(config.IsEnabled);
            Assert.Equal(5, config.FailureThreshold);
            Assert.Equal(TimeSpan.FromMinutes(1), config.RecoveryTimeout);
            Assert.Equal(TimeSpan.FromSeconds(30), config.Timeout);
            Assert.Equal(10, config.ConcurrentBreakerLimit);
        }

        [Fact]
        public void CircuitBreakerConfiguration_WithValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new CircuitBreakerConfiguration
            {
                IsEnabled = false,
                FailureThreshold = 3,
                RecoveryTimeout = TimeSpan.FromMinutes(2),
                Timeout = TimeSpan.FromMinutes(1),
                ConcurrentBreakerLimit = 15
            };

            // Assert
            Assert.False(config.IsEnabled);
            Assert.Equal(3, config.FailureThreshold);
            Assert.Equal(TimeSpan.FromMinutes(2), config.RecoveryTimeout);
            Assert.Equal(TimeSpan.FromMinutes(1), config.Timeout);
            Assert.Equal(15, config.ConcurrentBreakerLimit);
        }

        [Fact]
        public void RetryPolicyConfiguration_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new RetryPolicyConfiguration();

            // Assert
            Assert.True(config.IsEnabled);
            Assert.Equal(3, config.MaxRetries);
            Assert.Equal(TimeSpan.FromSeconds(1), config.BaseDelay);
            Assert.Equal(RetryType.ExponentialBackoff, config.RetryType);
            Assert.Equal(2.0, config.BackoffMultiplier);
            Assert.Equal(TimeSpan.FromSeconds(30), config.MaxRetryDelay);
        }

        [Fact]
        public void RetryPolicyConfiguration_WithValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new RetryPolicyConfiguration
            {
                IsEnabled = false,
                MaxRetries = 5,
                BaseDelay = TimeSpan.FromMilliseconds(500),
                RetryType = RetryType.Linear,
                BackoffMultiplier = 1.5,
                MaxRetryDelay = TimeSpan.FromMinutes(1)
            };

            // Assert
            Assert.False(config.IsEnabled);
            Assert.Equal(5, config.MaxRetries);
            Assert.Equal(TimeSpan.FromMilliseconds(500), config.BaseDelay);
            Assert.Equal(RetryType.Linear, config.RetryType);
            Assert.Equal(1.5, config.BackoffMultiplier);
            Assert.Equal(TimeSpan.FromMinutes(1), config.MaxRetryDelay);
        }

        [Fact]
        public void HealthMonitoringConfiguration_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new HealthMonitoringConfiguration();

            // Assert
            Assert.True(config.IsEnabled);
            Assert.Equal(TimeSpan.FromSeconds(30), config.CheckInterval);
            Assert.Equal(TimeSpan.FromSeconds(10), config.CheckTimeout);
            Assert.Equal(3, config.ConsecutiveFailureThreshold);
            Assert.NotNull(config.HealthChecks);
            Assert.Empty(config.HealthChecks);
        }

        [Fact]
        public void HealthMonitoringConfiguration_WithValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var config = new HealthMonitoringConfiguration
            {
                IsEnabled = false,
                CheckInterval = TimeSpan.FromMinutes(1),
                CheckTimeout = TimeSpan.FromSeconds(30),
                ConsecutiveFailureThreshold = 5,
                HealthChecks = new List<HealthCheckDefinition>
                {
                    new HealthCheckDefinition
                    {
                        Name = "test_check",
                        ComponentName = "test_component",
                        CheckType = HealthCheckType.Connectivity,
                        IsEnabled = true
                    }
                }
            };

            // Assert
            Assert.False(config.IsEnabled);
            Assert.Equal(TimeSpan.FromMinutes(1), config.CheckInterval);
            Assert.Equal(TimeSpan.FromSeconds(30), config.CheckTimeout);
            Assert.Equal(5, config.ConsecutiveFailureThreshold);
            Assert.Single(config.HealthChecks);
        }

        [Fact]
        public void ErrorRecoverySettings_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var settings = new ErrorRecoverySettings();

            // Assert
            Assert.True(settings.IsEnabled);
            Assert.NotNull(settings.GracefulDegradation);
            Assert.NotNull(settings.FallbackStrategies);
            Assert.NotNull(settings.CircuitBreaker);
            Assert.NotNull(settings.RetryPolicy);
            Assert.NotNull(settings.HealthMonitoring);
            Assert.Equal(ErrorRecoveryMode.Aggressive, settings.RecoveryMode);
            Assert.Equal(50, settings.RecoveryQueueSize);
            Assert.Equal(10, settings.MaxConcurrentRecoveries);
            Assert.Equal(TimeSpan.FromSeconds(30), settings.RecoveryProcessingInterval);
        }

        [Fact]
        public void ErrorRecoverySettings_WithValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var settings = new ErrorRecoverySettings
            {
                IsEnabled = false,
                GracefulDegradation = new GracefulDegradationConfiguration
                {
                    IsEnabled = false,
                    DefaultDegradationLevel = DegradationLevel.Emergency
                },
                FallbackStrategies = new FallbackStrategyConfiguration
                {
                    IsEnabled = false,
                    DefaultExecutionOrder = FallbackExecutionOrder.Parallel
                },
                CircuitBreaker = new CircuitBreakerConfiguration
                {
                    IsEnabled = false,
                    FailureThreshold = 10
                },
                RetryPolicy = new RetryPolicyConfiguration
                {
                    IsEnabled = false,
                    MaxRetries = 10
                },
                HealthMonitoring = new HealthMonitoringConfiguration
                {
                    IsEnabled = false,
                    CheckInterval = TimeSpan.FromMinutes(5)
                },
                RecoveryMode = ErrorRecoveryMode.Conservative,
                RecoveryQueueSize = 100,
                MaxConcurrentRecoveries = 20,
                RecoveryProcessingInterval = TimeSpan.FromMinutes(1)
            };

            // Assert
            Assert.False(settings.IsEnabled);
            Assert.NotNull(settings.GracefulDegradation);
            Assert.NotNull(settings.FallbackStrategies);
            Assert.NotNull(settings.CircuitBreaker);
            Assert.NotNull(settings.RetryPolicy);
            Assert.NotNull(settings.HealthMonitoring);
            Assert.Equal(ErrorRecoveryMode.Conservative, settings.RecoveryMode);
            Assert.Equal(100, settings.RecoveryQueueSize);
            Assert.Equal(20, settings.MaxConcurrentRecoveries);
            Assert.Equal(TimeSpan.FromMinutes(1), settings.RecoveryProcessingInterval);
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_WithValidSettings_ShouldInitializeCorrectly()
        {
            // Arrange
            var settings = new ErrorRecoverySettings
            {
                IsEnabled = true,
                RecoveryMode = ErrorRecoveryMode.Balanced,
                RecoveryQueueSize = 75
            };

            // Act
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Assert
            Assert.NotNull(manager);
            Assert.NotNull(manager.Settings);
            Assert.Same(settings, manager.Settings);
            Assert.True(manager.Settings.IsEnabled);
            Assert.Equal(ErrorRecoveryMode.Balanced, manager.Settings.RecoveryMode);
            Assert.Equal(75, manager.Settings.RecoveryQueueSize);
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_WithNullSettings_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ErrorRecoveryConfigurationManager(null!));
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_GetValue_WithExistingKey_ShouldReturnValue()
        {
            // Arrange
            var settings = new ErrorRecoverySettings
            {
                IsEnabled = true,
                RecoveryMode = ErrorRecoveryMode.Balanced
            };
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Act
            var recoveryMode = manager.GetValue<ErrorRecoveryMode>("RecoveryMode");

            // Assert
            Assert.Equal(ErrorRecoveryMode.Balanced, recoveryMode);
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_GetValue_WithNonExistingKey_ShouldReturnDefault()
        {
            // Arrange
            var settings = new ErrorRecoverySettings();
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Act
            var defaultValue = manager.GetValue<int>("NonExistingKey", 42);

            // Assert
            Assert.Equal(42, defaultValue);
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_GetValue_WithInvalidCast_ShouldThrowException()
        {
            // Arrange
            var settings = new ErrorRecoverySettings
            {
                IsEnabled = true
            };
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => manager.GetValue<DateTime>("IsEnabled"));
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_UpdateValue_WithValidKey_ShouldUpdateValue()
        {
            // Arrange
            var settings = new ErrorRecoverySettings
            {
                RecoveryMode = ErrorRecoveryMode.Aggressive
            };
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Act
            manager.UpdateValue("RecoveryMode", ErrorRecoveryMode.Conservative);

            // Assert
            Assert.Equal(ErrorRecoveryMode.Conservative, manager.Settings.RecoveryMode);
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_UpdateValue_WithNestedPath_ShouldUpdateNestedValue()
        {
            // Arrange
            var settings = new ErrorRecoverySettings
            {
                CircuitBreaker = new CircuitBreakerConfiguration
                {
                    FailureThreshold = 5
                }
            };
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Act
            manager.UpdateValue("CircuitBreaker.FailureThreshold", 10);

            // Assert
            Assert.Equal(10, manager.Settings.CircuitBreaker.FailureThreshold);
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_UpdateValue_WithInvalidKey_ShouldThrowException()
        {
            // Arrange
            var settings = new ErrorRecoverySettings();
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => manager.UpdateValue("InvalidKey", "value"));
        }

        [Fact]
        public void ErrorRecoveryConfigurationManager_ReloadConfiguration_ShouldReload()
        {
            // Arrange
            var settings = new ErrorRecoverySettings
            {
                RecoveryMode = ErrorRecoveryMode.Aggressive
            };
            var manager = new ErrorRecoveryConfigurationManager(settings);

            // Act
            var exception = Record.Exception(() => manager.ReloadConfiguration());

            // Assert
            Assert.Null(exception); // Should not throw in basic implementation
        }

        [Fact]
        public void ErrorRecoveryMode_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(0, (int)ErrorRecoveryMode.Conservative);
            Assert.Equal(1, (int)ErrorRecoveryMode.Balanced);
            Assert.Equal(2, (int)ErrorRecoveryMode.Aggressive);
        }

        [Fact]
        public void DegradationLevel_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(0, (int)DegradationLevel.Full);
            Assert.Equal(1, (int)DegradationLevel.Minimal);
            Assert.Equal(2, (int)DegradationLevel.Moderate);
            Assert.Equal(3, (int)DegradationLevel.Severe);
            Assert.Equal(4, (int)DegradationLevel.Emergency);
        }

        [Fact]
        public void FallbackExecutionOrder_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(0, (int)FallbackExecutionOrder.Sequential);
            Assert.Equal(1, (int)FallbackExecutionOrder.Parallel);
            Assert.Equal(2, (int)FallbackExecutionOrder.All);
            Assert.Equal(3, (int)FallbackExecutionOrder.Priority);
        }

        [Fact]
        public void RetryType_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(0, (int)RetryType.Fixed);
            Assert.Equal(1, (int)RetryType.Linear);
            Assert.Equal(2, (int)RetryType.ExponentialBackoff);
            Assert.Equal(3, (int)RetryType.ExponentialWithJitter);
        }

        [Fact]
        public void HealthCheckType_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.Equal(0, (int)HealthCheckType.Connectivity);
            Assert.Equal(1, (int)HealthCheckType.Performance);
            Assert.Equal(2, (int)HealthCheckType.Resource);
            Assert.Equal(3, (int)HealthCheckType.Custom);
        }

        [Fact]
        public void ErrorRecoverySettings_ToString_ShouldReturnMeaningfulString()
        {
            // Arrange
            var settings = new ErrorRecoverySettings
            {
                IsEnabled = true,
                RecoveryMode = ErrorRecoveryMode.Balanced,
                RecoveryQueueSize = 100,
                MaxConcurrentRecoveries = 20,
                RecoveryProcessingInterval = TimeSpan.FromSeconds(30)
            };

            // Act
            var result = settings.ToString();

            // Assert
            Assert.Contains("IsEnabled=True", result);
            Assert.Contains("RecoveryMode=Balanced", result);
            Assert.Contains("QueueSize=100", result);
            Assert.Contains("MaxConcurrent=20", result);
            Assert.Contains("ProcessingInterval=30.0s", result);
        }

        [Fact]
        public void HealthCheckDefinition_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var definition = new HealthCheckDefinition
            {
                Name = "test_check",
                ComponentName = "test_component",
                CheckType = HealthCheckType.Connectivity,
                IsEnabled = true,
                Timeout = TimeSpan.FromSeconds(30),
                Interval = TimeSpan.FromMinutes(5),
                Description = "Test health check"
            };

            // Assert
            Assert.Equal("test_check", definition.Name);
            Assert.Equal("test_component", definition.ComponentName);
            Assert.Equal(HealthCheckType.Connectivity, definition.CheckType);
            Assert.True(definition.IsEnabled);
            Assert.Equal(TimeSpan.FromSeconds(30), definition.Timeout);
            Assert.Equal(TimeSpan.FromMinutes(5), definition.Interval);
            Assert.Equal("Test health check", definition.Description);
        }
    }
}