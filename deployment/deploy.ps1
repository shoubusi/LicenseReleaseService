#!/usr/bin/powershell

<#
.SYNOPSIS
    Deployment script for License Release Service with Idle Detection

.DESCRIPTION
    This script deploys the License Release Service with comprehensive idle detection capabilities.
    It includes service installation, configuration, monitoring setup, and validation.

.PARAMETER Environment
    Target environment (Development, Staging, Production)

.PARAMETER ConfigPath
    Path to configuration file

.PARAMETER SkipServiceInstall
    Skip service installation (for configuration updates)

.PARAMETER Force
    Force deployment even if validation fails

.EXAMPLE
    .\deploy.ps1 -Environment Production -ConfigPath .\deployment\production-config.json

.EXAMPLE
    .\deploy.ps1 -Environment Staging -SkipServiceInstall
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment,

    [Parameter(Mandatory=$false)]
    [string]$ConfigPath = ".\deployment\production-config.json",

    [Parameter(Mandatory=$false)]
    [switch]$SkipServiceInstall,

    [Parameter(Mandatory=$false)]
    [switch]$Force
)

# Set strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Import required modules
Import-Module WebAdministration -ErrorAction SilentlyContinue

# Script variables
$ScriptName = "License Release Service Deployment"
$ScriptVersion = "1.0.0"
$LogFile = "deployment_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
$InstallPath = "C:\Program Files\LicenseReleaseService"
$BackupPath = "C:\Program Files\LicenseReleaseService\Backup"
$ServiceName = "LicenseReleaseService"
$EventLogName = "Application"
$EventLogSource = "LicenseReleaseService"

# Utility functions
function Write-Log {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,

        [Parameter(Mandatory=$false)]
        [ValidateSet("INFO", "WARNING", "ERROR", "SUCCESS")]
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"

    # Write to console
    switch ($Level) {
        "INFO"    { Write-Host $logMessage -ForegroundColor White }
        "WARNING" { Write-Host $logMessage -ForegroundColor Yellow }
        "ERROR"   { Write-Host $logMessage -ForegroundColor Red }
        "SUCCESS" { Write-Host $logMessage -ForegroundColor Green }
    }

    # Write to log file
    Add-Content -Path $LogFile -Value $logMessage

    # Write to event log
    if ($Level -eq "ERROR") {
        Write-EventLog -LogName $EventLogName -Source $EventLogSource -EntryType Error -EventId 1001 -Message $Message -ErrorAction SilentlyContinue
    }
}

function Test-Administrator {
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    catch {
        return $false
    }
}

function Test-Prerequisites {
    Write-Log "Checking deployment prerequisites..."

    # Check administrator privileges
    if (-not (Test-Administrator)) {
        throw "This script must be run with administrator privileges"
    }

    # Check .NET Framework
    $dotNetVersion = Get-ChildItem "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP" -Recurse |
                     Get-ItemProperty -Name Version -ErrorAction SilentlyContinue |
                     Where-Object { $_.PSChildName -match '^(?!S)\p{L}'} |
                     Sort-Object Version -Descending |
                     Select-Object -First 1

    if (-not $dotNetVersion -or [version]$dotNetVersion.Version -lt [version]"4.8") {
        throw ".NET Framework 4.8 or higher is required"
    }

    # Check PowerShell version
    if ($PSVersionTable.PSVersion.Major -lt 5) {
        throw "PowerShell 5.0 or higher is required"
    }

    # Check Windows Server version
    $osVersion = [Environment]::OSVersion.Version
    if ($osVersion.Major -lt 6 -or ($osVersion.Major -eq 6 -and $osVersion.Minor -lt 1)) {
        throw "Windows Server 2008 R2 or higher is required"
    }

    Write-Log "Prerequisites check completed successfully" -Level "SUCCESS"
}

