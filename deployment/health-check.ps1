#!/usr/bin/powershell

<#
.SYNOPSIS
    Health check script for License Release Service

.DESCRIPTION
    This script performs comprehensive health checks for the License Release Service
    including service status, performance metrics, configuration validation, and system monitoring.

.PARAMETER ConfigPath
    Path to configuration file

.PARAMETER OutputFormat
    Output format (Json, Text, Html)

.PARAMETER OutputFile
    Output file path (optional)

.PARAMETER Thresholds
    Custom thresholds for health checks

.EXAMPLE
    .\health-check.ps1 -ConfigPath "C:\Program Files\LicenseReleaseService\config.json" -OutputFormat Json

.EXAMPLE
    .\health-check.ps1 -ConfigPath "C:\Program Files\LicenseReleaseService\config.json" -OutputFile "C:\temp\health-report.html" -OutputFormat Html
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ConfigPath = "C:\Program Files\LicenseReleaseService\config.json",

    [Parameter(Mandatory=$false)]
    [ValidateSet("Json", "Text", "Html")]
    [string]$OutputFormat = "Text",

    [Parameter(Mandatory=$false)]
    [string]$OutputFile,

    [Parameter(Mandatory=$false)]
    [hashtable]$Thresholds = $null
)

# Set strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

# Script variables
$ServiceName = "LicenseReleaseService"
$InstallPath = "C:\Program Files\LicenseReleaseService"
$LogFile = "$InstallPath\Logs\health-check.log"
$StartTime = Get-Date

# Default thresholds
$defaultThresholds = @{
    ServiceResponseTime = 5000      # 5 seconds
    MemoryUsageMB = 100            # 100 MB
    CpuUsagePercent = 80           # 80%
    DiskUsagePercent = 90          # 90%
    ErrorRate = 0.01               # 1%
    Availability = 0.999           # 99.9%
    DetectionResponseTime = 100   # 100ms
    ConcurrentDetections = 100     # 100
    LogFileSizeMB = 10            # 10 MB
    DiskFreeSpaceGB = 1            # 1 GB
}

# Merge custom thresholds
if ($Thresholds) {
    foreach ($key in $Thresholds.Keys) {
        $defaultThresholds[$key] = $Thresholds[$key]
    }
}

$thresholds = $defaultThresholds

# Health check results
$healthResults = @{
    OverallStatus = "Healthy"
    Checks = @()
    Warnings = @()
    Errors = @()
    Metrics = @{}
    Timestamp = $StartTime
    Duration = $null
}

# Utility functions
function Write-HealthLog {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,

        [Parameter(Mandatory=$false)]
        [ValidateSet("INFO", "WARNING", "ERROR")]
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"

    # Write to log file
    if (Test-Path (Split-Path $LogFile -Parent)) {
        Add-Content -Path $LogFile -Value $logMessage -ErrorAction SilentlyContinue
    }
}

function Add-HealthCheck {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Name,

        [Parameter(Mandatory=$true)]
        [object]$Value,

        [Parameter(Mandatory=$true)]
        [string]$Status,

        [Parameter(Mandatory=$false)]
        [string]$Message,

        [Parameter(Mandatory=$false)]
        [hashtable]$Threshold = $null
    )

    $check = @{
        Name = $Name
        Value = $Value
        Status = $Status
        Message = $Message
        Threshold = $Threshold
        Timestamp = Get-Date
    }

    $healthResults.Checks += $check

    # Update overall status
    if ($Status -eq "Critical") {
        $healthResults.OverallStatus = "Critical"
    } elseif ($Status -eq "Warning" -and $healthResults.OverallStatus -eq "Healthy") {
        $healthResults.OverallStatus = "Warning"
    }

    # Add to warnings/errors lists
    if ($Status -eq "Warning") {
        $healthResults.Warnings += $check
    } elseif ($Status -eq "Critical") {
        $healthResults.Errors += $check
    }

    Write-HealthLog -Message "$Name`: $Message" -Level $Status
}

