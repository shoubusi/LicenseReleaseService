using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    /// <summary>
    /// Unit tests for ServiceResilienceCoordinator class
    /// </summary>
    public class ServiceResilienceCoordinatorTests : IDisposable
    {
        private readonly ServiceResilienceCoordinator _coordinator;
        private readonly ILogger<ServiceResilienceCoordinatorTests> _logger;

        public ServiceResilienceCoordinatorTests()
        {
            _logger = new NullLogger<ServiceResilienceCoordinatorTests>();
            _coordinator = new ServiceResilienceCoordinator(_logger);
        }

        public void Dispose()
        {
            _coordinator?.Dispose();
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            using var coordinator = new ServiceResilienceCoordinator(new NullLogger<ServiceResilienceCoordinator>());

            // Assert
            Assert.NotNull(coordinator);
            Assert.Equal(0, coordinator.ActivePolicies.Count);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceResilienceCoordinator(null!));
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithSuccessfulOperation_ShouldExecuteSuccessfully()
        {
            // Arrange
            var operationResult = "success";
            var executed = false;

            // Act
            var result = await _coordinator.ExecuteWithResilienceAsync<string>(
                "test_operation",
                async () =>
                {
                    executed = true;
                    await Task.Delay(10);
                    return operationResult;
                },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(executed);
            Assert.Equal(operationResult, result);
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithFailedOperation_ShouldPropagateException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test failure");

            // Act & Assert
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _coordinator.ExecuteWithResilienceAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        await Task.Delay(10);
                        throw expectedException;
                    },
                    cancellationToken: CancellationToken.None);
            });

            Assert.Same(expectedException, actualException);
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithCircuitBreakerPolicy_ShouldApplyCircuitBreaker()
        {
            // Arrange
            var circuitBreakerPolicy = new ResiliencePolicy
            {
                Name = "test_circuit_breaker",
                PolicyType = ResiliencePolicyType.CircuitBreaker,
                IsEnabled = true,
                CircuitBreakerSettings = new CircuitBreakerSettings
                {
                    FailureThreshold = 2,
                    RecoveryTimeout = TimeSpan.FromMilliseconds(100),
                    Timeout = TimeSpan.FromSeconds(30)
                }
            };

            _coordinator.AddResiliencePolicy(circuitBreakerPolicy);

            // Act - Cause failures to trip the circuit breaker
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    await _coordinator.ExecuteWithResilienceAsync<string>(
                        "test_operation",
                        async () =>
                        {
                            await Task.Delay(10);
                            throw new InvalidOperationException("Test failure");
                        },
                        cancellationToken: CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }
            }

            // Assert - Circuit should be open now
            await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
            {
                await _coordinator.ExecuteWithResilienceAsync<string>(
                    "test_operation",
                    async () => "should_not_execute",
                    cancellationToken: CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithRetryPolicy_ShouldApplyRetry()
        {
            // Arrange
            var retryCount = 0;
            var retryPolicy = new ResiliencePolicy
            {
                Name = "test_retry",
                PolicyType = ResiliencePolicyType.Retry,
                IsEnabled = true,
                RetrySettings = new RetrySettings
                {
                    MaxRetries = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(50),
                    RetryType = RetryType.ExponentialBackoff
                }
            };

            _coordinator.AddResiliencePolicy(retryPolicy);

            // Act
            var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _coordinator.ExecuteWithResilienceAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        retryCount++;
                        await Task.Delay(10);
                        throw new InvalidOperationException("Retryable failure");
                    },
                    cancellationToken: CancellationToken.None);
            });

            // Assert
            Assert.Equal(4, retryCount); // Initial attempt + 3 retries
            Assert.Equal("Retryable failure", actualException.Message);
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithTimeoutPolicy_ShouldApplyTimeout()
        {
            // Arrange
            var timeoutPolicy = new ResiliencePolicy
            {
                Name = "test_timeout",
                PolicyType = ResiliencePolicyType.Timeout,
                IsEnabled = true,
                TimeoutSettings = new TimeoutSettings
                {
                    Timeout = TimeSpan.FromMilliseconds(50)
                }
            };

            _coordinator.AddResiliencePolicy(timeoutPolicy);

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await _coordinator.ExecuteWithResilienceAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        await Task.Delay(200); // Longer than timeout
                        return "success";
                    },
                    cancellationToken: CancellationToken.None);
            });
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithMultiplePolicies_ShouldApplyAllPolicies()
        {
            // Arrange
            var executionCount = 0;

            var retryPolicy = new ResiliencePolicy
            {
                Name = "test_retry",
                PolicyType = ResiliencePolicyType.Retry,
                IsEnabled = true,
                RetrySettings = new RetrySettings
                {
                    MaxRetries = 1,
                    BaseDelay = TimeSpan.FromMilliseconds(10),
                    RetryType = RetryType.Fixed
                }
            };

            var timeoutPolicy = new ResiliencePolicy
            {
                Name = "test_timeout",
                PolicyType = ResiliencePolicyType.Timeout,
                IsEnabled = true,
                TimeoutSettings = new TimeoutSettings
                {
                    Timeout = TimeSpan.FromMilliseconds(100)
                }
            };

            _coordinator.AddResiliencePolicy(retryPolicy);
            _coordinator.AddResiliencePolicy(timeoutPolicy);

            // Act
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await _coordinator.ExecuteWithResilienceAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        executionCount++;
                        await Task.Delay(200); // Longer than timeout
                        return "success";
                    },
                    cancellationToken: CancellationToken.None);
            });

            // Assert
            Assert.Equal(2, executionCount); // Initial attempt + 1 retry
        }

        [Fact]
        public void AddResiliencePolicy_WithValidPolicy_ShouldAddPolicy()
        {
            // Arrange
            var policy = new ResiliencePolicy
            {
                Name = "test_policy",
                PolicyType = ResiliencePolicyType.CircuitBreaker,
                IsEnabled = true,
                CircuitBreakerSettings = new CircuitBreakerSettings
                {
                    FailureThreshold = 3,
                    RecoveryTimeout = TimeSpan.FromMinutes(1),
                    Timeout = TimeSpan.FromSeconds(30)
                }
            };

            // Act
            _coordinator.AddResiliencePolicy(policy);

            // Assert
            Assert.Contains(policy, _coordinator.ActivePolicies);
        }

        [Fact]
        public void AddResiliencePolicy_WithNullPolicy_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _coordinator.AddResiliencePolicy(null!));
        }

        [Fact]
        public void RemoveResiliencePolicy_WithExistingPolicy_ShouldRemovePolicy()
        {
            // Arrange
            var policy = new ResiliencePolicy
            {
                Name = "test_policy",
                PolicyType = ResiliencePolicyType.CircuitBreaker,
                IsEnabled = true,
                CircuitBreakerSettings = new CircuitBreakerSettings
                {
                    FailureThreshold = 3,
                    RecoveryTimeout = TimeSpan.FromMinutes(1),
                    Timeout = TimeSpan.FromSeconds(30)
                }
            };

            _coordinator.AddResiliencePolicy(policy);
            Assert.Contains(policy, _coordinator.ActivePolicies);

            // Act
            _coordinator.RemoveResiliencePolicy(policy);

            // Assert
            Assert.DoesNotContain(policy, _coordinator.ActivePolicies);
        }

        [Fact]
        public void RemoveResiliencePolicy_WithNonExistingPolicy_ShouldNotThrow()
        {
            // Arrange
            var policy = new ResiliencePolicy
            {
                Name = "test_policy",
                PolicyType = ResiliencePolicyType.CircuitBreaker,
                IsEnabled = true,
                CircuitBreakerSettings = new CircuitBreakerSettings
                {
                    FailureThreshold = 3,
                    RecoveryTimeout = TimeSpan.FromMinutes(1),
                    Timeout = TimeSpan.FromSeconds(30)
                }
            };

            // Act & Assert
            var exception = Record.Exception(() => _coordinator.RemoveResiliencePolicy(policy));
            Assert.Null(exception);
        }

        [Fact]
        public void RemoveResiliencePolicy_WithNullPolicy_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _coordinator.RemoveResiliencePolicy(null!));
        }

        [Fact]
        public async Task GetResilienceMetricsAsync_ShouldReturnCurrentMetrics()
        {
            // Arrange
            var policy = new ResiliencePolicy
            {
                Name = "test_policy",
                PolicyType = ResiliencePolicyType.Retry,
                IsEnabled = true,
                RetrySettings = new RetrySettings
                {
                    MaxRetries = 2,
                    BaseDelay = TimeSpan.FromMilliseconds(10),
                    RetryType = RetryType.Fixed
                }
            };

            _coordinator.AddResiliencePolicy(policy);

            // Execute some operations to generate metrics
            try
            {
                await _coordinator.ExecuteWithResilienceAsync<string>(
                    "test_operation",
                    async () =>
                    {
                        await Task.Delay(10);
                        return "success";
                    },
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                // Ignore any exceptions for this test
            }

            // Act
            var metrics = await _coordinator.GetResilienceMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Single(metrics.PolicyMetrics);
            Assert.Contains("test_policy", metrics.PolicyMetrics.Keys);
            Assert.NotNull(metrics.LastUpdated);
        }

        [Fact]
        public async Task GetResilienceMetricsAsync_WithNoPolicies_ShouldReturnEmptyMetrics()
        {
            // Arrange - No policies added

            // Act
            var metrics = await _coordinator.GetResilienceMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Empty(metrics.PolicyMetrics);
            Assert.NotNull(metrics.LastUpdated);
        }

        [Fact]
        public async Task ClearAllResiliencePoliciesAsync_ShouldRemoveAllPolicies()
        {
            // Arrange
            var policy1 = new ResiliencePolicy
            {
                Name = "policy1",
                PolicyType = ResiliencePolicyType.CircuitBreaker,
                IsEnabled = true,
                CircuitBreakerSettings = new CircuitBreakerSettings
                {
                    FailureThreshold = 3,
                    RecoveryTimeout = TimeSpan.FromMinutes(1),
                    Timeout = TimeSpan.FromSeconds(30)
                }
            };

            var policy2 = new ResiliencePolicy
            {
                Name = "policy2",
                PolicyType = ResiliencePolicyType.Retry,
                IsEnabled = true,
                RetrySettings = new RetrySettings
                {
                    MaxRetries = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(100),
                    RetryType = RetryType.ExponentialBackoff
                }
            };

            _coordinator.AddResiliencePolicy(policy1);
            _coordinator.AddResiliencePolicy(policy2);
            Assert.Equal(2, _coordinator.ActivePolicies.Count);

            // Act
            await _coordinator.ClearAllResiliencePoliciesAsync();

            // Assert
            Assert.Empty(_coordinator.ActivePolicies);
        }

        [Fact]
        public void ResiliencePolicy_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var policy = new ResiliencePolicy
            {
                Name = "test_policy",
                PolicyType = ResiliencePolicyType.CircuitBreaker,
                IsEnabled = true,
                CircuitBreakerSettings = new CircuitBreakerSettings
                {
                    FailureThreshold = 3,
                    RecoveryTimeout = TimeSpan.FromMinutes(1),
                    Timeout = TimeSpan.FromSeconds(30)
                },
                TimeoutSettings = new TimeoutSettings
                {
                    Timeout = TimeSpan.FromSeconds(30)
                },
                RetrySettings = new RetrySettings
                {
                    MaxRetries = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(100),
                    RetryType = RetryType.ExponentialBackoff
                },
                Description = "Test resilience policy"
            };

            // Assert
            Assert.Equal("test_policy", policy.Name);
            Assert.Equal(ResiliencePolicyType.CircuitBreaker, policy.PolicyType);
            Assert.True(policy.IsEnabled);
            Assert.NotNull(policy.CircuitBreakerSettings);
            Assert.NotNull(policy.TimeoutSettings);
            Assert.NotNull(policy.RetrySettings);
            Assert.Equal("Test resilience policy", policy.Description);
        }

        [Fact]
        public void ResilienceMetrics_ToString_ShouldReturnMeaningfulString()
        {
            // Arrange
            var metrics = new ResilienceMetrics
            {
                PolicyMetrics = new Dictionary<string, PolicyMetrics>
                {
                    ["test_policy"] = new PolicyMetrics
                    {
                        TotalExecutions = 100,
                        SuccessfulExecutions = 95,
                        FailedExecutions = 5,
                        AverageExecutionTime = TimeSpan.FromMilliseconds(50),
                        LastExecutionTime = DateTime.Now
                    }
                },
                LastUpdated = DateTime.Now
            };

            // Act
            var result = metrics.ToString();

            // Assert
            Assert.Contains("PolicyCount=1", result);
            Assert.Contains("LastUpdated=", result);
        }

        [Fact]
        public void PolicyMetrics_ToString_ShouldReturnMeaningfulString()
        {
            // Arrange
            var policyMetrics = new PolicyMetrics
            {
                TotalExecutions = 100,
                SuccessfulExecutions = 95,
                FailedExecutions = 5,
                AverageExecutionTime = TimeSpan.FromMilliseconds(50),
                LastExecutionTime = DateTime.Now
            };

            // Act
            var result = policyMetrics.ToString();

            // Assert
            Assert.Contains("Total=100", result);
            Assert.Contains("Success=95", result);
            Assert.Contains("Failed=5", result);
            Assert.Contains("SuccessRate=95.00%", result);
            Assert.Contains("AvgTime=50ms", result);
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithCancellation_ShouldCancelOperation()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var operationStarted = new TaskCompletionSource<bool>();

            // Act
            var operationTask = _coordinator.ExecuteWithResilienceAsync<string>(
                "test_operation",
                async () =>
                {
                    operationStarted.SetResult(true);
                    await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
                    return "success";
                },
                cancellationToken: cts.Token);

            // Wait for operation to start
            await operationStarted.Task;
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await operationTask);
        }

        [Fact]
        public async Task ExecuteWithResilienceAsync_WithDisabledPolicy_ShouldNotApplyPolicy()
        {
            // Arrange
            var disabledPolicy = new ResiliencePolicy
            {
                Name = "disabled_policy",
                PolicyType = ResiliencePolicyType.Timeout,
                IsEnabled = false, // Disabled
                TimeoutSettings = new TimeoutSettings
                {
                    Timeout = TimeSpan.FromMilliseconds(50)
                }
            };

            _coordinator.AddResiliencePolicy(disabledPolicy);

            // Act - Should succeed even though it takes longer than the disabled timeout
            var result = await _coordinator.ExecuteWithResilienceAsync<string>(
                "test_operation",
                async () =>
                {
                    await Task.Delay(200); // Longer than disabled timeout
                    return "success";
                },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal("success", result);
        }
    }
}