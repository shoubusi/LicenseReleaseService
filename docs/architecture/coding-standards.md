# Coding Standards

### Critical Fullstack Rules

- **Test-Driven Development**: All new functionality must include comprehensive tests before implementation
- **Mock-First Approach**: External dependencies must be mocked for unit testing before real integration
- **Async/Await Consistency**: All I/O operations must use async/await patterns consistently
- **Dependency Injection**: All components must use dependency injection for testability and maintainability
- **Configuration Management**: All configuration must be externalized and never hardcoded
- **Error Handling**: All operations must include comprehensive error handling and logging
- **Resource Cleanup**: All IDisposable resources must be properly disposed using using statements
- **Performance Monitoring**: All operations must include performance monitoring and metrics collection

### Naming Conventions

| Element | Frontend | Backend | Example |
|---------|----------|---------|---------|
| **Test Classes** | N/A | `[ClassName]Tests` | `LicenseQueryEngineTests.cs` |
| **Test Methods** | N/A | `[MethodName]_[Scenario]_[ExpectedResult]` | `QueryLicenses_ValidResponse_ReturnsLicenseInfo()` |
| **Mock Classes** | N/A | `Mock[InterfaceName]` | `MockILicenseManager.cs` |
| **Test Data** | N/A | `[Feature]_[Scenario]_[Data]` | `LicenseQuery_SuccessResponse.json` |
| **Test Fixtures** | N/A | `[ComponentName]TestFixture` | `LicenseManagementTestFixture.cs` |
| **Configuration** | N/A | `[ComponentName]Configuration` | `TestExecutionConfiguration.cs` |
| **Test Scenarios** | N/A | `[Feature]TestScenario` | `LicenseQueryTestScenario.cs` |

### Code Quality Standards