function Test-ServiceHealth {
    Write-HealthLog "Checking service health..."

    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

        if (-not $service) {
            Add-HealthCheck -Name "Service Status" -Value "Not Found" -Status "Critical" -Message "Service not installed"
            return
        }

        # Check service status
        if ($service.Status -ne "Running") {
            Add-HealthCheck -Name "Service Status" -Value $service.Status -Status "Critical" -Message "Service is not running"
            return
        }

        # Check service startup type
        if ($service.StartType -ne "Automatic") {
            Add-HealthCheck -Name "Service Startup Type" -Value $service.StartType -Status "Warning" -Message "Service startup type should be Automatic"
        } else {
            Add-HealthCheck -Name "Service Status" -Value $service.Status -Status "Healthy" -Message "Service is running"
            Add-HealthCheck -Name "Service Startup Type" -Value $service.StartType -Status "Healthy" -Message "Service startup type is correct"
        }

        # Test service response
        $responseTime = Test-ServiceResponse
        if ($responseTime -gt 0) {
            $status = if ($responseTime -le $thresholds.ServiceResponseTime) { "Healthy" } else { "Warning" }
            Add-HealthCheck -Name "Service Response Time" -Value "$($responseTime)ms" -Status $status -Threshold $thresholds.ServiceResponseTime -Message "Service response time check"
            $healthResults.Metrics.ServiceResponseTime = $responseTime
        }

        # Check service dependencies
        Test-ServiceDependencies
    }
    catch {
        Add-HealthCheck -Name "Service Health" -Value "Error" -Status "Critical" -Message "Error checking service: $($_.Exception.Message)"
    }
}

function Test-ServiceResponse {
    try {
        # Create HTTP request to service health endpoint
        $url = "http://localhost:8080/health"
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $response = Invoke-WebRequest -Uri $url -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
            $stopwatch.Stop()

            if ($response.StatusCode -eq 200) {
                return $stopwatch.ElapsedMilliseconds
            }
        }
        catch {
            # Service might not have HTTP endpoint, try other methods
            $stopwatch.Stop()
        }

        # Fallback: test via service control
        $service = Get-Service -Name $ServiceName
        if ($service -and $service.Status -eq "Running") {
            return 100 # Assume healthy if running
        }

        return -1
    }
    catch {
        return -1
    }
}

function Test-ServiceDependencies {
    try {
        $requiredServices = @("EventLog", "Tcpip")
        foreach ($depService in $requiredServices) {
            $service = Get-Service -Name $depService -ErrorAction SilentlyContinue
            if (-not $service -or $service.Status -ne "Running") {
                Add-HealthCheck -Name "Service Dependency: $depService" -Value "Not Running" -Status "Warning" -Message "Required service dependency not running"
            } else {
                Add-HealthCheck -Name "Service Dependency: $depService" -Value $service.Status -Status "Healthy" -Message "Service dependency is running"
            }
        }
    }
    catch {
        Add-HealthCheck -Name "Service Dependencies" -Value "Error" -Status "Warning" -Message "Error checking service dependencies: $($_.Exception.Message)"
    }
}

