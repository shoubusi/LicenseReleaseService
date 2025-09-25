using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for RecoveryManager class
    /// </summary>
    public class RecoveryManagerTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public RecoveryManagerTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RecoveryManager(null));
        }

        [Fact]
        public void Constructor_WithValidLogger_ShouldCreateInstance()
        {
            // Act
            var recoveryManager = new RecoveryManager(_mockLogger.Object);

            // Assert
            Assert.NotNull(recoveryManager);
        }

        [Fact]
        public void Constructor_ShouldRegisterDefaultScenarios()
        {
            // Act
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenarios = recoveryManager.GetRegisteredScenarios();

            // Assert
            Assert.NotEmpty(scenarios);
            Assert.True(scenarios.Count >= 3); // Network, Process, Timeout scenarios
            Assert.Contains(scenarios, s => s.Id == "network-connectivity");
            Assert.Contains(scenarios, s => s.Id == "process-execution");
            Assert.Contains(scenarios, s => s.Id == "timeout-recovery");
        }

        [Fact]
        public void RegisterScenario_WithNullScenario_ShouldThrowArgumentNullException()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => recoveryManager.RegisterScenario(null));
        }

        [Fact]
        public void RegisterScenario_WithValidScenario_ShouldRegisterSuccessfully()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "test-scenario",
                Name = "Test Scenario",
                Description = "Test scenario for unit testing",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>(),
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            // Act
            recoveryManager.RegisterScenario(scenario);

            // Assert
            var scenarios = recoveryManager.GetRegisteredScenarios();
            Assert.Contains(scenarios, s => s.Id == "test-scenario");
        }

        [Fact]
        public void RegisterScenario_WithEmptyId_ShouldThrowArgumentException()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "",
                Name = "Test Scenario",
                Description = "Test scenario for unit testing",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>(),
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => recoveryManager.RegisterScenario(scenario));
        }

        [Fact]
        public async Task RecoverAsync_WithNullException_ShouldThrowArgumentNullException()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => recoveryManager.RecoverAsync(null));
        }

        [Fact]
        public async Task RecoverAsync_WithUnknownExceptionType_ShouldReturnFailureResult()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var exception = new ArgumentException("Test exception");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("No recovery scenario found for this exception type", result.ErrorMessage);
            Assert.Same(exception, result.OriginalException);
        }

        [Fact]
        public async Task RecoverAsync_WithSocketException_ShouldExecuteNetworkRecovery()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var exception = new SocketException();

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("network-connectivity", result.ScenarioId);
            Assert.Same(exception, result.OriginalException);
        }

        [Fact]
        public async Task RecoverAsync_WithProcessExecutionException_ShouldExecuteProcessRecovery()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var exception = new ProcessExecutionException("Process failed");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("process-execution", result.ScenarioId);
            Assert.Same(exception, result.OriginalException);
        }

        [Fact]
        public async Task RecoverAsync_WithTimeoutException_ShouldExecuteTimeoutRecovery()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var exception = new TimeoutException("Operation timed out");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("timeout-recovery", result.ScenarioId);
            Assert.Same(exception, result.OriginalException);
        }

        [Fact]
        public async Task RecoverAsync_WithSuccessfulAction_ShouldReturnSuccess()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "success-scenario",
                Name = "Success Scenario",
                Description = "Test scenario that always succeeds",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "SuccessfulAction",
                        Description = "Action that always succeeds",
                        Timeout = TimeSpan.FromSeconds(1),
                        IsCritical = false,
                        Action = () => Task.FromResult(true)
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(scenario);
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("success-scenario", result.ScenarioId);
            Assert.Single(result.Actions);
            Assert.True(result.Actions[0].Success);
            Assert.Equal("SuccessfulAction", result.Actions[0].ActionName);
        }

        [Fact]
        public async Task RecoverAsync_WithFailingCriticalAction_ShouldReturnFailure()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "failure-scenario",
                Name = "Failure Scenario",
                Description = "Test scenario with critical failure",
                Severity = RecoverySeverity.High,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "CriticalFailureAction",
                        Description = "Critical action that always fails",
                        Timeout = TimeSpan.FromSeconds(1),
                        IsCritical = true,
                        Action = () => Task.FromResult(false)
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(scenario);
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("failure-scenario", result.ScenarioId);
            Assert.Single(result.Actions);
            Assert.False(result.Actions[0].Success);
            Assert.Contains("Critical recovery action CriticalFailureAction failed", result.ErrorMessage);
        }

        [Fact]
        public async Task RecoverAsync_WithFailingNonCriticalAction_ShouldContinueAndSucceed()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "mixed-scenario",
                Name = "Mixed Scenario",
                Description = "Test scenario with mixed success",
                Severity = RecoverySeverity.Medium,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "NonCriticalFailureAction",
                        Description = "Non-critical action that fails",
                        Timeout = TimeSpan.FromSeconds(1),
                        IsCritical = false,
                        Action = () => Task.FromResult(false)
                    },
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Reset,
                        Name = "SuccessfulAction",
                        Description = "Action that succeeds",
                        Timeout = TimeSpan.FromSeconds(1),
                        IsCritical = false,
                        Action = () => Task.FromResult(true)
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(scenario);
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("mixed-scenario", result.ScenarioId);
            Assert.Equal(2, result.Actions.Count);
            Assert.False(result.Actions[0].Success); // First action failed
            Assert.True(result.Actions[1].Success);   // Second action succeeded
        }

        [Fact]
        public async Task RecoverAsync_WithCooldownPeriod_ShouldBlockRecovery()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "cooldown-scenario",
                Name = "Cooldown Scenario",
                Description = "Test scenario with cooldown",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "TestAction",
                        Description = "Test action",
                        Timeout = TimeSpan.FromSeconds(1),
                        IsCritical = false,
                        Action = () => Task.FromResult(true)
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(10),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(scenario);
            var exception = new InvalidOperationException("Test exception");

            // First recovery should succeed
            await recoveryManager.RecoverAsync(exception);

            // Second recovery should be blocked by cooldown
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("cooldown-scenario", result.ScenarioId);
            Assert.Contains("Recovery scenario is in cooldown period", result.ErrorMessage);
        }

        [Fact]
        public async Task RecoverAsync_WithActionTimeout_ShouldMarkActionAsFailed()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "timeout-scenario",
                Name = "Timeout Scenario",
                Description = "Test scenario with action timeout",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "TimeoutAction",
                        Description = "Action that times out",
                        Timeout = TimeSpan.FromMilliseconds(100),
                        IsCritical = false,
                        Action = async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                            return true;
                        }
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(scenario);
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("timeout-scenario", result.ScenarioId);
            Assert.Single(result.Actions);
            Assert.False(result.Actions[0].Success);
            Assert.True(result.Actions[0].ExecutionTime.TotalMilliseconds >= 100); // At least timeout duration
        }

        [Fact]
        public async Task RecoverAsync_WithCustomCondition_ShouldOnlyMatchWhenConditionMet()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "conditional-scenario",
                Name = "Conditional Scenario",
                Description = "Test scenario with custom condition",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Condition = ex => ex.Message.Contains("specific"),
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "TestAction",
                        Description = "Test action",
                        Timeout = TimeSpan.FromSeconds(1),
                        IsCritical = false,
                        Action = () => Task.FromResult(true)
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(scenario);

            // Act - exception that doesn't match condition
            var result1 = await recoveryManager.RecoverAsync(new InvalidOperationException("Generic error"));

            // Act - exception that matches condition
            var result2 = await recoveryManager.RecoverAsync(new InvalidOperationException("specific error"));

            // Assert
            Assert.NotNull(result1);
            Assert.False(result1.Success); // Should not find matching scenario
            Assert.Contains("No recovery scenario found", result1.ErrorMessage);

            Assert.NotNull(result2);
            Assert.True(result2.Success); // Should find and execute matching scenario
            Assert.Equal("conditional-scenario", result2.ScenarioId);
        }

        [Fact]
        public void GetRegisteredScenarios_ShouldReturnAllRegisteredScenarios()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var customScenario = new RecoveryScenario
            {
                Id = "custom-scenario",
                Name = "Custom Scenario",
                Description = "Custom test scenario",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>(),
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(customScenario);

            // Act
            var scenarios = recoveryManager.GetRegisteredScenarios();

            // Assert
            Assert.NotEmpty(scenarios);
            Assert.Contains(scenarios, s => s.Id == "custom-scenario");
            Assert.Contains(scenarios, s => s.Id == "network-connectivity");
        }

        [Fact]
        public void GetStatistics_ShouldReturnCurrentStatistics()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);

            // Act
            var stats = recoveryManager.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.TotalScenarios >= 3); // Default scenarios
            Assert.True(stats.EnabledScenarios >= 3); // Default scenarios are enabled
        }

        [Fact]
        public async Task RecoverAsync_WithExceptionInAction_ShouldHandleGracefully()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);
            var scenario = new RecoveryScenario
            {
                Id = "exception-scenario",
                Name = "Exception Scenario",
                Description = "Test scenario with action exception",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(InvalidOperationException) },
                Actions = new List<RecoveryAction>
                {
                    new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        Name = "ExceptionAction",
                        Description = "Action that throws exception",
                        Timeout = TimeSpan.FromSeconds(1),
                        IsCritical = false,
                        Action = () => throw new InvalidOperationException("Action failed")
                    }
                },
                MaxRecoveryAttempts = 1,
                CooldownPeriod = TimeSpan.FromSeconds(1),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(scenario);
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = await recoveryManager.RecoverAsync(exception);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("exception-scenario", result.ScenarioId);
            Assert.Single(result.Actions);
            Assert.False(result.Actions[0].Success);
            Assert.Contains("Action failed", result.Actions[0].ErrorMessage);
        }

        [Fact]
        public void FindMatchingScenario_ShouldSelectLowestSeverityOnMultipleMatches()
        {
            // Arrange
            var recoveryManager = new RecoveryManager(_mockLogger.Object);

            var lowSeverityScenario = new RecoveryScenario
            {
                Id = "low-severity",
                Name = "Low Severity",
                Severity = RecoverySeverity.Low,
                ExceptionTypes = new HashSet<Type> { typeof(SocketException) },
                Actions = new List<RecoveryAction>(),
                IsEnabled = true
            };

            var highSeverityScenario = new RecoveryScenario
            {
                Id = "high-severity",
                Name = "High Severity",
                Severity = RecoverySeverity.High,
                ExceptionTypes = new HashSet<Type> { typeof(SocketException) },
                Actions = new List<RecoveryAction>(),
                IsEnabled = true
            };

            recoveryManager.RegisterScenario(lowSeverityScenario);
            recoveryManager.RegisterScenario(highSeverityScenario);

            var exception = new SocketException();

            // Act
            var matchingScenario = recoveryManager.GetType()
                .GetMethod("FindMatchingScenario", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(recoveryManager, new object[] { exception }) as RecoveryScenario;

            // Assert
            Assert.NotNull(matchingScenario);
            Assert.Equal("low-severity", matchingScenario.Id);
        }
    }
}