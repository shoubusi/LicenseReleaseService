# Testing Strategy

### Testing Pyramid

This architecture implements a comprehensive testing pyramid approach specifically designed for the Windows Service testing framework, balancing test coverage with execution efficiency.

```
        E2E Tests (5%)
           /        \
    Integration Tests (15%)
      /                    \
Unit Tests (60%)       Backend Unit Tests (60%)
```

**Test Distribution Strategy:**
- **Unit Tests (60%)**: Fast, isolated tests for individual components and methods
- **Integration Tests (25%)**: Tests for component interactions and external dependencies
- **Performance Tests (10%)**: Benchmarks and load testing for performance validation
- **End-to-End Tests (5%)**: Complete workflow validation with real-world scenarios

### Test Organization

#### Frontend Tests
**Not applicable** - This is a backend-only testing framework with no frontend components.

#### Backend Tests

**Unit Tests Structure:**
```
LicenseReleaseService.Tests/Unit/
├── Configuration/
│   ├── ConfigurationManagerTests.cs
│   ├── ServiceSettingsTests.cs
│   ├── ConfigurationValidationTests.cs
│   └── ConfigurationHotReloadTests.cs
├── LicenseManagement/
│   ├── LicenseQueryEngineTests.cs
│   ├── LmutilLicenseManagerTests.cs
│   ├── LicenseParsingTests.cs
│   ├── LicenseCachingTests.cs
│   └── MockLicenseServerTests.cs
├── ProcessExecution/
│   ├── ProcessExecutorTests.cs
│   ├── ProcessMetricsTests.cs
│   ├── ProcessTimeoutTests.cs
│   └── MockProcessExecutorTests.cs
├── HealthMonitoring/
│   ├── HealthCheckerTests.cs
│   ├── RecoveryManagerTests.cs
│   ├── ServiceStateTests.cs
│   └── CircuitBreakerTests.cs
└── TestInfrastructure/
    ├── MockServiceManagerTests.cs
    ├── TestDataProviderTests.cs
    └── TestConfigurationManagerTests.cs
```

**Integration Tests Structure:**
```
LicenseReleaseService.Tests/Integration/
├── ServiceLifecycle/
│   ├── ServiceStartupTests.cs
│   ├── ServiceShutdownTests.cs
│   ├── ServicePauseResumeTests.cs
│   └── ServiceConfigurationTests.cs
├── LicenseWorkflows/
│   ├── EndToEndLicenseQueryTests.cs
│   ├── LicenseReleaseWorkflowTests.cs
│   ├── LicenseParsingIntegrationTests.cs
│   └── MultiVersionLicenseTests.cs
├── FileSystemIntegration/
│   ├── ConfigurationFileWatcherTests.cs
│   ├── FilePermissionTests.cs
│   ├── FileSystemErrorTests.cs
│   └── ConcurrentFileAccessTests.cs
└── ExternalSystemIntegration/
    ├── WindowsServiceIntegrationTests.cs
    ├── EventLogIntegrationTests.cs
    ├── PerformanceCounterTests.cs
    └── WindowsRegistryTests.cs
```

**Performance Tests Structure:**
```
LicenseReleaseService.Tests/Performance/
├── Benchmarks/
│   ├── LicenseQueryBenchmark.cs
│   ├── ConfigurationReloadBenchmark.cs
│   ├── ProcessExecutionBenchmark.cs
│   ├── MemoryUsageBenchmark.cs
│   └── StartupTimeBenchmark.cs
├── LoadTests/
│   ├── ConcurrentLicenseQueryTests.cs
│   ├── HighVolumeConfigurationTests.cs
│   ├── MemoryLeakTests.cs
│   ├── ResourceExhaustionTests.cs
│   └── LongRunningStabilityTests.cs
└── RegressionTests/
    ├── PerformanceRegressionTests.cs
    ├── MemoryUsageRegressionTests.cs
    ├── StartupTimeRegressionTests.cs
    └── ResourceLeakRegressionTests.cs
```

### Test Examples

#### Backend API Test