function Backup-ExistingInstallation {
    Write-Log "Backing up existing installation..."

    if (Test-Path $InstallPath) {
        $backupFolder = "$BackupPath\$(Get-Date -Format 'yyyyMMdd_HHmmss')"

        if (-not (Test-Path $backupFolder)) {
            New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
        }

        # Stop service if running
        Stop-ServiceIfExists

        # Backup files
        Copy-Item "$InstallPath\*" -Destination $backupFolder -Recurse -Force

        # Backup configuration
        if (Test-Path "$InstallPath\config.json") {
            Copy-Item "$InstallPath\config.json" -Destination "$backupFolder\config.json.bak" -Force
        }

        Write-Log "Backup created at: $backupFolder" -Level "SUCCESS"
    }
}

function Stop-ServiceIfExists {
    Write-Log "Stopping service if running..."

    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Stop-Service -Name $ServiceName -Force
            Write-Log "Service stopped successfully"
        }
    }
}

function Remove-ServiceIfExists {
    Write-Log "Removing existing service if exists..."

    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        # Stop service first
        Stop-ServiceIfExists

        # Remove service
        sc.exe delete $ServiceName | Out-Null
        Write-Log "Service removed successfully"
    }
}

function Install-Service {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ExecutablePath,

        [Parameter(Mandatory=$true)]
        [hashtable]$Config
    )

    Write-Log "Installing Windows service..."

    $serviceConfig = $Config.deployment.serviceConfiguration

    # Create service
    $arguments = @(
        "create",
        $ServiceName,
        "binPath=`"$ExecutablePath`"",
        "DisplayName=`"$($serviceConfig.displayName)`"",
        "Description=`"$($serviceConfig.description)`"",
        "start=`"$(if ($serviceConfig.startType -eq "Automatic") { "auto" } else { "demand" })`""
    )

    if ($serviceConfig.dependencies) {
        $dependencies = $serviceConfig.dependencies -join "/"
        $arguments += "depend=`"$dependencies`""
    }

    if ($serviceConfig.account) {
        $arguments += "obj=`"$($serviceConfig.account)`""
    }

    # Execute sc.exe
    $result = & sc.exe $arguments 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create service: $result"
    }

    # Configure service recovery
    & sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

    Write-Log "Service installed successfully" -Level "SUCCESS"
}

function Install-Files {
    param(
        [Parameter(Mandatory=$true)]
        [string]$SourcePath,

        [Parameter(Mandatory=$true)]
        [hashtable]$Config
    )

    Write-Log "Installing application files..."

    # Create installation directory
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    }

    # Copy application files
    if (Test-Path $SourcePath) {
        Copy-Item "$SourcePath\*" -Destination $InstallPath -Recurse -Force
    }

    # Create required directories
    $directories = @(
        "$InstallPath\Logs",
        "$InstallPath\Config",
        "$InstallPath\Temp",
        "$InstallPath\Backups"
    )

    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }

    # Copy configuration
    Copy-Item $ConfigPath -Destination "$InstallPath\config.json" -Force

    Write-Log "Files installed successfully" -Level "SUCCESS"
}

