# Development Workflow

This workflow defines the complete development setup and procedures for the License Release Service testing framework, enabling efficient development, testing, and deployment cycles.

### Local Development Setup

#### Prerequisites

```bash
# System requirements check
systeminfo | findstr /B /C:"OS Name" /C:"System Type" /C:"Total Physical Memory"

# Verify .NET Framework 4.8 installation
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release

# Check Visual Studio installation
"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vs_installer.exe" --list

# Verify Git installation
git --version

# Check Windows SDK version
dir "C:\Program Files (x86)\Windows Kits\10\Lib" /AD
```

#### Initial Setup

```bash
# Clone repository
git clone <repository-url> LicenseReleaseService
cd LicenseReleaseService

# Restore NuGet packages
nuget restore LicenseReleaseService.sln

# Build solution
msbuild LicenseReleaseService.sln /p:Configuration=Release /p:Platform="Any CPU"

# Set up test environment
powershell -ExecutionPolicy Bypass -File scripts\setup-test-environment.ps1

# Verify test framework setup
dotnet test LicenseReleaseService.Tests.csproj --list-tests
```

#### Development Commands

```bash
# Start all services (development mode)
powershell -ExecutionPolicy Bypass -File scripts\start-dev-environment.ps1

# Start frontend only (not applicable for this backend-only project)
# N/A - No frontend components

# Start backend only
powershell -ExecutionPolicy Bypass -File scripts\start-service.ps1

# Run tests
# Run all tests
dotnet test LicenseReleaseService.Tests.csproj --configuration Debug --logger "console;verbosity=detailed"

# Run specific test categories
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Performance"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test LicenseReleaseService.Tests\Unit\Configuration.ConfigurationManagerTests.csproj

# Run performance benchmarks
dotnet run --project LicenseReleaseService.Tests --configuration Release -- --benchmark

# Generate test reports
powershell -ExecutionPolicy Bypass -File scripts\generate-test-reports.ps1
```

### Environment Configuration

#### Required Environment Variables

```bash
# Frontend (.env.local) - Not applicable for this backend-only project
# N/A - No frontend environment variables required

# Backend (.env)
# License Release Service Configuration
LicenseServer__Server=license-server.company.com
LicenseServer__Port=27000
LicenseServer__Timeout=30
LicenseServer__RetryCount=3

# Testing Framework Configuration
TestFramework__LogLevel=Debug
TestFramework__TestDataPath=C:\LicenseReleaseService\TestData
TestFramework__MockServicesEnabled=true
TestFramework__ParallelExecutionEnabled=true

# File System Paths
Configuration__ConfigFilePath=C:\LicenseReleaseService\config\app.config
Logging__LogFilePath=C:\LicenseReleaseService\logs
TestData__BasePath=C:\LicenseReleaseService\TestData

# Performance Testing
Performance__EnableProfiling=false
Performance__MaxConcurrentTests=4
Performance__TestTimeoutMs=300000

# Development Settings
Development__EnableDebugLogging=true
Development__EnableMockServices=true
Development__SkipLongRunningTests=false

# Shared
DOTNET_CLI_TELEMETRY_OPTOUT=1
DOTNET_ROOT=C:\Program Files\dotnet
```

### IDE Integration Setup

#### Visual Studio Configuration

```json
// .vscode/settings.json
{
    "dotnet.defaultSolution": "LicenseReleaseService.sln",
    "dotnet.testRunSettings": [
        "LicenseReleaseService.Tests.runsettings"
    ],
    "files.exclude": {
        "**/bin": true,
        "**/obj": true,
        "**/TestResults": true
    },
    "editor.formatOnSave": true,
    "editor.insertSpaces": true,
    "editor.tabSize": 4
}
```

```xml
<!-- LicenseReleaseService.Tests.runsettings -->
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <TargetFrameworkVersion>Framework48</TargetFrameworkVersion>
    <ResultsDirectory>.\TestResults</ResultsDirectory>
    <TestSessionTimeout>300000</TestSessionTimeout>
  </RunConfiguration>

  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="Code Coverage">
        <Configuration>
          <CodeCoverage>
            <ModulePaths>
              <Exclude>
                <ModulePath>.*\.Tests\.dll</ModulePath>
                <ModulePath>.*\\ref\\.*</ModulePath>
                <ModulePath>.*\\x64\\.*</ModulePath>
              </Exclude>
            </ModulePaths>
            <Functions>
              <Exclude>
                <Function>.*Test.*</Function>
                <Function>.*Mock.*</Function>
              </Exclude>
            </Functions>
          </CodeCoverage>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Debugging Configuration

#### Test Debugging Setup

```csharp
// TestBase.cs - Base class for test debugging
public abstract class TestBase
{
    protected ILogger Logger { get; }
    protected TestConfiguration Configuration { get; }

    protected TestBase()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().AddDebug();
        });
        Logger = loggerFactory.CreateLogger(GetType());

        Configuration = LoadTestConfiguration();
    }

    protected void DebugLog(string message, params object[] args)
    {
        if (Configuration.EnableDebugLogging)
        {
            Logger.LogDebug(message, args);
        }
    }

    protected async Task<T> WithDebuggingAsync<T>(
        string operationName,
        Func<Task<T>> operation)
    {
        DebugLog("Starting operation: {OperationName}", operationName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation();
            stopwatch.Stop();
            DebugLog("Completed operation: {OperationName} in {Duration}ms",
                operationName, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DebugLog("Operation failed: {OperationName} after {Duration}ms - {Error}",
                operationName, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
```

### Testing Workflow

#### Test Development Workflow

```bash
# 1. Create new test
# Add new test file to appropriate directory
# Example: LicenseReleaseService.Tests/Unit/LicenseManagement/LicenseQueryEngineTests.cs

# 2. Implement test with debug support
dotnet test --filter "TestCategory=NewTest" --logger "console;verbosity=detailed"

# 3. Debug failing test
dotnet test --filter "TestCategory=NewTest" --logger "console;verbosity=detailed" --no-build

# 4. Run test with coverage
dotnet test --filter "TestCategory=NewTest" --collect:"XPlat Code Coverage"

# 5. Verify test integration
dotnet test --filter "TestCategory=NewTest" --configuration Release
```

#### Performance Testing Workflow

```bash
# 1. Run baseline benchmarks
dotnet run --project LicenseReleaseService.Tests --configuration Release -- --benchmark --filter "*Baseline*"

# 2. Run performance tests
dotnet run --project LicenseReleaseService.Tests --configuration Release -- --benchmark --filter "*Performance*"

# 3. Compare results
dotnet run --project LicenseReleaseService.Tests --configuration Release -- --benchmark --compare Baseline

# 4. Generate performance report
dotnet run --project LicenseReleaseService.Tests --configuration Release -- --benchmark --export json
```
