# License Release Service - Administrator Guide

## Overview

This Administrator Guide provides comprehensive information for managing and maintaining the Idle Detection system. It covers installation, configuration, monitoring, troubleshooting, and best practices for system administration.

## System Architecture

### Core Components

```
License Release Service
├── Idle Detection Engine
│   ├── Time-Based Detection
│   ├── Ping-Based Detection
│   ├── Activity Monitoring
│   └── Consensus Engine
├── License Manager Integration
├── Timer Service
├── Configuration Management
├── Event System
└── Monitoring & Alerting
```

### Service Dependencies

- **License Manager**: Handles license queries and releases
- **Timer Service**: Manages scheduled detection cycles
- **Configuration Service**: Provides configuration management
- **Event System**: Handles event propagation and notifications
- **Monitoring Service**: Provides health monitoring and metrics

## Installation and Deployment

### Prerequisites

**System Requirements**:
- Windows Server 2016 or later (Windows 10/11 for workstation deployment)
- .NET Framework 4.8 or later
- 4GB RAM minimum (8GB recommended)
- 10GB free disk space
- Network connectivity to license server

**Software Dependencies**:
- Windows Service hosting environment
- Microsoft .NET Framework 4.8
- PowerShell 5.1 or later
- License Manager client libraries
- Monitoring agent (optional)

### Installation Process

#### 1. Prepare Environment
```powershell
# Check .NET Framework version
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP' -Recurse |
    Get-ItemProperty -Name Version -EA 0 |
    Where-Object { $_.PSChildName -match '^(?!S)\p{L}' } |
    Select-Object PSChildName, Version

# Create service account
New-ADUser -Name "LicenseSvc" -AccountPassword (ConvertTo-SecureString "P@ssw0rd123!" -AsPlainText -Force) -Enabled $true

# Add service account to local administrators group
Add-LocalGroupMember -Group "Administrators" -Member "DOMAIN\LicenseSvc"
```

#### 2. Install Service
```powershell
# Navigate to installation directory
cd "C:\Install\LicenseReleaseService"

# Run installer
.\Install-LicenseReleaseService.ps1 -InstallPath "C:\Program Files\LicenseReleaseService" -ServiceAccount "DOMAIN\LicenseSvc"

# Configure firewall rules
New-NetFirewallRule -DisplayName "License Release Service" -Direction Inbound -Program "C:\Program Files\LicenseReleaseService\LicenseReleaseService.exe" -Action Allow
```

#### 3. Configure Service
```powershell
# Set service startup type
Set-Service -Name "LicenseReleaseService" -StartupType Automatic

# Set service recovery options
sc.exe failure "LicenseReleaseService" reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Start service
Start-Service -Name "LicenseReleaseService"

# Verify service status
Get-Service -Name "LicenseReleaseService"
```

### Configuration Files

#### Main Configuration (app.config)
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="idleDetection" type="LicenseReleaseService.IdleDetection.IdleDetectionConfiguration, LicenseReleaseService" />
    <section name="logging" type="LicenseReleaseService.Logging.LoggingConfiguration, LicenseReleaseService" />
    <section name="licenseManager" type="LicenseReleaseService.LicenseManagement.LicenseManagerConfiguration, LicenseReleaseService" />
  </configSections>

  <idleDetection configSource="Config\idleDetection.config" />
  <logging configSource="Config\logging.config" />
  <licenseManager configSource="Config\licenseManager.config" />

  <appSettings>
    <add key="ServiceName" value="LicenseReleaseService" />
    <add key="Environment" value="Production" />
    <add key="LogLevel" value="Information" />
  </appSettings>
