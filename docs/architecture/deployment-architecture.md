# Deployment Architecture

The deployment architecture is designed specifically for the Windows Service environment and testing framework, ensuring seamless integration with existing infrastructure while enabling comprehensive testing capabilities.

### Deployment Strategy

**Frontend Deployment:** Not applicable - This is a backend-only testing framework with no frontend components.

**Backend Deployment:**
- **Platform:** Windows Server 2019+ / Windows 10/11 Pro
- **Build Command:** `msbuild LicenseReleaseService.sln /p:Configuration=Release /p:Platform="Any CPU"`
- **Deployment Method:** Windows Service installation with MSI installer or PowerShell scripts

**Testing Framework Deployment:**
- **Platform:** Same as backend - Windows Server 2019+ / Windows 10/11 Pro
- **Build Command:** `msbuild LicenseReleaseService.Tests.csproj /p:Configuration=Release`
- **Deployment Method:** File copy to test environments with PowerShell configuration scripts

**Service Installation Workflow:**
```powershell
# 1. Build solution
msbuild LicenseReleaseService.sln /p:Configuration=Release /p:Platform="Any CPU"

# 2. Create service directories
New-Item -ItemType Directory -Force -Path "C:\Program Files\LicenseReleaseService"
New-Item -ItemType Directory -Force -Path "C:\ProgramData\LicenseReleaseService\Logs"
New-Item -ItemType Directory -Force -Path "C:\ProgramData\LicenseReleaseService\Config"

# 3. Copy service files
Copy-Item "LicenseReleaseService\bin\Release\*" "C:\Program Files\LicenseReleaseService\" -Recurse

# 4. Install Windows Service
New-Service -Name "LicenseReleaseService" -BinaryPathName "C:\Program Files\LicenseReleaseService\LicenseReleaseService.exe" -DisplayName "License Release Service" -StartupType Automatic

# 5. Configure service
sc.exe config LicenseReleaseService start=auto
sc.exe description LicenseReleaseService "Automatically manages SolidWorks network licenses"

# 6. Start service
Start-Service -Name "LicenseReleaseService"
```

### CI/CD Pipeline

#### Azure DevOps Pipeline Configuration

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
    - master
    - develop
    - feature/*

pr:
  branches:
    include:
    - master
    - develop

pool:
  vmImage: 'windows-latest'

variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'
  testFramework: 'net48'

stages:
- stage: Build
  displayName: 'Build Stage'
  jobs:
  - job: Build
    displayName: 'Build Solution'
    steps:
    - task: NuGetToolInstaller@1
      displayName: 'Install NuGet'

    - task: NuGetCommand@2
      displayName: 'Restore NuGet Packages'
      inputs:
        command: 'restore'
        restoreSolution: '$(solution)'

    - task: VSBuild@1
      displayName: 'Build Solution'
      inputs:
        solution: '$(solution)'
        platform: '$(buildPlatform)'
        configuration: '$(buildConfiguration)'
        msbuildArgs: '/p:OutDir=$(Build.ArtifactStagingDirectory) /p:GeneratePackageOnBuild=true'

    - task: PublishBuildArtifacts@1
      displayName: 'Publish Build Artifacts'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)'
        ArtifactName: 'drop'

- stage: Test
  displayName: 'Test Stage'
  dependsOn: Build
  jobs:
  - job: UnitTests
    displayName: 'Unit Tests'
    steps:
    - task: DownloadBuildArtifacts@0
      displayName: 'Download Build Artifacts'
      inputs:
        buildType: 'current'
        downloadType: 'single'
        artifactName: 'drop'
        downloadPath: '$(System.ArtifactsDirectory)'

    - task: VSTest@2
      displayName: 'Run Unit Tests'
      inputs:
        platform: '$(buildPlatform)'
        configuration: '$(buildConfiguration)'
        testAssemblyVer2: |
          **\*UnitTests.dll
          !**\obj\**
        codeCoverageEnabled: true
        testRunTitle: 'Unit Tests'

    - task: PublishTestResults@2
      displayName: 'Publish Test Results'
      inputs:
        testResultsFormat: 'VSTest'
        testResultsFiles: '**/*.trx'
        failTaskOnFailedTests: true

  - job: IntegrationTests
    displayName: 'Integration Tests'
    dependsOn: UnitTests
    steps:
    - task: DownloadBuildArtifacts@0
      displayName: 'Download Build Artifacts'
      inputs:
        buildType: 'current'
        downloadType: 'single'
        artifactName: 'drop'
        downloadPath: '$(System.ArtifactsDirectory)'

    - task: PowerShell@2
      displayName: 'Setup Test Environment'
      inputs:
        targetType: 'filePath'
        filePath: 'scripts/setup-test-environment.ps1'

    - task: VSTest@2
      displayName: 'Run Integration Tests'
      inputs:
        platform: '$(buildPlatform)'
        configuration: '$(buildConfiguration)'
        testAssemblyVer2: |
          **\*IntegrationTests.dll
          !**\obj\**
        testRunTitle: 'Integration Tests'

    - task: PublishTestResults@2
      displayName: 'Publish Test Results'
      inputs:
        testResultsFormat: 'VSTest'
        testResultsFiles: '**/*.trx'
        failTaskOnFailedTests: true

  - job: PerformanceTests
    displayName: 'Performance Tests'
    dependsOn: IntegrationTests
    condition: succeeded()
    steps:
    - task: DownloadBuildArtifacts@0
      displayName: 'Download Build Artifacts'
      inputs:
        buildType: 'current'
        downloadType: 'single'
        artifactName: 'drop'
        downloadPath: '$(System.ArtifactsDirectory)'

    - task: PowerShell@2
      displayName: 'Run Performance Benchmarks'
      inputs:
        targetType: 'filePath'
        filePath: 'scripts/run-performance-tests.ps1'

    - task: PublishBuildArtifacts@1
      displayName: 'Publish Performance Results'
      inputs:
        PathtoPublish: 'TestResults/Performance'
        ArtifactName: 'performance-results'

