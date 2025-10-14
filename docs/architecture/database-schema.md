# Database Schema

Based on the testing framework requirements and the existing service's file-based approach, the architecture primarily uses **file-based data storage** rather than traditional databases. However, the testing framework implements structured data schemas for test data, configurations, and results.

### Test Data Storage Schema

Since the testing framework maintains consistency with the existing service's file-based architecture, all data is stored in structured file formats (JSON/XML) rather than relational databases. This approach simplifies deployment and maintains compatibility with the existing service patterns.

**Test Scenario Data Schema:**
```json
{
  "scenarioId": "license-query-success-2023",
  "name": "License Query Success Scenario",
  "description": "Test successful license query with available licenses",
  "solidWorksVersion": "2023",
  "licenseServerResponse": "Users of solidworks: (Total of 10 licenses issued; Total of 5 licenses in use)\n\"user1\" workstation1 (v2023.0) (start/hostname1)",
  "expectedResults": {
    "totalLicenses": 10,
    "usedLicenses": 5,
    "availableLicenses": 5,
    "users": [
      {
        "username": "user1",
        "workstation": "workstation1",
        "version": "2023.0",
        "startTime": "start/hostname1"
      }
    ]
  },
  "testConfiguration": {
    "timeoutMs": 30000,
    "expectedDurationMs": 5000,
    "mockBehavior": "always-success"
  }
}
```

**Test Configuration Schema:**
```json
{
  "configurationId": "integration-test-config",
  "configType": "integration",
  "licenseServer": {
    "server": "test-server",
    "port": 27000,
    "lmutilPath": "C:\\TestTools\\lmutil.exe"
  },
  "mockServices": {
    "licenseManager": {
      "enabled": true,
      "responseFile": "mock-responses/license-server-success.json",
      "behavior": "always-success",
      "latencyMs": 100
    },
    "fileSystem": {
      "enabled": true,
      "mockFiles": [
        {
          "path": "app.config",
          "content": "<?xml version=\"1.0\" encoding=\"utf-8\"?>...",
          "permissions": "read-write"
        }
      ]
    }
  },
  "logging": {
    "level": "debug",
    "enableFileLogging": true,
    "enableConsoleLogging": true
  }
}
```

**Test Results Schema:**
```json
{
  "executionId": "exec-20250113-001",
  "testSuiteId": "license-management-suite",
  "startTime": "2025-01-13T10:00:00Z",
  "endTime": "2025-01-13T10:05:30Z",
  "outcome": "passed",
  "summary": {
    "totalTests": 45,
    "passedTests": 43,
    "failedTests": 2,
    "skippedTests": 0
  },
  "performanceMetrics": {
    "totalDurationMs": 330000,
    "averageTestDurationMs": 7333,
    "maxMemoryUsedMB": 256,
    "cpuUsagePercent": 15
  },
  "testResults": [
    {
      "testId": "license-query-basic",
      "scenarioId": "license-query-success-2023",
      "outcome": "passed",
      "durationMs": 2340,
      "metrics": {
        "memoryUsedMB": 45,
        "cpuUsagePercent": 8
      },
      "assertions": [
        {
          "type": "license-count",
          "expected": 10,
          "actual": 10,
          "passed": true
        }
      ]
    }
  ]
}
```

**Performance Benchmark Schema:**
```json
{
  "benchmarkId": "license-query-performance",
  "operation": "license-query",
  "baselineMetrics": {
    "meanMs": 5000,
    "percentile95": 8000,
    "memoryBaselineMB": 64
  },
  "currentResults": {
    "executionDate": "2025-01-13T10:00:00Z",
    "iterations": 1000,
    "meanMs": 5200,
    "percentile95": 8300,
    "memoryUsedMB": 68,
    "passedThreshold": true
  },
  "thresholds": {
    "maxMeanMs": 6000,
    "maxPercentile95": 10000,
    "maxMemoryMB": 128,
    "maxVariancePercent": 20
  }
}
```

### File Organization Structure

The testing framework uses a structured file organization to maintain data integrity and enable efficient access:

```
LicenseReleaseService.Tests/
├── TestData/
│   ├── Scenarios/
│   │   ├── LicenseQueries/
│   │   │   ├── license-query-success-2023.json
│   │   │   ├── license-query-exhausted.json
│   │   │   └── license-query-timeout.json
│   │   ├── Configurations/
│   │   │   ├── unit-test-config.json
│   │   │   ├── integration-test-config.json
│   │   │   └── performance-test-config.json
│   │   └── MockResponses/
│   │       ├── license-server-success.json
│   │       ├── license-server-error.json
│   │       └── license-server-timeout.json
│   ├── Configurations/
│   │   ├── nunit.test.config
│   │   ├── performance.benchmark.config
│   │   └── mock-services.config
│   └── ExpectedResults/
│       ├── license-query-expected.json
│       └── configuration-reload-expected.json
├── TestResults/
│   ├── 2025-01-13/
│   │   ├── test-execution-001.json
│   │   ├── performance-benchmark-001.json
│   │   └── coverage-report-001.xml
│   └── Historical/
└── Reports/
    ├── Daily/
    ├── Weekly/
    └── Monthly/
```

### Data Access Patterns

**Test Data Provider Interface:**
```csharp
public interface ITestDataProvider
{
    // Test scenario access
    Task<LicenseTestScenario> GetTestScenarioAsync(string scenarioId);
    Task<LicenseTestScenario[]> GetTestScenariosByCategoryAsync(string category);
    Task SaveTestScenarioAsync(LicenseTestScenario scenario);

    // Configuration access
    Task<TestConfiguration> GetTestConfigurationAsync(string configId);
    Task<TestConfiguration[]> GetAllTestConfigurationsAsync();

    // Mock response access
    Task<MockResponse> GetMockResponseAsync(string responseId);
    Task<MockResponse[]> GetMockResponsesByServiceAsync(string serviceType);

    // Benchmark data access
    Task<PerformanceBenchmark> GetBenchmarkAsync(string benchmarkId);
    Task SaveBenchmarkResultAsync(BenchmarkResult result);
}
```

**File-Based Data Access Implementation:**
```csharp
public class FileBasedTestDataProvider : ITestDataProvider
{
    private readonly string _testDataPath;
    private readonly ILogger<FileBasedTestDataProvider> _logger;

    public async Task<LicenseTestScenario> GetTestScenarioAsync(string scenarioId)
    {
        var filePath = Path.Combine(_testDataPath, "Scenarios", "LicenseQueries", $"{scenarioId}.json");
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<LicenseTestScenario>(json);
    }

    public async Task SaveTestScenarioAsync(LicenseTestScenario scenario)
    {
        var filePath = Path.Combine(_testDataPath, "Scenarios", "LicenseQueries", $"{scenario.ScenarioId}.json");
        var json = JsonSerializer.Serialize(scenario, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
}
```