```csharp
[TestFixture]
public class LicenseQueryEngineTests
{
    private readonly ILicenseQueryEngine _queryEngine;
    private readonly Mock<IProcessExecutor> _mockProcessExecutor;
    private readonly Mock<ILmstatOutputParser> _mockParser;
    private readonly TestConfiguration _testConfig;

    public LicenseQueryEngineTests()
    {
        _mockProcessExecutor = new Mock<IProcessExecutor>();
        _mockParser = new Mock<ILmstatOutputParser>();
        _testConfig = new TestConfiguration
        {
            LicenseServer = "test-server",
            Port = 27000,
            TimeoutMs = 30000
        };

        _queryEngine = new LicenseQueryEngine(
            _mockProcessExecutor.Object,
            _mockParser.Object,
            _testConfig);
    }

    [Test]
    [Category("Unit")]
    [Description("Should successfully query license status with valid response")]
    public async Task QueryLicenses_ValidResponse_ReturnsLicenseInfo()
    {
        // Arrange
        var mockResponse = "Users of solidworks: (Total of 10 licenses issued; Total of 5 licenses in use)";
        var mockProcessResult = new ProcessResult
        {
            Success = true,
            Output = mockResponse,
            ExitCode = 0
        };

        var expectedLicenseInfo = new LicenseQueryResult
        {
            FeatureName = "solidworks",
            TotalLicenses = 10,
            UsedLicenses = 5,
            AvailableLicenses = 5,
            Users = new List<LicenseUser>
            {
                new LicenseUser { Username = "user1", Workstation = "workstation1" }
            }
        };

        _mockProcessExecutor
            .Setup(x => x.ExecuteAsync(It.Is<ProcessRequest>(req =>
                req.Arguments.Contains("lmstat") && req.Arguments.Contains("-a"))))
            .ReturnsAsync(mockProcessResult);

        _mockParser
            .Setup(x => x.ParseLmstatOutput(mockResponse, "test-server@27000"))
            .Returns(expectedLicenseInfo);

        // Act
        var result = await _queryEngine.QueryLicensesAsync(new LicenseQueryOptions());

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.FeatureName, Is.EqualTo("solidworks"));
        Assert.That(result.TotalLicenses, Is.EqualTo(10));
        Assert.That(result.UsedLicenses, Is.EqualTo(5));
        Assert.That(result.Users.Count, Is.EqualTo(1));

        _mockProcessExecutor.Verify(x => x.ExecuteAsync(It.IsAny<ProcessRequest>()), Times.Once);
        _mockParser.Verify(x => x.ParseLmstatOutput(mockResponse, "test-server@27000"), Times.Once);
    }

    [Test]
    [Category("Unit")]
    [Description("Should handle process execution failure gracefully")]
    public async Task QueryLicenses_ProcessFails_ThrowsLicenseQueryException()
    {
        // Arrange
        var mockProcessResult = new ProcessResult
        {
            Success = false,
            Error = "Process execution failed",
            ExitCode = 1
        };

        _mockProcessExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<ProcessRequest>()))
            .ReturnsAsync(mockProcessResult);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseQueryException>(
            () => _queryEngine.QueryLicensesAsync(new LicenseQueryOptions()));

        Assert.That(exception.Message, Does.Contain("Process execution failed"));
    }
}
```

#### E2E Test

```csharp
[TestFixture]
[Category("Integration")]
public class EndToEndLicenseManagementTests
{
    private readonly ITestServiceHost _testHost;
    private readonly ITestDataProvider _testDataProvider;
    private readonly IMockServiceManager _mockManager;

    public EndToEndLicenseManagementTests()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        _testHost = serviceProvider.GetRequiredService<ITestServiceHost>();
        _testDataProvider = serviceProvider.GetRequiredService<ITestDataProvider>();
        _mockManager = serviceProvider.GetRequiredService<IMockServiceManager>();
    }

    [Test]
    [Category("E2E")]
    [Description("Should complete full license query and release workflow")]
    public async Task FullLicenseWorkflow_CompleteWorkflow_Succeeds()
    {
        // Arrange
        var scenario = await _testDataProvider.GetTestScenarioAsync("end-to-end-license-workflow");

        // Configure mock services
        await _mockManager.ConfigureMockServiceAsync(new MockServiceConfig
        {
            ServiceType = "license-manager",
            ResponseFile = "mock-responses/license-query-success.json",
            Behavior = "always-success"
        });

        // Start test service host
        await _testHost.StartAsync();

        try
        {
            // Act - Execute complete workflow
            var licenseQuery = await _testHost.QueryLicensesAsync();
            Assert.That(licenseQuery, Is.Not.Null);
            Assert.That(licenseQuery.TotalLicenses, Is.GreaterThan(0));

            // Find a license to release
            var availableLicense = licenseQuery.Users.FirstOrDefault();
            if (availableLicense != null)
            {
                var releaseResult = await _testHost.ReleaseLicenseAsync(
                    availableLicense.FeatureName,
                    availableLicense.Username);

                Assert.That(releaseResult.Success, Is.True);

                // Verify license was actually released
                var updatedQuery = await _testHost.QueryLicensesAsync();
                Assert.That(updatedQuery.UsedLicenses, Is.EqualTo(licenseQuery.UsedLicenses - 1));
            }

            // Verify service health
            var healthReport = await _testHost.GetHealthReportAsync();
            Assert.That(healthReport.OverallStatus, Is.EqualTo(HealthStatus.Healthy));
        }
        finally
        {
            // Cleanup
            await _testHost.StopAsync();
            await _mockManager.ResetMockServicesAsync();
        }
    }

    [Test]
    [Category("E2E")]
    [Description("Should handle configuration hot-reload during service operation")]
    public async Task ConfigurationHotReload_DuringOperation_MaintainsServiceStability()
    {
        // Arrange
        await _testHost.StartAsync();

        try
        {
            // Get initial configuration
            var initialConfig = await _testHost.GetConfigurationAsync();
            var initialLogLevel = initialConfig.LogLevel;

            // Act - Trigger configuration change
            var newConfig = CreateModifiedConfiguration(initialConfig);
            await _testHost.UpdateConfigurationAsync(newConfig);

            // Wait for configuration reload
            await Task.Delay(1000);

            // Verify configuration was applied
            var updatedConfig = await _testHost.GetConfigurationAsync();
            Assert.That(updatedConfig.LogLevel, Is.Not.EqualTo(initialLogLevel));

            // Verify service is still healthy
            var healthReport = await _testHost.GetHealthReportAsync();
            Assert.That(healthReport.OverallStatus, Is.EqualTo(HealthStatus.Healthy));

            // Verify operations still work
            var licenseQuery = await _testHost.QueryLicensesAsync();
            Assert.That(licenseQuery, Is.Not.Null);
        }
        finally
        {
            await _testHost.StopAsync();
        }
    }

    private TestConfiguration CreateModifiedConfiguration(TestConfiguration original)
    {
        return new TestConfiguration
        {
            LogLevel = original.LogLevel == "Debug" ? "Information" : "Debug",
            LicenseServer = original.LicenseServer,
            MockServicesEnabled = original.MockServicesEnabled
        };
    }
}
```