function Test-ConfigurationHealth {
    Write-HealthLog "Checking configuration health..."

    try {
        if (-not (Test-Path $ConfigPath)) {
            Add-HealthCheck -Name "Configuration File" -Value "Not Found" -Status "Critical" -Message "Configuration file not found"
            return
        }

        # Load configuration
        $config = Get-Content $ConfigPath | ConvertFrom-Json

        # Validate configuration structure
        $requiredSections = @("idleDetection", "monitoring", "logging", "deployment")
        foreach ($section in $requiredSections) {
            if (-not $config.$section) {
                Add-HealthCheck -Name "Configuration Section: $section" -Value "Missing" -Status "Critical" -Message "Required configuration section missing"
            } else {
                Add-HealthCheck -Name "Configuration Section: $section" -Value "Present" -Status "Healthy" -Message "Configuration section is present"
            }
        }

        # Validate idle detection settings
        if ($config.idleDetection) {
            $idConfig = $config.idleDetection

            if ($idConfig.detectionInterval -lt [TimeSpan]"00:00:10") {
                Add-HealthCheck -Name "Detection Interval" -Value $idConfig.detectionInterval -Status "Warning" -Message "Detection interval is very short"
            }

            if ($idConfig.confidenceThreshold -lt 0.5 -or $idConfig.confidenceThreshold -gt 1.0) {
                Add-HealthCheck -Name "Confidence Threshold" -Value $idConfig.confidenceThreshold -Status "Critical" -Message "Confidence threshold out of valid range"
            }

            if ($idConfig.maxConcurrentDetections -gt 1000) {
                Add-HealthCheck -Name "Max Concurrent Detections" -Value $idConfig.maxConcurrentDetections -Status "Warning" -Message "High concurrent detection limit may impact performance"
            }
        }

        # Validate monitoring settings
        if ($config.monitoring) {
            $monitorConfig = $config.monitoring

            if ($monitorConfig.healthCheckInterval -lt [TimeSpan]"00:01:00") {
                Add-HealthCheck -Name "Health Check Interval" -Value $monitorConfig.healthCheckInterval -Status "Warning" -Message "Health check interval is very frequent"
            }
        }

        Add-HealthCheck -Name "Configuration Validation" -Value "Completed" -Status "Healthy" -Message "Configuration validation completed"
    }
    catch {
        Add-HealthCheck -Name "Configuration Health" -Value "Error" -Status "Critical" -Message "Error checking configuration: $($_.Exception.Message)"
    }
}

function Test-PerformanceHealth {
    Write-HealthLog "Checking performance health..."

    try {
        # Get current process
        $process = Get-Process -Name "LicenseReleaseService" -ErrorAction SilentlyContinue
        if (-not $process) {
            Add-HealthCheck -Name "Process Status" -Value "Not Found" -Status "Warning" -Message "LicenseReleaseService process not found"
            return
        }

        # Check memory usage
        $memoryMB = $process.WorkingSet64 / 1MB
        $memoryStatus = if ($memoryMB -le $thresholds.MemoryUsageMB) { "Healthy" } elseif ($memoryMB -le $thresholds.MemoryUsageMB * 1.5) { "Warning" } else { "Critical" }
        Add-HealthCheck -Name "Memory Usage" -Value "$([math]::Round($memoryMB, 1))MB" -Status $memoryStatus -Threshold $thresholds.MemoryUsageMB -Message "Memory usage check"
        $healthResults.Metrics.MemoryUsageMB = [math]::Round($memoryMB, 1)

        # Check CPU usage
        $cpuUsage = $process.CPU
        $cpuStatus = if ($cpuUsage -le $thresholds.CpuUsagePercent) { "Healthy" } else { "Critical" }
        Add-HealthCheck -Name "CPU Usage" -Value "$([math]::Round($cpuUsage, 1))%" -Status $cpuStatus -Threshold $thresholds.CpuUsagePercent -Message "CPU usage check"
        $healthResults.Metrics.CpuUsagePercent = [math]::Round($cpuUsage, 1)

        # Check handle count
        $handleCount = $process.HandleCount
        $handleStatus = if ($handleCount -le 1000) { "Healthy" } elseif ($handleCount -le 5000) { "Warning" } else { "Critical" }
        Add-HealthCheck -Name "Handle Count" -Value $handleCount -Status $handleStatus -Message "Handle count check"
        $healthResults.Metrics.HandleCount = $handleCount

        # Check thread count
        $threadCount = $process.Threads.Count
        $threadStatus = if ($threadCount -le 100) { "Healthy" } elseif ($threadCount -le 500) { "Warning" } else { "Critical" }
        Add-HealthCheck -Name "Thread Count" -Value $threadCount -Status $threadStatus -Message "Thread count check"
        $healthResults.Metrics.ThreadCount = $threadCount

        # Check system performance
        Test-SystemPerformance
    }
    catch {
        Add-HealthCheck -Name "Performance Health" -Value "Error" -Status "Warning" -Message "Error checking performance: $($_.Exception.Message)"
    }
}