function Setup-Monitoring {
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Config
    )

    Write-Log "Setting up monitoring..."

    # Create event log source
    if (-not [System.Diagnostics.EventLog]::SourceExists($EventLogSource)) {
        [System.Diagnostics.EventLog]::CreateEventSource($EventLogSource, $EventLogName)
        Write-Log "Event log source created successfully"
    }

    # Configure performance counters
    if ($Config.monitoring.enablePerformanceMonitoring) {
        # Create performance counter category
        if (-not [System.Diagnostics.PerformanceCounterCategory]::Exists("License Release Service")) {
            $counterCreationData = @(
                New-Object System.Diagnostics.CounterCreationData("Detections/sec", "Number of idle detections per second", [System.Diagnostics.PerformanceCounterType]::RateOfCountsPerSecond32),
                New-Object System.Diagnostics.CounterCreationData("Active Processes", "Number of actively monitored processes", [System.Diagnostics.PerformanceCounterType]::NumberOfItems32),
                New-Object System.Diagnostics.CounterCreationData("Memory Usage", "Current memory usage in MB", [System.Diagnostics.PerformanceCounterType]::NumberOfItems64),
                New-Object System.Diagnostics.CounterCreationData("Average Response Time", "Average detection response time in ms", [System.Diagnostics.PerformanceCounterType]::AverageTimer32),
                New-Object System.Diagnostics.CounterCreationData("Errors/sec", "Number of errors per second", [System.Diagnostics.PerformanceCounterType]::RateOfCountsPerSecond32)
            )

            $counterCollection = New-Object System.Diagnostics.CounterCreationDataCollection($counterCreationData)
            [System.Diagnostics.PerformanceCounterCategory]::Create("License Release Service", "License Release Service Performance Counters", $counterCollection)
            Write-Log "Performance counters created successfully"
        }
    }

    # Setup health check scheduled task
    if ($Config.monitoring.enableHealthMonitoring) {
        $taskName = "LicenseReleaseService_HealthCheck"
        $taskAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-File `"$InstallPath\health-check.ps1`""
        $taskTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 5)
        $taskSettings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd -AllowStartIfOnBatteries

        Register-ScheduledTask -TaskName $taskName -Action $taskAction -Trigger $taskTrigger -Settings $taskSettings -Force | Out-Null
        Write-Log "Health check scheduled task created successfully"
    }

    Write-Log "Monitoring setup completed successfully" -Level "SUCCESS"
}

function Set-Permissions {
    Write-Log "Setting up file and registry permissions..."

    # Set directory permissions
    $acl = Get-Acl $InstallPath

    # Add NETWORK SERVICE permissions
    $networkServiceRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "NETWORK SERVICE",
        "ReadAndExecute,Write",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($networkServiceRule)

    # Add Administrators permissions
    $adminRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "Administrators",
        "FullControl",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($adminRule)

    Set-Acl $InstallPath $acl

    # Set permissions for Logs directory
    $logsAcl = Get-Acl "$InstallPath\Logs"
    $logsAcl.AddAccessRule($networkServiceRule)
    $logsAcl.AddAccessRule($adminRule)
    Set-Acl "$InstallPath\Logs" $logsAcl

    Write-Log "Permissions configured successfully" -Level "SUCCESS"
}

function Test-Configuration {
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Config
    )

    Write-Log "Validating configuration..."

    $errors = @()

    # Validate required settings
    if (-not $Config.idleDetection.isEnabled) {
        $errors += "Idle detection must be enabled"
    }

    if ($Config.idleDetection.detectionInterval -lt [TimeSpan]"00:00:10") {
        $errors += "Detection interval must be at least 10 seconds"
    }

    if ($Config.idleDetection.confidenceThreshold -lt 0.5 -or $Config.idleDetection.confidenceThreshold -gt 1.0) {
        $errors += "Confidence threshold must be between 0.5 and 1.0"
    }

    # Validate file paths
    if (-not (Test-Path $InstallPath)) {
        $errors += "Installation path does not exist: $InstallPath"
    }

    if (-not (Test-Path $ConfigPath)) {
        $errors += "Configuration file does not exist: $ConfigPath"
    }

    # Validate service configuration
    if ($Config.deployment.serviceConfiguration.startType -notin @("Automatic", "Manual", "Disabled")) {
        $errors += "Invalid service start type"
    }

    if ($errors.Count -gt 0) {
        throw "Configuration validation failed:$($errors | ForEach-Object { "`n  - $_" })"
    }

    Write-Log "Configuration validation completed successfully" -Level "SUCCESS"
}

function Start-Service {
    Write-Log "Starting service..."

    # Start service
    Start-Service -Name $ServiceName

    # Wait for service to start
    $timeout = 30
    $startTime = Get-Date

    while (((Get-Date) - $startTime).TotalSeconds -lt $timeout) {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service -and $service.Status -eq "Running") {
            Write-Log "Service started successfully" -Level "SUCCESS"
            return
        }
        Start-Sleep -Seconds 1
    }

    throw "Service failed to start within $timeout seconds"
}

function Test-Deployment {
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Config
    )

    Write-Log "Testing deployment..."

    $errors = @()

    # Test service status
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        $errors += "Service not found"
    } elseif ($service.Status -ne "Running") {
        $errors += "Service is not running (Status: $($service.Status))"
    }

    # Test configuration file
    if (-not (Test-Path "$InstallPath\config.json")) {
        $errors += "Configuration file not found"
    }

    # Test log directory
    if (-not (Test-Path "$InstallPath\Logs")) {
        $errors += "Logs directory not found"
    }

    # Test event log source
    if (-not [System.Diagnostics.EventLog]::SourceExists($EventLogSource)) {
        $errors += "Event log source not found"
    }

    # Test performance counters
    if ($Config.monitoring.enablePerformanceMonitoring) {
        if (-not [System.Diagnostics.PerformanceCounterCategory]::Exists("License Release Service")) {
            $errors += "Performance counters not found"
        }
    }

    # Test health check task
    if ($Config.monitoring.enableHealthMonitoring) {
        $task = Get-ScheduledTask -TaskName "LicenseReleaseService_HealthCheck" -ErrorAction SilentlyContinue
        if (-not $task) {
            $errors += "Health check scheduled task not found"
        }
    }

    if ($errors.Count -gt 0) {
        if ($Force) {
            Write-Log "Deployment test failed but continuing due to -Force parameter" -Level "WARNING"
            foreach ($error in $errors) {
                Write-Log "  - $error" -Level "WARNING"
            }
        } else {
            throw "Deployment test failed:$($errors | ForEach-Object { "`n  - $_" })"
        }
    }

    Write-Log "Deployment test completed successfully" -Level "SUCCESS"
}