- stage: Deploy
  displayName: 'Deploy Stage'
  dependsOn: Test
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/master'))
  jobs:
  - job: Deploy
    displayName: 'Deploy to Production'
    steps:
    - task: DownloadBuildArtifacts@0
      displayName: 'Download Build Artifacts'
      inputs:
        buildType: 'current'
        downloadType: 'single'
        artifactName: 'drop'
        downloadPath: '$(System.ArtifactsDirectory)'

    - task: PowerShell@2
      displayName: 'Deploy Windows Service'
      inputs:
        targetType: 'filePath'
        filePath: 'scripts/deploy-service.ps1'
      env:
        SERVICE_PATH: 'C:\Program Files\LicenseReleaseService'
        CONFIG_PATH: 'C:\ProgramData\LicenseReleaseService\Config'

    - task: PowerShell@2
      displayName: 'Verify Deployment'
      inputs:
        targetType: 'filePath'
        filePath: 'scripts/verify-deployment.ps1'
```

### Environments

| Environment | Frontend URL | Backend URL | Purpose |
|-------------|-------------|-------------|---------|
| **Development** | N/A | `localhost:5000` | Local development and debugging |
| **Testing** | N/A | `test-server.company.com:5000` | Automated testing and validation |
| **Staging** | N/A | `staging-server.company.com:5000` | Pre-production validation |
| **Production** | N/A | `license-server.company.com:5000` | Live production environment |

### Environment Configuration

#### Development Environment
```json
{
  "environment": "Development",
  "licenseServer": {
    "server": "localhost",
    "port": 27000,
    "timeout": 30
  },
  "logging": {
    "level": "Debug",
    "enableConsoleLogging": true,
    "enableFileLogging": true,
    "logFilePath": "C:\\Logs\\LicenseReleaseService\\dev"
  },
  "testing": {
    "enableMockServices": true,
    "enableDebugLogging": true,
    "skipLongRunningTests": false
  }
}
```

#### Production Environment
```json
{
  "environment": "Production",
  "licenseServer": {
    "server": "license-server.company.com",
    "port": 27000,
    "timeout": 30
  },
  "logging": {
    "level": "Information",
    "enableConsoleLogging": false,
    "enableFileLogging": true,
    "enableEventLogging": true,
    "logFilePath": "C:\\ProgramData\\LicenseReleaseService\\Logs"
  },
  "testing": {
    "enableMockServices": false,
    "enableDebugLogging": false,
    "skipLongRunningTests": true
  },
  "security": {
    "enableAuthentication": true,
    "enableAuditLogging": true
  }
}
```

### Deployment Monitoring

#### Health Check Configuration
```csharp
public class DeploymentHealthCheck
{
    private readonly IHealthChecker _healthChecker;
    private readonly ILogger<DeploymentHealthCheck> _logger;

    public async Task<bool> VerifyDeploymentAsync()
    {
        var healthReport = await _healthChecker.GetHealthReportAsync();

        // Check overall health
        if (healthReport.OverallStatus != HealthStatus.Healthy)
        {
            _logger.LogError("Deployment health check failed: {Status}", healthReport.OverallStatus);
            return false;
        }

        // Check critical components
        var criticalComponents = new[] { "LicenseManager", "ConfigurationManager", "ProcessExecutor" };
        foreach (var component in criticalComponents)
        {
            if (!healthReport.HealthChecks.ContainsKey(component) ||
                healthReport.HealthChecks[component].Status != HealthStatus.Healthy)
            {
                _logger.LogError("Critical component unhealthy: {Component}", component);
                return false;
            }
        }

        _logger.LogInformation("Deployment health check passed");
        return true;
    }
}
```