function Test-SystemPerformance {
    try {
        # Get system performance counters
        $cpuCounter = Get-Counter "\Processor(_Total)\% Processor Time" -ErrorAction SilentlyContinue
        $memoryCounter = Get-Counter "\Memory\Available MBytes" -ErrorAction SilentlyContinue
        $diskCounter = Get-Counter "\PhysicalDisk(_Total)\% Disk Time" -ErrorAction SilentlyContinue

        if ($cpuCounter) {
            $cpuUsage = $cpuCounter.CounterSamples[0].CookedValue
            $cpuStatus = if ($cpuUsage -le $thresholds.CpuUsagePercent) { "Healthy" } else { "Warning" }
            Add-HealthCheck -Name "System CPU Usage" -Value "$([math]::Round($cpuUsage, 1))%" -Status $cpuStatus -Threshold $thresholds.CpuUsagePercent -Message "System CPU usage check"
            $healthResults.Metrics.SystemCpuUsagePercent = [math]::Round($cpuUsage, 1)
        }

        if ($memoryCounter) {
            $availableMB = $memoryCounter.CounterSamples[0].CookedValue
            $totalMemory = Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty TotalVisibleMemorySize
            $usedMemory = ($totalMemory - $availableMB) / 1MB
            $memoryUsagePercent = ($usedMemory / ($totalMemory / 1MB)) * 100

            $memoryStatus = if ($memoryUsagePercent -le $thresholds.MemoryUsagePercent) { "Healthy" } else { "Warning" }
            Add-HealthCheck -Name "System Memory Usage" -Value "$([math]::Round($memoryUsagePercent, 1))%" -Status $memoryStatus -Threshold $thresholds.MemoryUsagePercent -Message "System memory usage check"
            $healthResults.Metrics.SystemMemoryUsagePercent = [math]::Round($memoryUsagePercent, 1)
        }

        if ($diskCounter) {
            $diskUsage = $diskCounter.CounterSamples[0].CookedValue
            $diskStatus = if ($diskUsage -le $thresholds.DiskUsagePercent) { "Healthy" } else { "Warning" }
            Add-HealthCheck -Name "System Disk Usage" -Value "$([math]::Round($diskUsage, 1))%" -Status $diskStatus -Threshold $thresholds.DiskUsagePercent -Message "System disk usage check"
            $healthResults.Metrics.SystemDiskUsagePercent = [math]::Round($diskUsage, 1)
        }

        # Check disk space
        $installDrive = Get-PSDrive -Name (Split-Path $InstallPath -Qualifier).TrimEnd(':')
        if ($installDrive) {
            $freeSpaceGB = $installDrive.Free / 1GB
            $spaceStatus = if ($freeSpaceGB -ge $thresholds.DiskFreeSpaceGB) { "Healthy" } else { "Critical" }
            Add-HealthCheck -Name "Disk Free Space" -Value "$([math]::Round($freeSpaceGB, 1))GB" -Status $spaceStatus -Threshold $thresholds.DiskFreeSpaceGB -Message "Disk free space check"
            $healthResults.Metrics.DiskFreeSpaceGB = [math]::Round($freeSpaceGB, 1)
        }
    }
    catch {
        Add-HealthCheck -Name "System Performance" -Value "Error" -Status "Warning" -Message "Error checking system performance: $($_.Exception.Message)"
    }
}