function Rollback-Deployment {
    Write-Log "Rolling back deployment..."

    if (Test-Path $BackupPath) {
        # Get latest backup
        $latestBackup = Get-ChildItem $BackupPath | Sort-Object LastWriteTime -Descending | Select-Object -First 1

        if ($latestBackup) {
            # Stop service
            Stop-ServiceIfExists

            # Restore files
            Copy-Item "$($latestBackup.FullName)\*" -Destination $InstallPath -Recurse -Force

            # Start service
            Start-Service

            Write-Log "Rollback completed successfully" -Level "SUCCESS"
        } else {
            Write-Log "No backup found for rollback" -Level "WARNING"
        }
    } else {
        Write-Log "Backup directory not found" -Level "WARNING"
    }
}

# Main execution
try {
    Write-Log "Starting $ScriptName v$ScriptVersion"
    Write-Log "Environment: $Environment"
    Write-Log "Configuration: $ConfigPath"

    # Check prerequisites
    Test-Prerequisites

    # Load configuration
    Write-Log "Loading configuration..."
    if (-not (Test-Path $ConfigPath)) {
        throw "Configuration file not found: $ConfigPath"
    }

    $config = Get-Content $ConfigPath | ConvertFrom-Json

    # Validate configuration
    Test-Configuration -Config $config

    # Backup existing installation
    Backup-ExistingInstallation

    # Install files
    Install-Files -SourcePath "..\bin\Release" -Config $config

    # Setup monitoring
    Setup-Monitoring -Config $config

    # Set permissions
    Set-Permissions

    # Install service if not skipped
    if (-not $SkipServiceInstall) {
        Remove-ServiceIfExists
        Install-Service -ExecutablePath "$InstallPath\LicenseReleaseService.exe" -Config $config
    }

    # Start service
    Start-Service

    # Test deployment
    Test-Deployment -Config $config

    Write-Log "Deployment completed successfully!" -Level "SUCCESS"
    Write-Log "Service: $ServiceName"
    Write-Log "Installation Path: $InstallPath"
    Write-Log "Configuration: $ConfigPath"

    exit 0
}
catch {
    Write-Log "Deployment failed: $($_.Exception.Message)" -Level "ERROR"
    Write-Log "Stack trace: $($_.ScriptStackTrace)" -Level "ERROR"

    # Attempt rollback
    try {
        Rollback-Deployment
    }
    catch {
        Write-Log "Rollback failed: $($_.Exception.Message)" -Level "ERROR"
    }

    exit 1
}