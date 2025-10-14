# Core Workflows

These sequence diagrams illustrate the key system workflows for the testing framework, showing component interactions including external API integration, test execution flows, and error handling paths.

### Test Execution Workflow

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant TEE as TestExecutionEngine
    participant TDP as TestDataProvider
    participant MSM as MockServiceManager
    participant CM as ConfigurationManager
    participant LS as LicenseService
    participant RS as ReportingService

    Dev->>TEE: ExecuteTestSuite("license-management")
    TEE->>TDP: GetTestSuite("license-management")
    TDP-->>TEE: TestSuite with scenarios

    TEE->>CM: LoadTestConfiguration("integration")
    CM-->>TEE: TestConfiguration loaded

    TEE->>MSM: ConfigureMockServices(scenario)
    MSM->>MSM: Setup mock license server
    MSM->>MSM: Setup mock file system
    MSM-->>TEE: Mock services configured

    TEE->>LS: Initialize service with test configuration
    LS-->>TEE: Service ready for testing

    loop Each Test Scenario
        TEE->>TEE: Setup test environment
        TEE->>LS: Execute license operations
        LS->>MSM: Call mock license server
        MSM-->>LS: Mock response
        LS-->>TEE: Operation results
        TEE->>TEE: Validate results against expectations
        TEE->>RS: Record test results
    end

    TEE->>TEE: Cleanup test environment
    TEE->>MSM: Reset mock services
    TEE->>RS: GenerateTestReport(suiteResults)
    RS-->>TEE: Comprehensive test report
    TEE-->>Dev: Test execution complete with results
```

### License Management Test Workflow

```mermaid
sequenceDiagram
    participant Test as TestFramework
    participant Mock as MockLicenseServer
    participant LS as LicenseService
    participant Proc as ProcessExecutor
    participant Config as ConfigurationManager
    participant Logger as LoggingService

    Test->>Config: LoadTestConfiguration()
    Config-->>Test: Test configuration loaded

    Test->>Mock: ConfigureLicenseResponse("solidworks", available_licenses)
    Mock-->>Test: Mock response configured

    Test->>LS: QueryLicensesAsync(new LicenseQueryOptions())
    LS->>Config: GetLicenseServerConfiguration()
    Config-->>LS: Server configuration

    LS->>Proc: ExecuteAsync("lmutil.exe", "lmstat -c server@27000 -a")
    Proc->>Mock: Execute mock lmutil process
    Mock-->>Proc: Mock lmstat output
    Proc-->>LS: ProcessResult with license data

    LS->>LS: ParseLicenseData(output)
    LS->>Logger: LogLicenseQuery(results)
    LS-->>Test: LicenseQueryResult

    Test->>Test: ValidateResults(expected, actual)

    alt Test passes validation
        Test->>Logger: LogTestSuccess(scenario)
    else Test fails validation
        Test->>Logger: LogTestFailure(scenario, differences)
    end

    Test-->>Test: TestExecutionResult
```

### Configuration Hot-Reload Test Workflow

```mermaid
sequenceDiagram
    participant Test as TestFramework
    participant MockFS as MockFileSystem
    participant CM as ConfigurationManager
    participant LS as LicenseService
    participant WHL as WatcherHandler

    Test->>MockFS: CreateMockFile("app.config", initial_config)
    MockFS-->>Test: Mock file created

    Test->>CM: LoadConfigurationAsync("app.config")
    CM->>MockFS: ReadFile("app.config")
    MockFS-->>CM: Configuration content
    CM-->>Test: Configuration loaded

    Test->>LS: StartService()
    LS->>CM: InitializeConfiguration()
    CM->>CM: StartFileWatcher()
    CM->>WHL: RegisterFileChangeHandler()

    Test->>MockFS: SimulateFileChange("app.config", modified_config)
    MockFS->>WHL: TriggerFileChangedEvent()
    WHL->>CM: HandleConfigurationFileChanged()
    CM->>MockFS: ReadFile("app.config")
    MockFS-->>CM: Modified configuration content
    CM->>CM: ValidateNewConfiguration()
    CM->>LS: OnConfigurationChanged(newConfig)
    LS->>LS: ApplyConfigurationChanges()

    alt Configuration change successful
        LS->>Test: ConfigurationUpdated successfully
        Test->>Test: ValidateServiceBehavior(expectedChanges)
    else Configuration change fails
        LS->>Test: ConfigurationUpdate failed
        Test->>Test: ValidateErrorHandling(errorScenario)
    end

    Test-->>Test: HotReloadTestResult
```

### Performance Benchmarking Workflow

```mermaid
sequenceDiagram
    participant Perf as PerformanceFramework
    participant BM as BenchmarkRunner
    participant LS as LicenseService
    participant Mock as MockServices
    participant Monitor as PerformanceMonitor

    Perf->>BM: InitializeBenchmark("license-query-performance")
    BM->>Mock: ConfigureHighPerformanceMocks()
    Mock-->>BM: Mock services optimized for performance

    BM->>Monitor: StartPerformanceMonitoring()
    Monitor-->>BM: Monitoring active

    loop Benchmark iterations (e.g., 1000 iterations)
        BM->>LS: QueryLicensesAsync(options)
        LS->>Mock: Execute optimized license query
        Mock-->>LS: Fast mock response
        LS-->>BM: Query result
        BM->>Monitor: RecordIterationMetrics()
    end

    BM->>Monitor: StopPerformanceMonitoring()
    Monitor-->>BM: Complete performance data

    BM->>BM: CalculateStatistics(perfData)
    BM->>BM: CompareWithBaseline(baselineMetrics)

    alt Performance within acceptable range
        BM->>Perf: BenchmarkPassed(results)
    else Performance degradation detected
        BM->>Perf: BenchmarkFailed(results, degradation)
    end

    Perf-->>Perf: BenchmarkExecutionResult
```

### Error Recovery and Resilience Test Workflow

```mermaid
sequenceDiagram
    participant Test as TestFramework
    participant LS as LicenseService
    participant RM as RecoveryManager
    participant Mock as MockServices
    participant Logger as LoggingService
    participant HC as HealthChecker

    Test->>Mock: ConfigureFailureScenario("license-server-timeout")
    Mock-->>Test: Failure scenario configured

    Test->>LS: QueryLicensesAsync(options)
    LS->>Mock: Execute license server call
    Mock-->>LS: TimeoutException (simulated)
    LS->>RM: HandleException(timeoutException)

    RM->>HC: CheckComponentHealth("license-manager")
    HC-->>RM: ComponentStatus: Degraded

    RM->>RM: DetermineRecoveryStrategy(exception)
    RM->>Logger: LogErrorWithRecoveryAttempt(exception, strategy)

    alt Retry strategy selected
        RM->>LS: ExecuteWithRetry(operation, retryCount)
        LS->>Mock: Retry license server call
        Mock-->>LS: Success response (after retry)
        LS-->>RM: Operation successful
        RM->>HC: UpdateComponentHealth("license-manager", Healthy)
    else Circuit breaker activated
        RM->>LS: ActivateCircuitBreaker("license-manager")
        LS-->>Test: CircuitBreakerException
        Test->>Test: ValidateCircuitBreakerBehavior()
    end

    RM->>Logger: LogRecoveryCompletion(result)
    Test-->>Test: ErrorRecoveryTestResult
```