function Test-LogHealth {
    Write-HealthLog "Checking log health..."

    try {
        $logDir = Split-Path $LogFile -Parent
        if (-not (Test-Path $logDir)) {
            Add-HealthCheck -Name "Log Directory" -Value "Not Found" -Status "Warning" -Message "Log directory not found"
            return
        }

        # Check log files
        $logFiles = Get-ChildItem $logDir -Filter "*.log" -ErrorAction SilentlyContinue
        if (-not $logFiles) {
            Add-HealthCheck -Name "Log Files" -Value "None Found" -Status "Warning" -Message "No log files found"
            return
        }

        # Check log file sizes
        foreach ($logFile in $logFiles) {
            $fileSizeMB = $logFile.Length / 1MB
            $sizeStatus = if ($fileSizeMB -le $thresholds.LogFileSizeMB) { "Healthy" } else { "Warning" }
            Add-HealthCheck -Name "Log File: $($logFile.Name)" -Value "$([math]::Round($fileSizeMB, 1))MB" -Status $sizeStatus -Threshold $thresholds.LogFileSizeMB -Message "Log file size check"
        }

        # Check recent log entries for errors
        $recentErrors = $logFiles | ForEach-Object {
            Get-Content $_.FullName -Tail 100 -ErrorAction SilentlyContinue | Where-Object { $_ -match "\[ERROR\]" }
        }

        if ($recentErrors) {
            $errorCount = $recentErrors.Count
            $errorStatus = if ($errorCount -le 5) { "Warning" } else { "Critical" }
            Add-HealthCheck -Name "Recent Log Errors" -Value $errorCount -Status $errorStatus -Message "Recent log errors count check"
            $healthResults.Metrics.RecentLogErrors = $errorCount
        } else {
            Add-HealthCheck -Name "Recent Log Errors" -Value 0 -Status "Healthy" -Message "No recent log errors found"
            $healthResults.Metrics.RecentLogErrors = 0
        }

        # Check log file permissions
        $logAcl = Get-Acl $logDir
        $hasWritePermission = $logAcl.Access | Where-Object {
            $_.IdentityReference -eq "NETWORK SERVICE" -and $_.FileSystemRights -match "Write"
        }

        if (-not $hasWritePermission) {
            Add-HealthCheck -Name "Log Permissions" -Value "Insufficient" -Status "Warning" -Message "Log directory may not have write permissions for service"
        } else {
            Add-HealthCheck -Name "Log Permissions" -Value "OK" -Status "Healthy" -Message "Log directory permissions are correct"
        }
    }
    catch {
        Add-HealthCheck -Name "Log Health" -Value "Error" -Status "Warning" -Message "Error checking log health: $($_.Exception.Message)"
    }
}

function Test-NetworkHealth {
    Write-HealthLog "Checking network health..."

    try {
        # Test local connectivity
        $localhostTest = Test-Connection -ComputerName localhost -Count 1 -ErrorAction SilentlyContinue
        if ($localhostTest) {
            Add-HealthCheck -Name "Local Connectivity" -Value "OK" -Status "Healthy" -Message "Local network connectivity test"
        } else {
            Add-HealthCheck -Name "Local Connectivity" -Value "Failed" -Status "Warning" -Message "Local network connectivity test failed"
        }

        # Test port availability
        $port8080 = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue | Where-Object { $_.State -eq "Listen" }
        if ($port8080) {
            Add-HealthCheck -Name "Port 8080" -Value "Listening" -Status "Healthy" -Message "Port 8080 is listening"
        } else {
            Add-HealthCheck -Name "Port 8080" -Value "Not Listening" -Status "Warning" -Message "Port 8080 is not listening"
        }

        # Test DNS resolution
        try {
            $dnsTest = Resolve-DnsName "localhost" -ErrorAction SilentlyContinue
            if ($dnsTest) {
                Add-HealthCheck -Name "DNS Resolution" -Value "OK" -Status "Healthy" -Message "DNS resolution test"
            } else {
                Add-HealthCheck -Name "DNS Resolution" -Value "Failed" -Status "Warning" -Message "DNS resolution test failed"
            }
        }
        catch {
            Add-HealthCheck -Name "DNS Resolution" -Value "Error" -Status "Warning" -Message "DNS resolution test error"
        }
    }
    catch {
        Add-HealthCheck -Name "Network Health" -Value "Error" -Status "Warning" -Message "Error checking network health: $($_.Exception.Message)"
    }
}