```csharp
// 1. Always use async/await for I/O operations
public async Task<LicenseQueryResult> QueryLicensesAsync(LicenseQueryOptions options)
{
    try
    {
        _logger.LogInformation("Querying licenses with options: {@Options}", options);

        var processResult = await _processExecutor.ExecuteAsync(CreateProcessRequest(options));

        if (!processResult.Success)
        {
            throw new LicenseQueryException($"License query failed: {processResult.Error}");
        }

        var result = _outputParser.ParseLmstatOutput(processResult.Output, options.ServerAddress);
        _logger.LogInformation("License query completed successfully. Total: {Total}, Used: {Used}",
            result.TotalLicenses, result.UsedLicenses);

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "License query failed unexpectedly");
        throw;
    }
}

// 2. Always use dependency injection
public class LicenseQueryEngine : ILicenseQueryEngine
{
    private readonly IProcessExecutor _processExecutor;
    private readonly ILmstatOutputParser _outputParser;
    private readonly ILogger<LicenseQueryEngine> _logger;
    private readonly IMemoryCache _cache;

    public LicenseQueryEngine(
        IProcessExecutor processExecutor,
        ILmstatOutputParser outputParser,
        ILogger<LicenseQueryEngine> logger,
        IMemoryCache cache)
    {
        _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
        _outputParser = outputParser ?? throw new ArgumentNullException(nameof(outputParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
}

// 3. Always use proper resource disposal
public async Task<TestExecutionResult> ExecuteTestAsync(TestScenario scenario)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    using var testHost = await CreateTestHostAsync(scenario);

    try
    {
        await testHost.StartAsync();

        var result = await ExecuteTestStepsAsync(scenario, testHost, cts.Token);

        _logger.LogInformation("Test {TestId} completed with result: {Outcome}",
            scenario.TestId, result.Outcome);

        return result;
    }
    finally
    {
        await testHost.StopAsync();
        await CleanupTestEnvironmentAsync(scenario);
    }
}

// 4. Always validate input parameters
public class TestConfigurationManager
{
    public TestConfiguration LoadConfiguration(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Configuration path cannot be null or empty", nameof(configPath));
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<TestConfiguration>(json);

            return ValidateConfiguration(config) ? config : throw new ConfigurationValidationException();
        }
        catch (JsonException ex)
        {
            throw new ConfigurationParseException($"Invalid JSON in configuration file: {configPath}", ex);
        }
    }

    private bool ValidateConfiguration(TestConfiguration config)
    {
        return config != null &&
               !string.IsNullOrWhiteSpace(config.LicenseServer) &&
               config.Port > 0 && config.Port <= 65535 &&
               config.TimeoutMs > 0;
    }
}

// 5. Always include comprehensive logging
public class TestExecutionEngine
{
    private readonly ILogger<TestExecutionEngine> _logger;

    public async Task<TestSuiteResult> ExecuteTestSuiteAsync(TestSuite suite)
    {
        _logger.LogInformation("Starting test suite execution: {SuiteId} with {TestCount} tests",
            suite.SuiteId, suite.Tests.Count);

        var stopwatch = Stopwatch.StartNew();
        var results = new List<TestExecutionResult>();

        foreach (var test in suite.Tests)
        {
            _logger.LogDebug("Executing test: {TestId}", test.TestId);

            try
            {
                var result = await ExecuteTestAsync(test);
                results.Add(result);

                if (result.Outcome == TestOutcome.Passed)
                {
                    _logger.LogDebug("Test passed: {TestId} in {Duration}ms",
                        test.TestId, result.DurationMs);
                }
                else
                {
                    _logger.LogWarning("Test failed: {TestId} - {Error}",
                        test.TestId, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test execution failed: {TestId}", test.TestId);
                results.Add(new TestExecutionResult
                {
                    TestId = test.TestId,
                    Outcome = TestOutcome.Failed,
                    ErrorMessage = ex.Message,
                    DurationMs = stopwatch.ElapsedMilliseconds
                });
            }
        }

        stopwatch.Stop();

        var suiteResult = new TestSuiteResult
        {
            SuiteId = suite.SuiteId,
            TestResults = results,
            TotalDurationMs = stopwatch.ElapsedMilliseconds,
            PassedTests = results.Count(r => r.Outcome == TestOutcome.Passed),
            FailedTests = results.Count(r => r.Outcome == TestOutcome.Failed)
        };

        _logger.LogInformation("Test suite completed: {SuiteId} - Passed: {Passed}, Failed: {Failed}, Duration: {Duration}ms",
            suiteResult.SuiteId, suiteResult.PassedTests, suiteResult.FailedTests, suiteResult.TotalDurationMs);

        return suiteResult;
    }
}
```

### Test Organization Standards

```csharp
// Test fixture organization
[TestFixture]
[Category("Unit")]
[Category("LicenseManagement")]
[Description("Tests for LicenseQueryEngine functionality")]
public class LicenseQueryEngineTests
{
    private readonly ILicenseQueryEngine _queryEngine;
    private readonly Mock<IProcessExecutor> _mockProcessExecutor;
    private readonly Mock<ILmstatOutputParser> _mockParser;

    public LicenseQueryEngineTests()
    {
        // Setup mock dependencies
        _mockProcessExecutor = new Mock<IProcessExecutor>();
        _mockParser = new Mock<ILmstatOutputParser>();

        // Create test instance
        _queryEngine = new LicenseQueryEngine(
            _mockProcessExecutor.Object,
            _mockParser.Object,
            Mock.Of<ILogger<LicenseQueryEngine>>(),
            Mock.Of<IMemoryCache>());
    }

    [SetUp]
    public void SetUp()
    {
        // Reset mock calls before each test
        _mockProcessExecutor.Reset();
        _mockParser.Reset();
    }

    [Test]
    [Category("HappyPath")]
    [Description("Should successfully query license status with valid response")]
    public async Task QueryLicenses_ValidResponse_ReturnsLicenseInfo()
    {
        // Test implementation
    }

    [Test]
    [Category("ErrorHandling")]
    [Description("Should handle process execution failure gracefully")]
    public async Task QueryLicenses_ProcessFails_ThrowsLicenseQueryException()
    {
        // Test implementation
    }
}
```
