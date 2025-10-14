# Unified Project Structure

This monorepo structure accommodates both the existing License Release Service and the new testing framework, maintaining clear separation while enabling efficient development and deployment workflows.

```
LicenseReleaseService/
├── .github/                           # CI/CD workflows
│   └── workflows/
│       ├── ci.yml                     # Continuous integration
│       ├── test.yml                   # Test execution pipeline
│       └── deploy.yml                 # Deployment pipeline
├── apps/                              # Application packages
│   ├── LicenseReleaseService/         # Main Windows Service application
│   │   ├── Configuration/              # Service configuration management
│   │   ├── LicenseManagement/         # License query and management logic
│   │   ├── Process/                   # Process execution framework
│   │   ├── IdleDetection/             # Idle detection algorithms
│   │   ├── Properties/                # Project properties and build configuration
│   │   ├── ServiceSettings.cs         # Service settings wrapper
│   │   ├── LicenseReleaseService.cs   # Main Windows Service implementation
│   │   ├── app.config                 # Service configuration file
│   │   └── LicenseReleaseService.csproj # Service project file
│   └── LicenseReleaseService.Tests/   # Main testing framework application
│       ├── Unit/                      # Unit test implementations
│       │   ├── Configuration/
│       │   │   ├── ConfigurationManagerTests.cs
│       │   │   ├── ServiceSettingsTests.cs
│       │   │   └── ConfigurationValidationTests.cs
│       │   ├── LicenseManagement/
│       │   │   ├── LicenseQueryEngineTests.cs
│       │   │   ├── LmutilLicenseManagerTests.cs
│       │   │   ├── LicenseParsingTests.cs
│       │   │   └── LicenseCachingTests.cs
│       │   ├── ProcessExecution/
│       │   │   ├── ProcessExecutorTests.cs
│       │   │   ├── ProcessMetricsTests.cs
│       │   │   └── ProcessTimeoutTests.cs
│       │   └── HealthMonitoring/
│       │       ├── HealthCheckerTests.cs
│       │       ├── RecoveryManagerTests.cs
│       │       └── ServiceStateTests.cs
│       ├── Integration/               # Integration test implementations
│       │   ├── ServiceLifecycle/
│       │   │   ├── ServiceStartupTests.cs
│       │   │   ├── ServiceShutdownTests.cs
│       │   │   ├── ServicePauseResumeTests.cs
│       │   │   └── ConfigurationHotReloadTests.cs
│       │   ├── LicenseWorkflows/
│       │   │   ├── EndToEndLicenseQueryTests.cs
│       │   │   ├── LicenseReleaseWorkflowTests.cs
│       │   │   └── LicenseParsingIntegrationTests.cs
│       │   └── ErrorRecovery/
│       │       ├── NetworkFailureRecoveryTests.cs
│       │       ├── LicenseServerFailureTests.cs
│       │       └── FileSystemErrorTests.cs
│       ├── Performance/               # Performance testing implementations
│       │   ├── Benchmarks/
│       │   │   ├── LicenseQueryBenchmark.cs
│       │   │   ├── ConfigurationReloadBenchmark.cs
│       │   │   └── ProcessExecutionBenchmark.cs
│       │   └── LoadTests/
│       │       ├── ConcurrentLicenseQueryTests.cs
│       │       ├── MemoryUsageTests.cs
│       │       └── ResourceLeakTests.cs
│       ├── TestData/                  # Test data and configurations
│       │   ├── LicenseServerResponses/
│       │   │   ├── license-query-success-2023.json
│       │   │   ├── license-query-exhausted.json
│       │   │   ├── license-server-error.json
│       │   │   └── license-server-timeout.json
│       │   ├── Configurations/
│       │   │   ├── unit-test-config.json
│       │   │   ├── integration-test-config.json
│       │   │   ├── performance-test-config.json
│       │   │   └── mock-services.config
│       │   └── Scenarios/
│       │       ├── basic-license-query.json
│       │       ├── complex-license-release.json
│       │       └── configuration-reload.json
│       ├── TestInfrastructure/          # Testing framework infrastructure
│       │   ├── Mocks/
│       │   │   ├── MockLicenseServer.cs
│       │   │   ├── MockFileSystem.cs
│       │   │   ├── MockWindowsService.cs
│       │   │   └── MockProcessExecutor.cs
│       │   ├── TestHosts/
│       │   │   ├── TestServiceHost.cs
│       │   │   ├── IntegrationTestHost.cs
│       │   │   └── PerformanceTestHost.cs
│       │   ├── Utilities/
│       │   │   ├── TestDataProvider.cs
│       │   │   ├── TestConfigurationManager.cs
│       │   │   ├── TestMetricsCollector.cs
│       │   │   └── TestResultReporter.cs
│       │   └── BaseClasses/
│       │       ├── BaseUnitTest.cs
│       │       ├── BaseIntegrationTest.cs
│       │       ├── BasePerformanceTest.cs
│       │       └── TestBase.cs
│       ├── TestResults/                # Test execution results
│       │   ├── 2025-01-13/
│       │   │   ├── unit-test-results.json
│       │   │   ├── integration-test-results.json
│       │   │   ├── performance-test-results.json
│       │   │   └── coverage-report.xml
│       │   └── Historical/
│       └── LicenseReleaseService.Tests.csproj
├── packages/                          # Shared packages
│   ├── shared/                        # Shared types and utilities
│   │   ├── src/
│   │   │   ├── Types/
│   │   │   │   ├── LicenseTypes.cs
│   │   │   │   ├── ConfigurationTypes.cs
│   │   │   │   ├── TestTypes.cs
│   │   │   │   └── PerformanceTypes.cs
│   │   │   ├── Constants/
│   │   │   │   ├── ServiceConstants.cs
│   │   │   │   ├── TestConstants.cs
│   │   │   │   └── ConfigurationConstants.cs
│   │   │   ├── Utils/
│   │   │   │   ├── SerializationHelper.cs
│   │   │   │   ├── ValidationHelper.cs
│   │   │   │   └── PerformanceHelper.cs
│   │   │   └── Extensions/
│   │   │       ├── ServiceExtensions.cs
│   │   │       ├── TestExtensions.cs
│   │   │       └── ConfigurationExtensions.cs
│   │   └── package.json
│   └── config/                        # Shared configuration
│       ├── eslint/
│       │   └── .eslintrc.js
│       ├── typescript/
│       │   └── tsconfig.json
│       └── jest/
│           └── jest.config.js
├── infrastructure/                     # Infrastructure as code
│   └── iac/
│       ├── azure-pipelines.yml
│       ├── github-actions.yml
│       └── deployment-scripts/
├── scripts/                           # Build and deployment scripts
│   ├── build.bat
│   ├── test.bat
│   ├── deploy.bat
│   ├── setup-test-environment.ps1
│   └── generate-test-reports.ps1
├── docs/                              # Documentation
│   ├── prd.md                         # Product Requirements Document
│   ├── architecture.md                # This architecture document
│   ├── api.md                         # API documentation
│   ├── deployment.md                   # Deployment guide
│   └── testing.md                     # Testing guide
├── tools/                             # Development tools
│   ├── test-data-generator/           # Test data generation tools
│   ├── mock-server/                   # Mock server implementation
│   └── performance-analyzer/           # Performance analysis tools
├── .env.example                       # Environment variables template
├── LicenseReleaseService.sln          # Solution file
├── package.json                       # Root package.json for scripts
├── README.md                          # Project README
└── .gitignore                         # Git ignore file
```