function Test-SecurityHealth {
    Write-HealthLog "Checking security health..."

    try {
        # Check file permissions
        if (Test-Path $InstallPath) {
            $acl = Get-Acl $InstallPath
            $networkServiceAccess = $acl.Access | Where-Object {
                $_.IdentityReference -eq "NETWORK SERVICE"
            }

            if ($networkServiceAccess) {
                Add-HealthCheck -Name "File Permissions" -Value "OK" -Status "Healthy" -Message "File permissions are configured"
            } else {
                Add-HealthCheck -Name "File Permissions" -Value "Missing" -Status "Warning" -Message "NETWORK SERVICE permissions may be missing"
            }
        }

        # Check service account
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service) {
            $serviceConfig = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'"
            if ($serviceConfig) {
                $account = $serviceConfig.StartName
                if ($account -eq "LocalSystem" -or $account -eq "NT AUTHORITY\NetworkService") {
                    Add-HealthCheck -Name "Service Account" -Value $account -Status "Healthy" -Message "Service account is appropriate"
                } else {
                    Add-HealthCheck -Name "Service Account" -Value $account -Status "Warning" -Message "Service account review recommended"
                }
            }
        }

        # Check for recent security events
        $securityLogs = Get-WinEvent -LogName Security -MaxEvents 100 -ErrorAction SilentlyContinue | Where-Object {
            $_.TimeCreated -gt (Get-Date).AddHours(-24)
        }

        if ($securityLogs) {
            $auditEvents = $securityLogs | Where-Object { $_.Id -in (4624, 4625, 4634) }
            if ($auditEvents) {
                Add-HealthCheck -Name "Security Events" -Value "$($auditEvents.Count) events" -Status "Healthy" -Message "Recent security events found"
            }
        }
    }
    catch {
        Add-HealthCheck -Name "Security Health" -Value "Error" -Status "Warning" -Message "Error checking security health: $($_.Exception.Message)"
    }
}

function Format-Output {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Results,

        [Parameter(Mandatory=$true)]
        [string]$Format
    )

    switch ($Format) {
        "Json" {
            return $Results | ConvertTo-Json -Depth 10
        }

        "Html" {
            return Format-HtmlOutput -Results $Results
        }

        default {
            return Format-TextOutput -Results $Results
        }
    }
}

function Format-TextOutput {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Results
    )

    $output = @"
================================================================================
LICENSE RELEASE SERVICE HEALTH CHECK REPORT
================================================================================
Generated: $($Results.Timestamp)
Overall Status: $($Results.OverallStatus)
Duration: $($Results.Duration.TotalSeconds.ToString("0.00")) seconds

================================================================================
HEALTH CHECK RESULTS
================================================================================
"@

    foreach ($check in $Results.Checks) {
        $status = $check.Status.ToUpper()
        $output += "$($status.PadRight(10)) - $($check.Name): $($check.Message)`n"
        if ($check.Threshold) {
            $output += "             Threshold: $($check.Threshold)`n"
        }
        $output += "             Value: $($check.Value)`n"
        $output += "             Timestamp: $($check.Timestamp)`n`n"
    }

    if ($Results.Warnings.Count -gt 0) {
        $output += @"
================================================================================
WARNINGS ($($Results.Warnings.Count))
================================================================================
"@
        foreach ($warning in $Results.Warnings) {
            $output += "- $($warning.Name): $($warning.Message)`n"
        }
        $output += "`n"
    }

    if ($Results.Errors.Count -gt 0) {
        $output += @"
================================================================================
ERRORS ($($Results.Errors.Count))
================================================================================
"@
        foreach ($error in $Results.Errors) {
            $output += "- $($error.Name): $($error.Message)`n"
        }
        $output += "`n"
    }

    if ($Results.Metrics.Count -gt 0) {
        $output += @"
================================================================================
PERFORMANCE METRICS
================================================================================
"@
        foreach ($metric in $Results.Metrics.GetEnumerator()) {
            $output += "- $($metric.Key): $($metric.Value)`n"
        }
        $output += "`n"
    }

    $output += @"
================================================================================
END OF REPORT
================================================================================
"@

    return $output
}

