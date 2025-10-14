# Data Models

Based on the PRD requirements and existing service analysis, the testing framework requires several core data models to support comprehensive testing of the License Release Service. These models will be shared between test components and mock environments to ensure consistent test scenarios.

### LicenseTestScenario

**Purpose:** Defines test scenarios for license management operations including query, parsing, and release workflows. This model encapsulates the input conditions and expected outcomes for testing license-related functionality.

**Key Attributes:**
- **ScenarioId**: string - Unique identifier for the test scenario
- **Name**: string - Human-readable name for the test scenario
- **Description**: string - Detailed description of what the scenario tests
- **SolidWorksVersion**: string - Target SolidWorks version (2020-2025)
- **LicenseServerResponse**: string - Mock lmstat output for testing
- **ExpectedLicenses**: LicenseInfo[] - Expected parsed license information
- **ExpectedActions**: LicenseAction[] - Expected license release actions

#### TypeScript Interface

```typescript
interface LicenseTestScenario {
  scenarioId: string;
  name: string;
  description: string;
  solidWorksVersion: string;
  licenseServerResponse: string;
  expectedLicenses: LicenseInfo[];
  expectedActions: LicenseAction[];
}

interface LicenseInfo {
  featureName: string;
  totalLicenses: number;
  usedLicenses: number;
  users: LicenseUser[];
}

interface LicenseAction {
  action: 'query' | 'release' | 'hold';
  featureName: string;
  userName: string;
  expectedSuccess: boolean;
}
```

#### Relationships
- **Has Many**: LicenseInfo (expected parsing results)
- **Has Many**: LicenseAction (expected actions)
- **Belongs To**: TestSuite (grouped in test suites)

### TestConfiguration

**Purpose:** Represents test-specific configuration settings that override or extend the production service configuration for testing purposes. This model enables testing across different scenarios without affecting production configurations.

**Key Attributes:**
- **TestId**: string - Unique identifier for the test configuration
- **ConfigType**: ConfigurationType - Type of configuration (unit, integration, performance)
- **LicenseServerPath**: string - Mock license server executable path
- **ConfigFilePath**: string - Path to test-specific configuration file
- **LogLevel**: LogLevel - Logging level for test execution
- **TimeoutMs**: number - Timeout for license operations in milliseconds
- **MockExternalDependencies**: boolean - Whether to mock external dependencies

#### TypeScript Interface

```typescript
interface TestConfiguration {
  testId: string;
  configType: 'unit' | 'integration' | 'performance';
  licenseServerPath: string;
  configFilePath: string;
  logLevel: 'debug' | 'info' | 'warn' | 'error';
  timeoutMs: number;
  mockExternalDependencies: boolean;
}

interface TestEnvironment {
  environmentId: string;
  name: string;
  testConfigurations: TestConfiguration[];
  mockServices: MockServiceConfig[];
}
```

#### Relationships
- **Belongs To**: TestEnvironment (part of test environment setup)
- **Has Many**: MockServiceConfig (mock service configurations)

### MockServiceConfig

**Purpose:** Configuration for mock external services including the SolidWorks License Manager and other external dependencies. This model enables realistic testing without requiring actual external services.

**Key Attributes:**
- **ServiceId**: string - Unique identifier for the mock service
- **ServiceType**: ServiceType - Type of service being mocked (license manager, file system, etc.)
- **ResponseFile**: string - Path to mock response data file
- **Behavior**: MockBehavior - Mock behavior (success, failure, timeout, etc.)
- **LatencyMs**: number - Simulated latency in milliseconds
- **FailureRate**: number - Simulated failure rate (0.0-1.0)

#### TypeScript Interface

```typescript
interface MockServiceConfig {
  serviceId: string;
  serviceType: 'license-manager' | 'file-system' | 'network' | 'process';
  responseFile: string;
  behavior: 'always-success' | 'always-failure' | 'random' | 'conditional';
  latencyMs: number;
  failureRate: number;
}

interface MockResponse {
  serviceType: string;
  requestType: string;
  response: any;
  statusCode: number;
  headers: Record<string, string>;
}
```

#### Relationships
- **Belongs To**: TestEnvironment (configured within test environment)
- **Has Many**: MockResponse ( predefined responses for different scenarios)

### TestExecutionResult

**Purpose:** Captures the results of test execution including metrics, outcomes, and diagnostic information. This model enables comprehensive test reporting and analysis.

**Key Attributes:**
- **ExecutionId**: string - Unique identifier for test execution
- **TestScenarioId**: string - Reference to the test scenario executed
- **StartTime**: DateTime - Test execution start time
- **EndTime**: DateTime - Test execution end time
- **Outcome**: TestOutcome - Test result (passed, failed, skipped)
- **Metrics**: TestMetrics - Performance and resource usage metrics
- **ErrorMessage**: string - Error message if test failed
- **DiagnosticInfo**: DiagnosticInfo - Additional diagnostic information

#### TypeScript Interface

```typescript
interface TestExecutionResult {
  executionId: string;
  testScenarioId: string;
  startTime: Date;
  endTime: Date;
  outcome: 'passed' | 'failed' | 'skipped';
  metrics: TestMetrics;
  errorMessage?: string;
  diagnosticInfo: DiagnosticInfo;
}

interface TestMetrics {
  durationMs: number;
  memoryUsedMB: number;
  cpuUsagePercent: number;
  operationsCount: number;
  successRate: number;
}

interface DiagnosticInfo {
  logs: TestLogEntry[];
  stackTraces: string[];
  systemInfo: SystemInfo;
}
```

#### Relationships
- **Belongs To**: TestScenario (result of executing a scenario)
- **Has Many**: TestLogEntry (log entries during execution)
- **Has One**: TestMetrics (performance metrics)

### PerformanceBenchmark

**Purpose:** Defines performance benchmarks and expected performance characteristics for license management operations. This model enables performance regression testing and monitoring.

**Key Attributes:**
- **BenchmarkId**: string - Unique identifier for the benchmark
- **Operation**: LicensedOperation - Type of operation being benchmarked
- **BaselineMs**: number - Baseline execution time in milliseconds
- **ThresholdMs**: number - Maximum acceptable execution time
- **SampleSize**: number - Number of samples for benchmark
- **Percentile95**: number - 95th percentile execution time
- **MemoryBaselineMB**: number - Baseline memory usage in megabytes

#### TypeScript Interface

```typescript
interface PerformanceBenchmark {
  benchmarkId: string;
  operation: 'license-query' | 'license-parse' | 'license-release' | 'config-reload';
  baselineMs: number;
  thresholdMs: number;
  sampleSize: number;
  percentile95: number;
  memoryBaselineMB: number;
}

interface BenchmarkResult {
  benchmarkId: string;
  executionId: string;
  actualMs: number;
  actualMemoryMB: number;
  passedThreshold: boolean;
  variancePercent: number;
}
```

#### Relationships
- **Has Many**: BenchmarkResult (results from benchmark executions)
- **Belongs To**: TestSuite (grouped within test suites)