</configuration>
```

#### Idle Detection Configuration
```xml
<idleDetection>
  <timeBasedDetection
    detectionIntervalSeconds="60"
    warningThresholdMinutes="5"
    imminentThresholdMinutes="10"
    criticalThresholdMinutes="15"
    hysteresisMinutes="2"
    confidenceThreshold="0.7"
    enableWorkHours="true"
    workHourStart="09:00"
    workHourEnd="17:00"
    workDayMultiplier="1.0"
    offHourMultiplier="0.5"
    weekendMultiplier="0.3"
    holidaysMultiplier="0.2"
    enableAdaptiveThresholds="true"
    adaptiveLearningRate="0.1"
    minAdaptiveThreshold="0.1"
    maxAdaptiveThreshold="2.0"
    enableGraduatedDetection="true"
    enableKeyboardMonitoring="true"
    enableMouseMonitoring="true"
    enableSystemMonitoring="true"
    enableSolidWorksMonitoring="true"
    monitoringSampleRateMs="100"
    maxDetectionTimeMs="5000"
  />

  <pingBasedDetection
    pingIntervalSeconds="30"
    pingTimeoutMs="5000"
    maxPingFailures="3"
    enableProcessMonitoring="true"
    enableWindowMonitoring="true"
    enableNetworkMonitoring="true"
    processNames="SLDWORKS.exe,solidworks.exe"
    includeChildProcesses="false"
    processPriorityClass="Normal"
    minProcessMemoryMB="100"
    maxProcessCpuPercent="95"
    checkWindowVisibility="true"
    checkWindowFocus="true"
    checkWindowPosition="false"
    minVisibleAreaPercent="10"
    focusChangeTimeoutMs="2000"
  />

  <activityMonitoring
    enableFileSystemMonitoring="true"
    enablePerformanceMonitoring="true"
    enableSystemActivityTracking="true"
    activityHistorySize="1000"
    activityAnalysisIntervalMs="5000"
    monitoredExtensions=".sldprt,.sldasm,.slddrw"
    monitoredDirectories=""
    includeSubdirectories="true"
    fileAccessThresholdMs="100"
    directoryChangeBufferSize="8192"
    cpuUsageThreshold="10"
    memoryUsageThresholdMB="50"
    diskIOThresholdMB="1"
    networkActivityThresholdKB="100"
    performanceSampleIntervalMs="1000"
  />

  <consensusEngine
    enableConsensusLogic="true"
    consensusThreshold="0.7"
    minDetectorsForConsensus="2"
    enableHysteresis="true"
    hysteresisDurationMinutes="2"
    timeBasedWeight="0.4"
    pingBasedWeight="0.4"
    activityMonitoringWeight="0.2"
    confidenceBoostFactor="1.1"
    confidencePenaltyFactor="0.9"
  />
</idleDetection>
```

## Configuration Management

### Configuration Best Practices

#### 1. Environment Separation
```powershell
# Create environment-specific configurations
$environments = @("Development", "Staging", "Production")