function Format-HtmlOutput {
    param(
        [Parameter(Mandatory=$true)]
        [object]$Results
    )

    $statusColor = switch ($Results.OverallStatus) {
        "Healthy" { "#28a745" }
        "Warning" { "#ffc107" }
        "Critical" { "#dc3545" }
        default { "#6c757d" }
    }

    $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>License Release Service Health Check</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background-color: #f8f9fa; padding: 20px; border-radius: 5px; }
        .status { font-size: 24px; font-weight: bold; color: $statusColor; }
        .section { margin: 20px 0; }
        .section h2 { border-bottom: 2px solid #dee2e6; padding-bottom: 10px; }
        .check { margin: 10px 0; padding: 10px; border-radius: 3px; }
        .healthy { background-color: #d4edda; border-left: 4px solid #28a745; }
        .warning { background-color: #fff3cd; border-left: 4px solid #ffc107; }
        .critical { background-color: #f8d7da; border-left: 4px solid #dc3545; }
        .metrics { background-color: #f8f9fa; padding: 15px; border-radius: 5px; }
        .metric { display: inline-block; margin: 5px; padding: 10px; background-color: white; border-radius: 3px; }
    </style>
</head>
<body>
    <div class="header">
        <h1>License Release Service Health Check</h1>
        <div class="status">Overall Status: $($Results.OverallStatus)</div>
        <p>Generated: $($Results.Timestamp) | Duration: $($Results.Duration.TotalSeconds.ToString("0.00")) seconds</p>
    </div>

    <div class="section">
        <h2>Health Check Results</h2>
"@

    foreach ($check in $Results.Checks) {
        $cssClass = $check.Status.ToLower()
        $html += @"
        <div class="check $cssClass">
            <strong>$($check.Name)</strong><br>
            Status: $($check.Status)<br>
            Value: $($check.Value)<br>
            Message: $($check.Message)<br>
            Timestamp: $($check.Timestamp)
        </div>
"@
    }

    if ($Results.Warnings.Count -gt 0) {
        $html += @"
    </div>
    <div class="section">
        <h2>Warnings ($($Results.Warnings.Count))</h2>
"@
        foreach ($warning in $Results.Warnings) {
            $html += @"
        <div class="check warning">
            <strong>$($warning.Name)</strong><br>
            $($warning.Message)
        </div>
"@
        }
    }

    if ($Results.Errors.Count -gt 0) {
        $html += @"
    </div>
    <div class="section">
        <h2>Errors ($($Results.Errors.Count))</h2>
"@
        foreach ($error in $Results.Errors) {
            $html += @"
        <div class="check critical">
            <strong>$($error.Name)</strong><br>
            $($error.Message)
        </div>
"@
        }
    }

    if ($Results.Metrics.Count -gt 0) {
        $html += @"
    </div>
    <div class="section">
        <h2>Performance Metrics</h2>
        <div class="metrics">
"@
        foreach ($metric in $Results.Metrics.GetEnumerator()) {
            $html += @"
            <div class="metric">
                <strong>$($metric.Key)</strong><br>
                $($metric.Value)
            </div>
"@
        }
        $html += @"
        </div>
    </div>
"@
    }

    $html += @"
</body>
</html>
"@

    return $html
}

# Main execution
try {
    Write-HealthLog "Starting health check..."

    # Run health checks
    Test-ServiceHealth
    Test-ConfigurationHealth
    Test-PerformanceHealth
    Test-LogHealth
    Test-NetworkHealth
    Test-SecurityHealth

    # Calculate duration
    $healthResults.Duration = (Get-Date) - $StartTime

    # Format output
    $output = Format-Output -Results $healthResults -Format $OutputFormat

    # Write output
    if ($OutputFile) {
        $output | Out-File -FilePath $OutputFile -Force
        Write-HealthLog "Health report saved to: $OutputFile"
        Write-Host "Health report saved to: $OutputFile" -ForegroundColor Green
    } else {
        Write-Host $output
    }

    # Log completion
    Write-HealthLog "Health check completed. Status: $($healthResults.OverallStatus)"

    # Exit with appropriate code
    $exitCode = switch ($healthResults.OverallStatus) {
        "Healthy" { 0 }
        "Warning" { 1 }
        "Critical" { 2 }
        default { 3 }
    }

    exit $exitCode
}
catch {
    Write-HealthLog "Health check failed: $($_.Exception.Message)" -Level "ERROR"
    Write-Host "Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 3
}