foreach ($env in $environments) {
    $configPath = "C:\Program Files\LicenseReleaseService\Config\$env"
    New-Item -ItemType Directory -Path $configPath -Force

    # Copy base configuration
    Copy-Item -Path "C:\Program Files\LicenseReleaseService\Config\base\*.config" -Destination $configPath

    # Apply environment-specific overrides
    switch ($env) {
        "Development" {
            Set-ConfigValue -Path "$configPath\idleDetection.config" -XPath "//detectionIntervalSeconds" -Value "300"
        }
        "Staging" {
            Set-ConfigValue -Path "$configPath\idleDetection.config" -XPath "//detectionIntervalSeconds" -Value "120"
        }
        "Production" {
            Set-ConfigValue -Path "$configPath\idleDetection.config" -XPath "//detectionIntervalSeconds" -Value "60"
        }
    }
}
```

#### 2. Configuration Validation
```powershell
function Validate-IdleDetectionConfiguration {
    param (
        [string]$ConfigPath
    )

    try {
        # Load configuration
        $config = Get-IdleDetectionConfiguration -Path $ConfigPath

        # Validate thresholds
        if ($config.TimeBasedDetection.WarningThresholdMinutes -ge $config.TimeBasedDetection.ImminentThresholdMinutes) {
            throw "Warning threshold must be less than imminent threshold"
        }

        if ($config.TimeBasedDetection.ImminentThresholdMinutes -ge $config.TimeBasedDetection.CriticalThresholdMinutes) {
            throw "Imminent threshold must be less than critical threshold"
        }

        # Validate consensus weights
        $totalWeight = $config.ConsensusEngine.TimeBasedWeight +
                       $config.ConsensusEngine.PingBasedWeight +
                       $config.ConsensusEngine.ActivityMonitoringWeight

        if ([Math]::Abs($totalWeight - 1.0) -gt 0.01) {
            throw "Consensus weights must sum to 1.0"
        }

        # Validate time formats
        if (-not [TimeSpan]::TryParse($config.TimeBasedDetection.WorkHourStart, [ref]$null)) {
            throw "Invalid work hour start format"
        }

        if (-not [TimeSpan]::TryParse($config.TimeBasedDetection.WorkHourEnd, [ref]$null)) {
            throw "Invalid work hour end format"
        }

        Write-Host "Configuration validation successful" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "Configuration validation failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}
```

#### 3. Configuration Deployment
```powershell
function Deploy-Configuration {
    param (
        [string]$Environment,
        [string]$ConfigPath,
        [switch]$Backup
    )

    $servicePath = "C:\Program Files\LicenseReleaseService"
    $targetPath = Join-Path $servicePath "Config"

    # Stop service
    Stop-Service -Name "LicenseReleaseService" -Force

    if ($Backup) {
        # Create backup
        $backupPath = Join-Path $servicePath "Config\Backup\$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        New-Item -ItemType Directory -Path $backupPath -Force
        Copy-Item -Path "$targetPath\*.config" -Destination $backupPath
    }

    # Deploy new configuration
    Copy-Item -Path "$ConfigPath\*.config" -Destination $targetPath -Force

    # Validate configuration
    if (-not (Validate-IdleDetectionConfiguration -Path (Join-Path $targetPath "idleDetection.config"))) {
        # Restore from backup if validation failed
        if ($Backup) {
            Copy-Item -Path "$backupPath\*.config" -Destination $targetPath -Force
        }
        throw "Configuration validation failed, deployment aborted"
    }

    # Start service
    Start-Service -Name "LicenseReleaseService"

    Write-Host "Configuration deployed successfully to $Environment" -ForegroundColor Green
}
```

## Monitoring and Health Management

### Health Monitoring Setup

#### 1. Service Health Monitoring
```powershell
function Get-ServiceHealth {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    try {
        $service = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -ErrorAction Stop

        # Check service status
        $status = @{
            ServiceName = $service.Name
            DisplayName = $service.DisplayName
            Status = $service.Status
            CanStart = $true
            CanStop = $true
            CanPauseAndContinue = $false
            MachineName = $service.MachineName
            Timestamp = Get-Date
        }

        # Check if service is responsive
        try {
            $health = Invoke-RestMethod -Uri "http://$ComputerName:8080/api/idledetection/health" -TimeoutSec 5
            $status.IsHealthy = $health.IsHealthy
            $status.HealthMessage = $health.StatusMessage
            $status.EngineStatus = $health.Status
            $status.DetectorCount = $health.Statistics.DetectorCount
            $status.EnabledDetectorCount = $health.Statistics.EnabledDetectorCount
            $status.SuccessRate = $health.Statistics.SuccessRate
        }
        catch {
            $status.IsHealthy = $false
            $status.HealthMessage = "Service not responding to health checks"
            $status.EngineStatus = "Unknown"
            $status.DetectorCount = 0
            $status.EnabledDetectorCount = 0
            $status.SuccessRate = 0
        }

        return [PSCustomObject]$status
    }
    catch {
        return [PSCustomObject]@{
            ServiceName = "LicenseReleaseService"
            DisplayName = "License Release Service"
            Status = "NotFound"
            CanStart = $false
            CanStop = $false
            CanPauseAndContinue = $false
            MachineName = $ComputerName
            Timestamp = Get-Date
            IsHealthy = $false
            HealthMessage = "Service not found"
            EngineStatus = "Unknown"
            DetectorCount = 0
            EnabledDetectorCount = 0
            SuccessRate = 0
        }
    }
}
```

#### 2. Performance Monitoring
```powershell
function Get-ServicePerformance {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [int]$SampleCount = 10
    )

    $counters = @(
        "\Process(LicenseReleaseService)\% Processor Time",
        "\Process(LicenseReleaseService)\Private Bytes",
        "\Process(LicenseReleaseService)\Handle Count",
        "\Process(LicenseReleaseService)\Thread Count"
    )

    $samples = Get-Counter -Counter $counters -ComputerName $ComputerName -MaxSamples $SampleCount -ErrorAction SilentlyContinue

    if ($samples) {
        $performanceData = foreach ($sample in $samples.CounterSamples) {
            [PSCustomObject]@{
                Timestamp = $sample.Timestamp
                Counter = $sample.Path
                Value = $sample.CookedValue
            }
        }

        # Calculate statistics
        $stats = $performanceData | Group-Object -Property Counter | ForEach-Object {
            $values = $_.Group.Value
            [PSCustomObject]@{
                Counter = $_.Name
                Average = ($values | Measure-Object -Average).Average
                Maximum = ($values | Measure-Object -Maximum).Maximum
                Minimum = ($values | Measure-Object -Minimum).Minimum
                LastValue = $values[-1]
            }
        }

        return $stats
    }
    else {
        Write-Warning "Unable to collect performance data from $ComputerName"
        return $null
    }
}
```

#### 3. Detection Metrics Monitoring
```powershell
function Get-DetectionMetrics {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [DateTime]$StartTime = (Get-Date).AddHours(-1),
        [DateTime]$EndTime = (Get-Date)
    )

    try {
        $logs = Get-WinEvent -ComputerName $ComputerName -LogName "Application" -ProviderName "LicenseReleaseService" -StartTime $StartTime -EndTime $EndTime |
            Where-Object { $_.Id -eq 1001 -or $_.Id -eq 1002 }

        $metrics = @{
            TotalDetections = ($logs | Where-Object { $_.Id -eq 1001 }).Count
            SuccessfulDetections = ($logs | Where-Object { $_.Id -eq 1002 }).Count
            FailedDetections = ($logs | Where-Object { $_.Id -eq 1001 }).Count - ($logs | Where-Object { $_.Id -eq 1002 }).Count
            AverageDetectionTime = 0
            DetectionRate = 0
        }

        if ($metrics.TotalDetections -gt 0) {
            $metrics.SuccessRate = $metrics.SuccessfulDetections / $metrics.TotalDetections
            $metrics.FailureRate = $metrics.FailedDetections / $metrics.TotalDetections
        }

        return [PSCustomObject]$metrics
    }
    catch {
        Write-Warning "Unable to collect detection metrics from $ComputerName"
        return $null
    }
}
```

### Alert Configuration

#### 1. Set Up Monitoring Alerts
```powershell
function Set-ServiceAlerts {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [string[]]$NotificationEmails
    )

    # Create scheduled task for health monitoring
    $action = New-ScheduledTaskAction -Execute "powershell" -Argument "-File `"C:\Program Files\LicenseReleaseService\Scripts\Monitor-ServiceHealth.ps1`""
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 5)
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd

    $task = Register-ScheduledTask -TaskName "LicenseReleaseServiceHealthCheck" -Action $action -Trigger $trigger -Settings $settings -Force

    # Configure email alerts
    foreach ($email in $NotificationEmails) {
        $smtpServer = "smtp.company.com"
        $from = "licensing@company.com"
        $subject = "License Release Service Alert - $ComputerName"

        $mailParams = @{
            From = $from
            To = $email
            Subject = $subject
            SmtpServer = $smtpServer
            Body = "Service health check failed on $ComputerName. Please investigate immediately."
        }

        # Add email notification to health check script
        Add-Content -Path "C:\Program Files\LicenseReleaseService\Scripts\Monitor-ServiceHealth.ps1" -Value @"
if (-not `$health.IsHealthy) {
    Send-MailMessage @mailParams
}
"@
    }
}
```

## Troubleshooting and Diagnostics

### Common Issues and Solutions

#### 1. Service Not Starting
```powershell
function Troubleshoot-ServiceStartup {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    Write-Host "Troubleshooting service startup issues on $ComputerName" -ForegroundColor Yellow

    # Check if service exists
    $service = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -ErrorAction SilentlyContinue
    if (-not $service) {
        Write-Host "Service not found on $ComputerName" -ForegroundColor Red
        return
    }

    # Check service status
    Write-Host "Service status: $($service.Status)" -ForegroundColor Cyan

    # Check dependencies
    Write-Host "Checking dependencies..." -ForegroundColor Cyan
    $dependencies = Get-Service -Name $service.Name -DependentServices | Where-Object { $_.Status -ne "Running" }
    if ($dependencies) {
        Write-Host "Required services not running:" -ForegroundColor Red
        $dependencies | ForEach-Object { Write-Host "  - $($_.Name)" }
    }

    # Check configuration
    Write-Host "Checking configuration..." -ForegroundColor Cyan
    $configPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\app.config"
    if (Test-Path $configPath) {
        try {
            $config = [xml](Get-Content $configPath)
            Write-Host "Configuration file is valid XML" -ForegroundColor Green
        }
        catch {
            Write-Host "Configuration file is invalid: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "Configuration file not found" -ForegroundColor Red
    }

    # Check event logs
    Write-Host "Checking event logs..." -ForegroundColor Cyan
    $events = Get-WinEvent -ComputerName $ComputerName -LogName "Application" -ProviderName "LicenseReleaseService" -MaxEvents 10 -ErrorAction SilentlyContinue
    if ($events) {
        Write-Host "Recent events:" -ForegroundColor Yellow
        $events | ForEach-Object { Write-Host "  $($_.TimeCreated) - $($_.Message)" }
    }
    else {
        Write-Host "No recent events found" -ForegroundColor Yellow
    }

    # Check permissions
    Write-Host "Checking permissions..." -ForegroundColor Cyan
    $serviceAccount = (Get-WmiObject -Class Win32_Service -Filter "Name='LicenseReleaseService'" -ComputerName $ComputerName).StartName
    Write-Host "Service account: $serviceAccount" -ForegroundColor Cyan

    # Test service account permissions
    $testPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService"
    $acl = Get-Acl $testPath
    $access = $acl.Access | Where-Object { $_.IdentityReference -like "*$serviceAccount*" }
    if ($access) {
        Write-Host "Service account has appropriate permissions" -ForegroundColor Green
    }
    else {
        Write-Host "Service account lacks appropriate permissions" -ForegroundColor Red
    }
}
```

#### 2. Performance Issues
```powershell
function Diagnose-PerformanceIssues {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    Write-Host "Diagnosing performance issues on $ComputerName" -ForegroundColor Yellow

    # Get performance counters
    $performance = Get-ServicePerformance -ComputerName $ComputerName -SampleCount 5

    if ($performance) {
        Write-Host "Performance metrics:" -ForegroundColor Cyan
        $performance | ForEach-Object {
            Write-Host "  $($_.Counter): Avg=$($_.Average:F2), Max=$($_.Maximum:F2), Current=$($_.LastValue:F2)"
        }

        # Check for high CPU usage
        $cpuUsage = $performance | Where-Object { $_.Counter -like "*% Processor Time" }
        if ($cpuUsage.Average -gt 80) {
            Write-Host "High CPU usage detected: $($cpuUsage.Average:F2)%" -ForegroundColor Red
            Write-Host "Recommendations:" -ForegroundColor Yellow
            Write-Host "  - Increase detection interval"
            Write-Host "  - Reduce monitoring frequency"
            Write-Host "  - Disable unnecessary features"
        }

        # Check for high memory usage
        $memoryUsage = $performance | Where-Object { $_.Counter -like "*Private Bytes" }
        $memoryMB = $memoryUsage.Average / 1MB
        if ($memoryMB -gt 500) {
            Write-Host "High memory usage detected: $memoryMB:F2 MB" -ForegroundColor Red
            Write-Host "Recommendations:" -ForegroundColor Yellow
            Write-Host "  - Reduce activity history size"
            Write-Host "  - Enable garbage collection optimization"
            Write-Host "  - Check for memory leaks"
        }
    }
    else {
        Write-Host "Unable to collect performance data" -ForegroundColor Red
    }
}
```

### Log Management

#### 1. Log Rotation
```powershell
function Set-LogRotation {
    param (
        [string]$LogPath = "C:\Logs\LicenseReleaseService",
        [int]$MaxAgeDays = 30,
        [int]$MaxSizeMB = 100
    )

    # Create scheduled task for log rotation
    $action = New-ScheduledTaskAction -Execute "powershell" -Argument "-Command `"& 'C:\Program Files\LicenseReleaseService\Scripts\Rotate-Logs.ps1' -LogPath '$LogPath' -MaxAgeDays $MaxAgeDays -MaxSizeMB $MaxSizeMB`""
    $trigger = New-ScheduledTaskTrigger -Daily -At 2:00AM
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable

    Register-ScheduledTask -TaskName "LicenseReleaseServiceLogRotation" -Action $action -Trigger $trigger -Settings $settings -Force
}
```

#### 2. Log Analysis
```powershell
function Analyze-ServiceLogs {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [DateTime]$StartTime = (Get-Date).AddDays(-7),
        [DateTime]$EndTime = (Get-Date)
    )

    $logPath = "\\$ComputerName\C$\Logs\LicenseReleaseService"

    if (Test-Path $logPath) {
        $logFiles = Get-ChildItem -Path $logPath -Filter "*.log" | Where-Object { $_.LastWriteTime -ge $StartTime -and $_.LastWriteTime -le $EndTime }

        $analysis = @{
            TotalFiles = $logFiles.Count
            TotalSizeMB = ($logFiles | Measure-Object -Property Length -Sum).Sum / 1MB
            ErrorCount = 0
            WarningCount = 0
            InfoCount = 0
            MostCommonErrors = @()
        }

        foreach ($file in $logFiles) {
            $content = Get-Content $file.FullName

            $analysis.ErrorCount += ($content | Where-Object { $_ -match "\[ERROR\]" }).Count
            $analysis.WarningCount += ($content | Where-Object { $_ -match "\[WARN\]" }).Count
            $analysis.InfoCount += ($content | Where-Object { $_ -match "\[INFO\]" }).Count

            # Extract error patterns
            $errors = $content | Where-Object { $_ -match "\[ERROR\]" } | ForEach-Object {
                if ($_ -match ":\s+(.+)$") {
                    $matches[1]
                }
            }

            $analysis.MostCommonErrors += $errors
        }

        # Get most common errors
        $analysis.MostCommonErrors = $analysis.MostCommonErrors | Group-Object | Sort-Object -Property Count -Descending | Select-Object -First 5

        return [PSCustomObject]$analysis
    }
    else {
        Write-Warning "Log path not found: $logPath"
        return $null
    }
}
```

## Security Management

### Security Configuration

#### 1. Service Account Security
```powershell
function Configure-ServiceAccountSecurity {
    param (
        [string]$ServiceAccount = "DOMAIN\LicenseSvc",
        [string]$ComputerName = $env:COMPUTERNAME
    )

    # Remove unnecessary group memberships
    $groups = Get-ADPrincipalGroupMembership -Identity $ServiceAccount
    foreach ($group in $groups) {
        if ($group.Name -notin "Domain Users", "License Service Users") {
            Remove-ADGroupMember -Identity $group.Name -Members $ServiceAccount -Confirm:$false
        }
    }

    # Set service account permissions
    $acl = Get-Acl "C:\Program Files\LicenseReleaseService"
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $ServiceAccount,
        "ReadAndExecute",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.SetAccessRule($rule)
    Set-Acl "C:\Program Files\LicenseReleaseService" $acl

    # Configure service to run with least privileges
    $service = Get-WmiObject -Class Win32_Service -Filter "Name='LicenseReleaseService'" -ComputerName $ComputerName
    $service.Change($null, $null, $null, $null, $null, $null, $ServiceAccount, $null, $null, $null, $null)
}
```

#### 2. Network Security
```powershell
function Configure-NetworkSecurity {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    # Configure firewall rules
    $firewallRules = @(
        @{
            Name = "LicenseReleaseService-In"
            Direction = "Inbound"
            Program = "C:\Program Files\LicenseReleaseService\LicenseReleaseService.exe"
            Action = "Allow"
            LocalPort = "8080"
            Protocol = "TCP"
        },
        @{
            Name = "LicenseReleaseService-Out"
            Direction = "Outbound"
            Program = "C:\Program Files\LicenseReleaseService\LicenseReleaseService.exe"
            Action = "Allow"
            RemotePort = "27000"
            Protocol = "TCP"
        }
    )

    foreach ($rule in $firewallRules) {
        New-NetFirewallRule @rule -ErrorAction SilentlyContinue
    }

    # Enable Windows Firewall
    Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
}
```

## Backup and Recovery

### Backup Configuration

#### 1. Configuration Backup
```powershell
function Backup-ServiceConfiguration {
    param (
        [string]$BackupPath = "C:\Backup\LicenseReleaseService",
        [string]$ComputerName = $env:COMPUTERNAME
    )

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupFolder = Join-Path $BackupPath $timestamp

    New-Item -ItemType Directory -Path $backupFolder -Force

    # Backup configuration files
    $configPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\Config"
    if (Test-Path $configPath) {
        Copy-Item -Path "$configPath\*.config" -Destination $backupFolder -Force
    }

    # Backup service settings
    $service = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -ErrorAction SilentlyContinue
    if ($service) {
        $serviceInfo = @{
            ServiceName = $service.Name
            DisplayName = $service.DisplayName
            Status = $service.Status
            StartType = $service.StartType
            Account = (Get-WmiObject -Class Win32_Service -Filter "Name='LicenseReleaseService'" -ComputerName $ComputerName).StartName
        }
        $serviceInfo | ConvertTo-Json | Out-File (Join-Path $backupFolder "service_info.json")
    }

    # Backup registry settings
    $regPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\registry_backup.reg"
    if (Test-Path "HKLM:\SOFTWARE\LicenseReleaseService") {
        reg export "HKLM\SOFTWARE\LicenseReleaseService" $regPath /y
        Copy-Item $regPath $backupFolder
    }

    Write-Host "Configuration backed up to $backupFolder" -ForegroundColor Green
}
```

#### 2. Automated Backup Schedule
```powershell
function Set-AutomatedBackup {
    param (
        [string]$BackupPath = "C:\Backup\LicenseReleaseService",
        [int]$RetentionDays = 30
    )

    # Create backup script
    $backupScript = @"
# License Release Service Backup Script
`$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
`$backupFolder = Join-Path "$BackupPath" `$timestamp

# Create backup
Backup-ServiceConfiguration -BackupPath `$backupFolder

# Clean old backups
`$oldBackups = Get-ChildItem -Path "$BackupPath" | Where-Object { `$_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) }
`$oldBackups | Remove-Item -Recurse -Force

Write-Host "Backup completed and old backups cleaned up"
"@

    $scriptPath = "C:\Program Files\LicenseReleaseService\Scripts\Backup-Service.ps1"
    $backupScript | Out-File $scriptPath -Force

    # Create scheduled task
    $action = New-ScheduledTaskAction -Execute "powershell" -Argument "-File `"$scriptPath`""
    $trigger = New-ScheduledTaskTrigger -Daily -At 1:00AM
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable

    Register-ScheduledTask -TaskName "LicenseReleaseServiceBackup" -Action $action -Trigger $trigger -Settings $settings -Force

    Write-Host "Automated backup configured" -ForegroundColor Green
}
```

### Recovery Procedures

#### 1. Service Recovery
```powershell
function Recover-Service {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [string]$BackupPath
    )

    Write-Host "Starting service recovery for $ComputerName" -ForegroundColor Yellow

    # Stop service
    Stop-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -Force

    # Restore configuration
    if ($BackupPath -and (Test-Path $BackupPath)) {
        $configPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\Config"
        Copy-Item -Path "$BackupPath\*.config" -Destination $configPath -Force

        # Restore service info if available
        $serviceInfoPath = Join-Path $BackupPath "service_info.json"
        if (Test-Path $serviceInfoPath) {
            $serviceInfo = Get-Content $serviceInfoPath | ConvertFrom-Json

            # Restore service account
            $service = Get-WmiObject -Class Win32_Service -Filter "Name='LicenseReleaseService'" -ComputerName $ComputerName
            $service.Change($null, $null, $null, $null, $null, $null, $serviceInfo.Account, $null, $null, $null, $null)
        }
    }

    # Start service
    Start-Service -Name "LicenseReleaseService" -ComputerName $ComputerName

    # Verify service is running
    $service = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName
    if ($service.Status -eq "Running") {
        Write-Host "Service recovery completed successfully" -ForegroundColor Green
    }
    else {
        Write-Host "Service recovery failed" -ForegroundColor Red
    }
}
```

This Administrator Guide provides comprehensive coverage of all aspects of managing the Idle Detection system. Regular monitoring, proper configuration management, and proactive troubleshooting are essential for maintaining system health and ensuring optimal performance.