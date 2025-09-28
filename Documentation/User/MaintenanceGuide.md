# License Release Service - Maintenance Guide

## Overview

This guide provides comprehensive maintenance procedures for the Idle Detection system. Regular maintenance is essential for ensuring optimal performance, reliability, and security of the system.

## Maintenance Schedule

### Routine Maintenance Tasks

#### Daily Tasks
- **Monitor System Health**: Check service status and performance metrics
- **Review Alerts**: Investigate and resolve any critical alerts
- **Check Log Files**: Review application and system logs for errors
- **Monitor Resource Usage**: Ensure CPU, memory, and disk usage are within normal ranges
- **Backup Verification**: Confirm that automated backups are running successfully

#### Weekly Tasks
- **Performance Analysis**: Review performance trends and identify potential issues
- **Configuration Review**: Check for configuration drift and ensure settings are optimal
- **Security Audit**: Review security logs and access controls
- **Capacity Planning**: Analyze usage patterns and plan for future capacity needs
- **Update Management**: Check for and apply security patches and updates

#### Monthly Tasks
- **Comprehensive System Review**: Full health assessment of all components
- **Database Maintenance**: Perform database optimization and cleanup
- **Configuration Backup**: Create full configuration backups
- **Disaster Recovery Testing**: Test backup and recovery procedures
- **User Access Review**: Review and update user access permissions

#### Quarterly Tasks
- **Major Version Updates**: Apply major software updates and patches
- **Performance Benchmarking**: Run comprehensive performance tests
- **Security Assessment**: Conduct thorough security review and penetration testing
- **Documentation Updates**: Update all documentation to reflect current system state
- **Capacity Planning Review**: Update capacity plans based on usage trends

### Maintenance Calendar

| Task | Frequency | Time Required | Priority | Responsible Party |
|------|-----------|---------------|----------|-------------------|
| Health Monitoring | Daily | 15 minutes | High | System Administrator |
| Alert Review | Daily | 30 minutes | High | System Administrator |
| Performance Analysis | Weekly | 1 hour | Medium | System Administrator |
| Configuration Review | Weekly | 1 hour | Medium | System Administrator |
| Security Audit | Weekly | 2 hours | High | Security Team |
| Database Maintenance | Monthly | 2 hours | High | Database Administrator |
| Backup Verification | Monthly | 1 hour | High | System Administrator |
| System Review | Quarterly | 4 hours | Medium | System Administrator |
| Security Assessment | Quarterly | 8 hours | High | Security Team |
| Documentation Update | Quarterly | 4 hours | Medium | Technical Writer |

## System Maintenance Procedures

### 1. Service Maintenance

#### Service Restart Procedures
```powershell
function Restart-LicenseReleaseService {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [switch]$Graceful,
        [switch]$Force
    )

    Write-Host "Starting service restart procedure for $ComputerName" -ForegroundColor Yellow

    try {
        # Get service status
        $service = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName

        if ($Graceful -and $service.Status -eq "Running") {
            Write-Host "Performing graceful shutdown..." -ForegroundColor Cyan

            # Notify users if possible
            try {
                Invoke-RestMethod -Uri "http://$ComputerName:8080/api/admin/notify-maintenance" -Method Post -Body @{
                    message = "System maintenance in progress. Service will restart shortly."
                    duration = 300
                } -ErrorAction SilentlyContinue
            }
            catch {
                Write-Warning "Unable to send maintenance notification"
            }

            # Wait for graceful shutdown period
            Start-Sleep -Seconds 60
        }

        if ($Force) {
            Write-Host "Force stopping service..." -ForegroundColor Red
            Stop-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -Force
        }
        else {
            Write-Host "Stopping service..." -ForegroundColor Cyan
            Stop-Service -Name "LicenseReleaseService" -ComputerName $ComputerName
        }

        # Verify service stopped
        $timeout = 30
        $stopped = $false
        for ($i = 0; $i -lt $timeout; $i++) {
            $currentService = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -ErrorAction SilentlyContinue
            if ($currentService.Status -eq "Stopped") {
                $stopped = $true
                break
            }
            Start-Sleep -Seconds 1
        }

        if (-not $stopped) {
            Write-Warning "Service did not stop gracefully, forcing stop"
            Stop-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -Force
        }

        Write-Host "Service stopped successfully" -ForegroundColor Green

        # Perform maintenance tasks
        Write-Host "Performing maintenance tasks..." -ForegroundColor Cyan
        Invoke-MaintenanceTasks -ComputerName $ComputerName

        # Start service
        Write-Host "Starting service..." -ForegroundColor Cyan
        Start-Service -Name "LicenseReleaseService" -ComputerName $ComputerName

        # Verify service started
        $started = $false
        for ($i = 0; $i -lt $timeout; $i++) {
            $currentService = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -ErrorAction SilentlyContinue
            if ($currentService.Status -eq "Running") {
                $started = $true
                break
            }
            Start-Sleep -Seconds 1
        }

        if ($started) {
            Write-Host "Service started successfully" -ForegroundColor Green

            # Verify health
            $health = Get-ServiceHealth -ComputerName $ComputerName
            if ($health.IsHealthy) {
                Write-Host "Service health check passed" -ForegroundColor Green
            }
            else {
                Write-Warning "Service health check failed: $($health.HealthMessage)"
            }
        }
        else {
            throw "Service failed to start"
        }
    }
    catch {
        Write-Error "Service restart failed: $($_.Exception.Message)"
        throw
    }
}

function Invoke-MaintenanceTasks {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    Write-Host "Executing maintenance tasks..." -ForegroundColor Cyan

    # Clean up old log files
    Clean-LogFiles -ComputerName $ComputerName

    # Clear temporary files
    Clear-TemporaryFiles -ComputerName $ComputerName

    # Verify configuration
    Test-Configuration -ComputerName $ComputerName

    # Check disk space
    Check-DiskSpace -ComputerName $ComputerName

    Write-Host "Maintenance tasks completed" -ForegroundColor Green
}
```

#### Service Health Check
```powershell
function Invoke-ComprehensiveHealthCheck {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    Write-Host "Performing comprehensive health check for $ComputerName" -ForegroundColor Yellow

    $healthReport = @{
        ComputerName = $ComputerName
        Timestamp = Get-Date
        Checks = @()
        OverallHealth = "Healthy"
        Recommendations = @()
    }

    # Service Health Check
    Write-Host "Checking service health..." -ForegroundColor Cyan
    $serviceHealth = Get-ServiceHealth -ComputerName $ComputerName
    $healthReport.Checks += @{
        Name = "Service Health"
        Status = if ($serviceHealth.IsHealthy) { "Healthy" } else { "Unhealthy" }
        Details = $serviceHealth.HealthMessage
        Critical = -not $serviceHealth.IsHealthy
    }

    if (-not $serviceHealth.IsHealthy) {
        $healthReport.OverallHealth = "Unhealthy"
        $healthReport.Recommendations += "Investigate service health issues: $($serviceHealth.HealthMessage)"
    }

    # Resource Usage Check
    Write-Host "Checking resource usage..." -ForegroundColor Cyan
    $resources = Get-SystemResourceMetrics -ComputerName $ComputerName -SampleCount 5
    if ($resources) {
        $cpuUsage = $resources | Where-Object { $_.Counter -like "*% Processor Time" -and $_.Instance -eq "_Total" } | Select-Object -ExpandProperty Average
        $memoryUsage = $resources | Where-Object { $_.Counter -like "*Private Bytes" } | Select-Object -ExpandProperty Average

        $healthReport.Checks += @{
            Name = "CPU Usage"
            Status = if ($cpuUsage -lt 80) { "Healthy" } elseif ($cpuUsage -lt 90) { "Warning" } else { "Critical" }
            Details = "Average CPU usage: $([Math]::Round($cpuUsage, 1))%"
            Critical = $cpuUsage -ge 90
        }

        $healthReport.Checks += @{
            Name = "Memory Usage"
            Status = if ($memoryUsage -lt 500MB) { "Healthy" } elseif ($memoryUsage -lt 1000MB) { "Warning" } else { "Critical" }
            Details = "Average memory usage: $([Math]::Round($memoryUsage / 1MB, 1))MB"
            Critical = $memoryUsage -ge 1000MB
        }

        if ($cpuUsage -ge 80) {
            $healthReport.OverallHealth = if ($cpuUsage -ge 90) { "Critical" } else { "Warning" }
            $healthReport.Recommendations += "High CPU usage detected. Consider increasing detection interval or reducing monitoring frequency."
        }

        if ($memoryUsage -ge 500MB) {
            $healthReport.OverallHealth = if ($memoryUsage -ge 1000MB) { "Critical" } else { "Warning" }
            $healthReport.Recommendations += "High memory usage detected. Consider reducing activity history size or enabling garbage collection optimization."
        }
    }

    # Performance Check
    Write-Host "Checking performance metrics..." -ForegroundColor Cyan
    $performance = Get-DetectionSuccessMetrics -ComputerName $ComputerName -StartTime (Get-Date).AddHours(-1)
    if ($performance) {
        $healthReport.Checks += @{
            Name = "Detection Success Rate"
            Status = if ($performance.SuccessRate -ge 0.9) { "Healthy" } elseif ($performance.SuccessRate -ge 0.8) { "Warning" } else { "Critical" }
            Details = "Success rate: $([Math]::Round($performance.SuccessRate * 100, 1))%"
            Critical = $performance.SuccessRate -lt 0.8
        }

        if ($performance.SuccessRate -lt 0.9) {
            $healthReport.OverallHealth = if ($performance.SuccessRate -lt 0.8) { "Critical" } else { "Warning" }
            $healthReport.Recommendations += "Low detection success rate detected. Review detector configurations and system health."
        }
    }

    # Disk Space Check
    Write-Host "Checking disk space..." -ForegroundColor Cyan
    $diskSpace = Get-WmiObject -Class Win32_LogicalDisk -ComputerName $ComputerName -Filter "DriveType=3" | Select-Object DeviceID, Size, FreeSpace
    foreach ($disk in $diskSpace) {
        $freeSpacePercent = ($disk.FreeSpace / $disk.Size) * 100
        $driveLetter = $disk.DeviceID

        $healthReport.Checks += @{
            Name = "Disk Space - $driveLetter"
            Status = if ($freeSpacePercent -ge 20) { "Healthy" } elseif ($freeSpacePercent -ge 10) { "Warning" } else { "Critical" }
            Details = "Free space: $([Math]::Round($freeSpacePercent, 1))%"
            Critical = $freeSpacePercent -lt 10
        }

        if ($freeSpacePercent -lt 20) {
            $healthReport.OverallHealth = if ($freeSpacePercent -lt 10) { "Critical" } else { "Warning" }
            $healthReport.Recommendations += "Low disk space on $driveLetter. Clean up old files or increase disk capacity."
        }
    }

    # Configuration Check
    Write-Host "Checking configuration..." -ForegroundColor Cyan
    $configValid = Test-Configuration -ComputerName $ComputerName
    $healthReport.Checks += @{
        Name = "Configuration"
        Status = if ($configValid) { "Healthy" } else { "Critical" }
        Details = if ($configValid) { "Configuration is valid" } else { "Configuration validation failed" }
        Critical = -not $configValid
    }

    if (-not $configValid) {
        $healthReport.OverallHealth = "Critical"
        $healthReport.Recommendations += "Configuration validation failed. Review configuration files and fix errors."
    }

    # Generate report
    $reportFile = "C:\Logs\LicenseReleaseService\HealthReports\HealthReport_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
    $reportDir = Split-Path $reportFile -Parent
    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force
    }

    $healthReport | ConvertTo-Json -Depth 5 | Out-File $reportFile

    Write-Host "Health check completed. Overall status: $($healthReport.OverallHealth)" -ForegroundColor $($healthReport.OverallHealth -eq "Healthy" ? "Green" : "Yellow")
    Write-Host "Report saved to: $reportFile" -ForegroundColor Cyan

    return $healthReport
}
```

### 2. Configuration Maintenance

#### Configuration Validation and Update
```powershell
function Test-Configuration {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    Write-Host "Validating configuration on $ComputerName" -ForegroundColor Cyan

    $configPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\app.config"
    if (-not (Test-Path $configPath)) {
        Write-Error "Configuration file not found: $configPath"
        return $false
    }

    try {
        # Test XML validity
        $config = [xml](Get-Content $configPath)
        Write-Host "Configuration file is valid XML" -ForegroundColor Green

        # Test configuration sections
        $validationResults = @()

        # Test idle detection configuration
        if ($config.Configuration.idleDetection) {
            $idleDetectionConfig = $config.Configuration.idleDetection

            # Validate thresholds
            if ($idleDetectionConfig.timeBasedDetection) {
                $timeConfig = $idleDetectionConfig.timeBasedDetection
                if ([int]$timeConfig.warningThresholdMinutes -ge [int]$timeConfig.imminentThresholdMinutes) {
                    $validationResults += "Warning threshold must be less than imminent threshold"
                }
                if ([int]$timeConfig.imminentThresholdMinutes -ge [int]$timeConfig.criticalThresholdMinutes) {
                    $validationResults += "Imminent threshold must be less than critical threshold"
                }
            }

            # Validate consensus weights
            if ($idleDetectionConfig.consensusEngine) {
                $consensusConfig = $idleDetectionConfig.consensusEngine
                $totalWeight = [double]$consensusConfig.timeBasedWeight +
                               [double]$consensusConfig.pingBasedWeight +
                               [double]$consensusConfig.activityMonitoringWeight

                if ([Math]::Abs($totalWeight - 1.0) -gt 0.01) {
                    $validationResults += "Consensus weights must sum to 1.0 (current: $totalWeight)"
                }
            }
        }

        # Test time formats
        if ($config.Configuration.idleDetection.timeBasedDetection) {
            $timeConfig = $config.Configuration.idleDetection.timeBasedDetection
            if (-not [TimeSpan]::TryParse($timeConfig.workHourStart, [ref]$null)) {
                $validationResults += "Invalid work hour start format: $($timeConfig.workHourStart)"
            }
            if (-not [TimeSpan]::TryParse($timeConfig.workHourEnd, [ref]$null)) {
                $validationResults += "Invalid work hour end format: $($timeConfig.workHourEnd)"
            }
        }

        # Report results
        if ($validationResults.Count -eq 0) {
            Write-Host "Configuration validation passed" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "Configuration validation failed:" -ForegroundColor Red
            foreach ($error in $validationResults) {
                Write-Host "  - $error" -ForegroundColor Red
            }
            return $false
        }
    }
    catch {
        Write-Error "Configuration validation error: $($_.Exception.Message)"
        return $false
    }
}

function Update-Configuration {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [hashtable]$ConfigurationUpdates,
        [switch]$Backup
    )

    Write-Host "Updating configuration on $ComputerName" -ForegroundColor Yellow

    $configPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\app.config"
    $backupPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\Backup"

    if (-not (Test-Path $configPath)) {
        throw "Configuration file not found: $configPath"
    }

    try {
        # Stop service
        Write-Host "Stopping service..." -ForegroundColor Cyan
        Stop-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -Force

        # Create backup
        if ($Backup) {
            if (-not (Test-Path $backupPath)) {
                New-Item -ItemType Directory -Path $backupPath -Force
            }
            $backupFile = Join-Path $backupPath "app.config.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
            Copy-Item -Path $configPath -Destination $backupFile
            Write-Host "Configuration backed up to: $backupFile" -ForegroundColor Green
        }

        # Load configuration
        $config = [xml](Get-Content $configPath)

        # Apply updates
        foreach ($update in $ConfigurationUpdates.GetEnumerator()) {
            $section, $property = $update.Key.Split('.')
            $value = $update.Value

            Write-Host "Updating $section.$property = $value" -ForegroundColor Cyan

            # Navigate to the correct node
            $node = $config
            foreach ($part in $section.Split('.')) {
                $node = $node.SelectSingleNode($part)
                if (-not $node) {
                    throw "Configuration section not found: $section"
                }
            }

            # Update property
            if ($node.Attributes[$property]) {
                $node.Attributes[$property].Value = $value
            }
            else {
                $node.SetAttribute($property, $value)
            }
        }

        # Save configuration
        $config.Save($configPath)
        Write-Host "Configuration updated successfully" -ForegroundColor Green

        # Validate configuration
        if (-not (Test-Configuration -ComputerName $ComputerName)) {
            Write-Warning "Configuration validation failed, restoring from backup"
            if ($Backup) {
                Copy-Item -Path $backupFile -Destination $configPath -Force
            }
            throw "Configuration validation failed after update"
        }

        # Start service
        Write-Host "Starting service..." -ForegroundColor Cyan
        Start-Service -Name "LicenseReleaseService" -ComputerName $ComputerName

        # Verify service is running
        $service = Get-Service -Name "LicenseReleaseService" -ComputerName $ComputerName
        if ($service.Status -eq "Running") {
            Write-Host "Service started successfully" -ForegroundColor Green
        }
        else {
            throw "Service failed to start after configuration update"
        }
    }
    catch {
        Write-Error "Configuration update failed: $($_.Exception.Message)"
        throw
    }
}
```

### 3. Database Maintenance

#### Database Health Check and Optimization
```powershell
function Invoke-DatabaseMaintenance {
    param (
        [string]$ServerName = $env:COMPUTERNAME,
        [string]$DatabaseName = "LicenseReleaseService",
        [switch]$RebuildIndexes,
        [switch]$UpdateStatistics,
        [switch]$CleanOldData
    )

    Write-Host "Performing database maintenance on $ServerName.$DatabaseName" -ForegroundColor Yellow

    try {
        # Load SQL Server module
        Import-Module SqlServer -ErrorAction Stop

        $connectionString = "Server=$ServerName;Database=$DatabaseName;Integrated Security=True;"

        # Check database health
        Write-Host "Checking database health..." -ForegroundColor Cyan
        $dbHealth = Invoke-Sqlcmd -ConnectionString $connectionString -Query @"
            SELECT
                name AS DatabaseName,
                state_desc AS State,
                recovery_model_desc AS RecoveryModel,
                compatibility_level AS CompatibilityLevel,
                page_verify_option_desc AS PageVerifyOption
            FROM sys.databases
            WHERE name = '$DatabaseName'
"@

        Write-Host "Database Status: $($dbHealth.State)" -ForegroundColor $(if ($dbHealth.State -eq "ONLINE") { "Green" } else { "Red" })

        # Check index fragmentation
        Write-Host "Checking index fragmentation..." -ForegroundColor Cyan
        $fragmentation = Invoke-Sqlcmd -ConnectionString $connectionString -Query @"
            SELECT
                OBJECT_NAME(i.object_id) AS TableName,
                i.name AS IndexName,
                ips.avg_fragmentation_in_percent AS FragmentationPercent,
                ips.page_count AS PageCount
            FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
            JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
            WHERE ips.avg_fragmentation_in_percent > 5
            ORDER BY ips.avg_fragmentation_in_percent DESC
"@

        if ($fragmentation.Count -gt 0) {
            Write-Host "Found $($fragmentation.Count) fragmented indexes" -ForegroundColor Yellow

            if ($RebuildIndexes) {
                Write-Host "Rebuilding fragmented indexes..." -ForegroundColor Cyan
                foreach ($index in $fragmentation) {
                    $tableName = $index.TableName
                    $indexName = $index.IndexName
                    $fragmentationPercent = $index.FragmentationPercent

                    Write-Host "Rebuilding index $indexName on table $tableName ($fragmentationPercent% fragmented)" -ForegroundColor Cyan

                    $rebuildQuery = if ($fragmentationPercent -gt 30) {
                        "ALTER INDEX [$indexName] ON [$tableName] REBUILD WITH (ONLINE = OFF)"
                    } else {
                        "ALTER INDEX [$indexName] ON [$tableName] REORGANIZE"
                    }

                    Invoke-Sqlcmd -ConnectionString $connectionString -Query $rebuildQuery -ErrorAction Stop
                }
                Write-Host "Index rebuild completed" -ForegroundColor Green
            }
        }
        else {
            Write-Host "No fragmented indexes found" -ForegroundColor Green
        }

        # Update statistics
        if ($UpdateStatistics) {
            Write-Host "Updating database statistics..." -ForegroundColor Cyan
            Invoke-Sqlcmd -ConnectionString $connectionString -Query "EXEC sp_updatestats" -ErrorAction Stop
            Write-Host "Statistics updated successfully" -ForegroundColor Green
        }

        # Clean old data
        if ($CleanOldData) {
            Write-Host "Cleaning old data..." -ForegroundColor Cyan

            # Clean old session records (older than 90 days)
            $cleanupQuery = @"
                DELETE FROM SessionRecords
                WHERE Timestamp < DATEADD(DAY, -90, GETDATE())

                DELETE FROM DetectionEvents
                WHERE EventTime < DATEADD(DAY, -90, GETDATE())

                DELETE FROM SystemLogs
                WHERE LogTime < DATEADD(DAY, -30, GETDATE())
"@

            $rowsAffected = Invoke-Sqlcmd -ConnectionString $connectionString -Query $cleanupQuery
            Write-Host "Cleaned up old data" -ForegroundColor Green
        }

        # Check database size and growth
        Write-Host "Checking database size and growth..." -ForegroundColor Cyan
        $dbSize = Invoke-Sqlcmd -ConnectionString $connectionString -Query @"
            SELECT
                name AS FileName,
                size/128.0 AS SizeMB,
                size/128.0 - CAST(FILEPROPERTY(name, 'SpaceUsed') AS int)/128.0 AS FreeSpaceMB,
                growth AS Growth,
                CASE is_percent_growth
                    WHEN 1 THEN 'Percentage'
                    ELSE 'MB'
                END AS GrowthUnit
            FROM sys.database_files
"@

        foreach ($file in $dbSize) {
            $freeSpacePercent = ($file.FreeSpaceMB / $file.SizeMB) * 100
            Write-Host "$($file.FileName): $($file.SizeMB.ToString('N1'))MB, Free: $($file.FreeSpaceMB.ToString('N1'))MB ($([Math]::Round($freeSpacePercent, 1))%)" -ForegroundColor Cyan
        }

        # Shrink database if needed (only if significant free space)
        $totalSizeMB = $dbSize | Measure-Object -Property SizeMB -Sum | Select-Object -ExpandProperty Sum
        $totalFreeSpaceMB = $dbSize | Measure-Object -Property FreeSpaceMB -Sum | Select-Object -ExpandProperty Sum
        $freeSpacePercent = ($totalFreeSpaceMB / $totalSizeMB) * 100

        if ($freeSpacePercent -gt 50) {
            Write-Host "Database has $([Math]::Round($freeSpacePercent, 1))% free space, considering shrink" -ForegroundColor Yellow
            # Note: Shrinking should be done carefully and only when necessary
            # Invoke-Sqlcmd -ConnectionString $connectionString -Query "DBCC SHRINKDATABASE ($DatabaseName)" -ErrorAction Stop
            # Write-Host "Database shrunk successfully" -ForegroundColor Green
        }

        Write-Host "Database maintenance completed successfully" -ForegroundColor Green
    }
    catch {
        Write-Error "Database maintenance failed: $($_.Exception.Message)"
        throw
    }
}
```

### 4. Log File Management

#### Log Rotation and Cleanup
```powershell
function Manage-LogFiles {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [string]$LogPath = "C:\Logs\LicenseReleaseService",
        [int]$RetentionDays = 30,
        [int]$MaxSizeMB = 1000,
        [switch]$CompressOldLogs,
        [switch]$WhatIf
    )

    Write-Host "Managing log files on $ComputerName" -ForegroundColor Yellow

    if (-not (Test-Path $LogPath)) {
        Write-Warning "Log path not found: $LogPath"
        return
    }

    try {
        $totalSizeBefore = 0
        $totalSizeAfter = 0
        $filesProcessed = 0
        $filesDeleted = 0
        $filesCompressed = 0

        # Get all log files
        $logFiles = Get-ChildItem -Path $LogPath -Filter "*.log" -Recurse -ErrorAction SilentlyContinue

        if ($logFiles.Count -eq 0) {
            Write-Host "No log files found" -ForegroundColor Green
            return
        }

        Write-Host "Found $($logFiles.Count) log files" -ForegroundColor Cyan

        foreach ($file in $logFiles) {
            $totalSizeBefore += $file.Length
            $filesProcessed++

            $fileAge = (Get-Date) - $file.LastWriteTime
            $fileSizeMB = $file.Length / 1MB

            Write-Host "Processing $($file.Name) ($([Math]::Round($fileSizeMB, 2))MB, $($fileAge.Days) days old)" -ForegroundColor Cyan

            # Delete old files
            if ($fileAge.Days -gt $RetentionDays) {
                Write-Host "  - Deleting old file (older than $RetentionDays days)" -ForegroundColor Red
                if (-not $WhatIf) {
                    Remove-Item $file.FullName -Force
                    $filesDeleted++
                }
                continue
            }

            # Compress large files
            if ($CompressOldLogs -and $fileSizeMB -gt 10 -and $fileAge.Days -gt 7) {
                $compressedFile = "$($file.FullName).zip"
                if (-not (Test-Path $compressedFile)) {
                    Write-Host "  - Compressing large file" -ForegroundColor Yellow
                    if (-not $WhatIf) {
                        Compress-Archive -Path $file.FullName -DestinationPath $compressedFile -Force
                        Remove-Item $file.FullName -Force
                        $filesCompressed++
                    }
                }
                continue
            }

            # Rotate large files
            if ($fileSizeMB -gt $MaxSizeMB) {
                Write-Host "  - Rotating large file (larger than $MaxSizeMB MB)" -ForegroundColor Yellow
                if (-not $WhatIf) {
                    $rotatedFile = "$($file.FullName).$($file.LastWriteTime.ToString('yyyyMMdd_HHmmss'))"
                    Move-Item $file.FullName $rotatedFile -Force
                    New-Item $file.FullName -ItemType File -Force
                }
            }
        }

        # Calculate new total size
        $newLogFiles = Get-ChildItem -Path $LogPath -Filter "*.log" -Recurse -ErrorAction SilentlyContinue
        $compressedFiles = Get-ChildItem -Path $LogPath -Filter "*.zip" -Recurse -ErrorAction SilentlyContinue

        $totalSizeAfter = ($newLogFiles | Measure-Object -Property Length -Sum).Sum + ($compressedFiles | Measure-Object -Property Length -Sum).Sum

        # Generate report
        $report = @"
Log File Management Report
==========================
Computer: $ComputerName
Timestamp: $(Get-Date)
Files Processed: $filesProcessed
Files Deleted: $filesDeleted
Files Compressed: $filesCompressed

Size Before: $([Math]::Round($totalSizeBefore / 1MB, 2)) MB
Size After: $([Math]::Round($totalSizeAfter / 1MB, 2)) MB
Space Saved: $([Math]::Round(($totalSizeBefore - $totalSizeAfter) / 1MB, 2)) MB
"@

        Write-Host $report -ForegroundColor Green

        # Save report
        $reportFile = Join-Path $LogPath "LogManagementReport_$(Get-Date -Format 'yyyyMMdd').txt"
        $report | Out-File $reportFile -Force

        Write-Host "Report saved to: $reportFile" -ForegroundColor Cyan

        # Send notification if significant cleanup occurred
        $spaceSavedMB = ($totalSizeBefore - $totalSizeAfter) / 1MB
        if ($spaceSavedMB -gt 100) {
            Send-MaintenanceNotification -Subject "Log Cleanup Completed - $spaceSavedMB MB saved" -Body $report
        }
    }
    catch {
        Write-Error "Log file management failed: $($_.Exception.Message)"
        throw
    }
}
```

### 5. Security Maintenance

#### Security Audit and Updates
```powershell
function Invoke-SecurityAudit {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    Write-Host "Performing security audit on $ComputerName" -ForegroundColor Yellow

    $auditReport = @{
        ComputerName = $ComputerName
        Timestamp = Get-Date
        Findings = @()
        Recommendations = @()
        OverallSecurityScore = 100
    }

    try {
        # Check service account permissions
        Write-Host "Checking service account permissions..." -ForegroundColor Cyan
        $service = Get-WmiObject -Class Win32_Service -Filter "Name='LicenseReleaseService'" -ComputerName $ComputerName
        $serviceAccount = $service.StartName

        $accountInfo = Get-ADUser -Identity $serviceAccount -Properties *
        $auditReport.Findings += @{
            Category = "Service Account"
            Finding = "Service account: $serviceAccount"
            Status = "Info"
            Critical = $false
        }

        # Check if account is disabled
        if ($accountInfo.Enabled -eq $false) {
            $auditReport.Findings += @{
                Category = "Service Account"
                Finding = "Service account is disabled"
                Status = "Critical"
                Critical = $true
            }
            $auditReport.OverallSecurityScore -= 20
            $auditReport.Recommendations += "Enable service account or update service configuration"
        }

        # Check account password expiration
        if ($accountInfo.PasswordNeverExpires -eq $false -and $accountInfo.PasswordLastSet) {
            $passwordAge = (Get-Date) - $accountInfo.PasswordLastSet
            $maxPasswordAge = (Get-ADDefaultDomainPasswordPolicy).MaxPasswordAge
            $daysUntilExpiration = $maxPasswordAge.TotalDays - $passwordAge.TotalDays

            if ($daysUntilExpiration -lt 7) {
                $auditReport.Findings += @{
                    Category = "Service Account"
                    Finding = "Service account password expires in $daysUntilExpiration days"
                    Status = "Warning"
                    Critical = $false
                }
                $auditReport.OverallSecurityScore -= 10
                $auditReport.Recommendations += "Update service account password before expiration"
            }
        }

        # Check file permissions
        Write-Host "Checking file permissions..." -ForegroundColor Cyan
        $installPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService"
        if (Test-Path $installPath) {
            $acl = Get-Acl $installPath
            $accessRules = $acl.Access | Where-Object { $_.IdentityReference -like "*$serviceAccount*" }

            if ($accessRules.Count -eq 0) {
                $auditReport.Findings += @{
                    Category = "File Permissions"
                    Finding = "Service account has no permissions to installation directory"
                    Status = "Critical"
                    Critical = $true
                }
                $auditReport.OverallSecurityScore -= 25
                $auditReport.Recommendations += "Grant appropriate permissions to service account"
            }
        }

        # Check firewall rules
        Write-Host "Checking firewall rules..." -ForegroundColor Cyan
        $firewallRules = Get-NetFirewallRule -DisplayName "*LicenseReleaseService*" -ErrorAction SilentlyContinue
        if ($firewallRules.Count -eq 0) {
            $auditReport.Findings += @{
                Category = "Firewall"
                Finding = "No firewall rules found for License Release Service"
                Status = "Warning"
                Critical = $false
            }
            $auditReport.OverallSecurityScore -= 15
            $auditReport.Recommendations += "Configure appropriate firewall rules for the service"
        }

        # Check for security updates
        Write-Host "Checking for security updates..." -ForegroundColor Cyan
        $updates = Get-HotFix -ComputerName $ComputerName -ErrorAction SilentlyContinue
        $securityUpdates = $updates | Where-Object { $_.Description -like "*Security*" -and $_.InstalledOn -gt (Get-Date).AddDays(-30) }

        if ($securityUpdates.Count -eq 0) {
            $auditReport.Findings += @{
                Category = "Updates"
                Finding = "No recent security updates found"
                Status = "Warning"
                Critical = $false
            }
            $auditReport.OverallSecurityScore -= 10
            $auditReport.Recommendations += "Check for and apply available security updates"
        }

        # Check audit logs
        Write-Host "Checking security logs..." -ForegroundColor Cyan
        $securityEvents = Get-WinEvent -ComputerName $ComputerName -LogName "Security" -MaxEvents 1000 -ErrorAction SilentlyContinue |
            Where-Object { $_.Id -in 4625, 4624, 4672, 4673, 4720, 4738 } # Logon/logoff events

        $failedLogons = $securityEvents | Where-Object { $_.Id -eq 4625 }
        if ($failedLogons.Count -gt 10) {
            $auditReport.Findings += @{
                Category = "Security Events"
                Finding = "High number of failed logon attempts: $($failedLogons.Count)"
                Status = "Warning"
                Critical = $false
            }
            $auditReport.OverallSecurityScore -= 15
            $auditReport.Recommendations += "Investigate failed logon attempts and consider implementing account lockout policies"
        }

        # Check for malware protection
        Write-Host "Checking malware protection..." -ForegroundColor Cyan
        $antivirus = Get-MpComputerStatus -ComputerName $ComputerName -ErrorAction SilentlyContinue
        if ($antivirus) {
            if ($antivirus.AntispywareEnabled -eq $false) {
                $auditReport.Findings += @{
                    Category = "Malware Protection"
                    Finding = "Antispyware protection is disabled"
                    Status = "Critical"
                    Critical = $true
                }
                $auditReport.OverallSecurityScore -= 20
                $auditReport.Recommendations += "Enable antispyware protection"
            }

            if ($antivirus.RealTimeProtectionEnabled -eq $false) {
                $auditReport.Findings += @{
                    Category = "Malware Protection"
                    Finding = "Real-time protection is disabled"
                    Status = "Critical"
                    Critical = $true
                }
                $auditReport.OverallSecurityScore -= 20
                $auditReport.Recommendations += "Enable real-time protection"
            }
        }
        else {
            $auditReport.Findings += @{
                Category = "Malware Protection"
                Finding = "Unable to check malware protection status"
                Status = "Warning"
                Critical = $false
            }
            $auditReport.OverallSecurityScore -= 10
            $auditReport.Recommendations += "Verify malware protection is installed and enabled"
        }

        # Generate final score
        $auditReport.OverallSecurityScore = [Math]::Max(0, $auditReport.OverallSecurityScore)

        # Save report
        $reportFile = "C:\Logs\LicenseReleaseService\SecurityAudits\SecurityAudit_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
        $reportDir = Split-Path $reportFile -Parent
        if (-not (Test-Path $reportDir)) {
            New-Item -ItemType Directory -Path $reportDir -Force
        }

        $auditReport | ConvertTo-Json -Depth 5 | Out-File $reportFile

        Write-Host "Security audit completed" -ForegroundColor Green
        Write-Host "Overall Security Score: $($auditReport.OverallSecurityScore)/100" -ForegroundColor $($auditReport.OverallSecurityScore -ge 80 ? "Green" : ($auditReport.OverallSecurityScore -ge 60 ? "Yellow" : "Red"))
        Write-Host "Report saved to: $reportFile" -ForegroundColor Cyan

        # Send notification if score is low
        if ($auditReport.OverallSecurityScore -lt 60) {
            Send-MaintenanceNotification -Subject "Security Audit Failed - Score: $($auditReport.OverallSecurityScore)/100" -Body ($auditReport | ConvertTo-Json -Depth 5)
        }

        return $auditReport
    }
    catch {
        Write-Error "Security audit failed: $($_.Exception.Message)"
        throw
    }
}
```

## Disaster Recovery Planning

### Backup and Recovery Procedures

#### 1. System Backup
```powershell
function Invoke-SystemBackup {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [string]$BackupPath = "D:\Backups\LicenseReleaseService",
        [switch]$IncludeDatabase,
        [switch]$IncludeLogs,
        [switch]$WhatIf
    )

    Write-Host "Starting system backup for $ComputerName" -ForegroundColor Yellow

    try {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $backupFolder = Join-Path $BackupPath $timestamp

        if (-not $WhatIf) {
            New-Item -ItemType Directory -Path $backupFolder -Force
        }

        Write-Host "Backup folder: $backupFolder" -ForegroundColor Cyan

        # Backup configuration files
        Write-Host "Backing up configuration files..." -ForegroundColor Cyan
        $configPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\Config"
        if (Test-Path $configPath) {
            $destConfigPath = Join-Path $backupFolder "Config"
            if (-not $WhatIf) {
                Copy-Item -Path $configPath -Destination $destConfigPath -Recurse -Force
            }
            Write-Host "Configuration files backed up" -ForegroundColor Green
        }

        # Backup service executable and dependencies
        Write-Host "Backing up service files..." -ForegroundColor Cyan
        $servicePath = "\\$ComputerName\C$\Program Files\LicenseReleaseService"
        if (Test-Path $servicePath) {
            $destServicePath = Join-Path $backupFolder "Service"
            if (-not $WhatIf) {
                # Copy only necessary files, exclude logs and temporary files
                Get-ChildItem -Path $servicePath | Where-Object {
                    $_.Extension -notin @(".log", ".tmp", ".bak") -and
                    $_.Name -notin @("Logs", "Temp")
                } | Copy-Item -Destination $destServicePath -Recurse -Force
            }
            Write-Host "Service files backed up" -ForegroundColor Green
        }

        # Backup registry settings
        Write-Host "Backing up registry settings..." -ForegroundColor Cyan
        $regFile = Join-Path $backupFolder "registry_backup.reg"
        if (-not $WhatIf) {
            reg export "HKLM\SOFTWARE\LicenseReleaseService" $regFile /y
        }
        Write-Host "Registry settings backed up" -ForegroundColor Green

        # Backup database
        if ($IncludeDatabase) {
            Write-Host "Backing up database..." -ForegroundColor Cyan
            $databaseBackupPath = Join-Path $backupFolder "Database"
            if (-not $WhatIf) {
                New-Item -ItemType Directory -Path $databaseBackupPath -Force

                # Perform SQL Server backup
                $backupQuery = @"
                BACKUP DATABASE [LicenseReleaseService]
                TO DISK = '$databaseBackupPath\LicenseReleaseService_$timestamp.bak'
                WITH NAME = 'License Release Service Full Backup',
                DESCRIPTION = 'Full backup of License Release Service database',
                STATS = 10
"@

                Invoke-Sqlcmd -ServerInstance $ComputerName -Database "master" -Query $backupQuery -ErrorAction Stop
            }
            Write-Host "Database backed up" -ForegroundColor Green
        }

        # Backup logs
        if ($IncludeLogs) {
            Write-Host "Backing up log files..." -ForegroundColor Cyan
            $logPath = "\\$ComputerName\C$\Logs\LicenseReleaseService"
            if (Test-Path $logPath) {
                $destLogPath = Join-Path $backupFolder "Logs"
                if (-not $WhatIf) {
                    New-Item -ItemType Directory -Path $destLogPath -Force
                    Get-ChildItem -Path $logPath | Copy-Item -Destination $destLogPath -Recurse -Force
                }
                Write-Host "Log files backed up" -ForegroundColor Green
            }
        }

        # Create backup manifest
        $manifest = @{
            ComputerName = $ComputerName
            BackupTimestamp = $timestamp
            BackupPath = $backupFolder
            IncludesDatabase = $IncludeDatabase.IsPresent
            IncludesLogs = $IncludeLogs.IsPresent
            FilesBackedUp = @{
                Config = Test-Path $configPath
                Service = Test-Path $servicePath
                Registry = $true
                Database = $IncludeDatabase.IsPresent
                Logs = $IncludeLogs.IsPresent
            }
            BackupSize = if (-not $WhatIf) { (Get-ChildItem $backupFolder -Recurse | Measure-Object -Property Length -Sum).Sum } else { 0 }
        }

        $manifestFile = Join-Path $backupFolder "backup_manifest.json"
        if (-not $WhatIf) {
            $manifest | ConvertTo-Json -Depth 5 | Out-File $manifestFile
        }

        Write-Host "Backup completed successfully" -ForegroundColor Green
        Write-Host "Backup size: $([Math]::Round($manifest.BackupSize / 1MB, 2)) MB" -ForegroundColor Cyan

        # Clean old backups (keep last 7 days)
        if (-not $WhatIf) {
            $oldBackups = Get-ChildItem -Path $BackupPath | Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-7) }
            foreach ($oldBackup in $oldBackups) {
                Write-Host "Removing old backup: $($oldBackup.Name)" -ForegroundColor Yellow
                Remove-Item $oldBackup.FullName -Recurse -Force
            }
        }

        return $manifest
    }
    catch {
        Write-Error "System backup failed: $($_.Exception.Message)"
        throw
    }
}
```

#### 2. System Recovery
```powershell
function Invoke-SystemRecovery {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [string]$BackupPath,
        [switch]$WhatIf
    )

    Write-Host "Starting system recovery for $ComputerName" -ForegroundColor Yellow

    if (-not (Test-Path $BackupPath)) {
        throw "Backup path not found: $BackupPath"
    }

    try {
        # Load backup manifest
        $manifestFile = Join-Path $BackupPath "backup_manifest.json"
        if (-not (Test-Path $manifestFile)) {
            throw "Backup manifest not found: $manifestFile"
        }

        $manifest = Get-Content $manifestFile | ConvertFrom-Json
        Write-Host "Recovering from backup created on: $($manifest.BackupTimestamp)" -ForegroundColor Cyan

        # Stop service
        Write-Host "Stopping service..." -ForegroundColor Cyan
        if (-not $WhatIf) {
            Stop-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -Force -ErrorAction Stop
        }

        # Restore configuration files
        Write-Host "Restoring configuration files..." -ForegroundColor Cyan
        $configBackupPath = Join-Path $BackupPath "Config"
        $configPath = "\\$ComputerName\C$\Program Files\LicenseReleaseService\Config"
        if (Test-Path $configBackupPath) {
            if (-not $WhatIf) {
                # Create backup of current config first
                $currentConfigBackup = Join-Path $configPath "Backup\Recovery_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                if (Test-Path $configPath) {
                    New-Item -ItemType Directory -Path $currentConfigBackup -Force
                    Copy-Item -Path "$configPath\*" -Destination $currentConfigBackup -Recurse -Force
                }

                # Restore from backup
                Copy-Item -Path "$configBackupPath\*" -Destination $configPath -Recurse -Force
            }
            Write-Host "Configuration files restored" -ForegroundColor Green
        }

        # Restore service files
        Write-Host "Restoring service files..." -ForegroundColor Cyan
        $serviceBackupPath = Join-Path $BackupPath "Service"
        $servicePath = "\\$ComputerName\C$\Program Files\LicenseReleaseService"
        if (Test-Path $serviceBackupPath) {
            if (-not $WhatIf) {
                # Create backup of current service files first
                $currentServiceBackup = Join-Path $servicePath "Backup\Recovery_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                if (Test-Path $servicePath) {
                    New-Item -ItemType Directory -Path $currentServiceBackup -Force
                    Get-ChildItem -Path $servicePath | Where-Object { $_.Name -notin @("Logs", "Temp", "Backup") } | Copy-Item -Destination $currentServiceBackup -Recurse -Force
                }

                # Restore from backup
                Copy-Item -Path "$serviceBackupPath\*" -Destination $servicePath -Recurse -Force
            }
            Write-Host "Service files restored" -ForegroundColor Green
        }

        # Restore registry settings
        Write-Host "Restoring registry settings..." -ForegroundColor Cyan
        $regBackupFile = Join-Path $BackupPath "registry_backup.reg"
        if (Test-Path $regBackupFile) {
            if (-not $WhatIf) {
                reg import $regBackupFile
            }
            Write-Host "Registry settings restored" -ForegroundColor Green
        }

        # Restore database
        if ($manifest.IncludesDatabase) {
            Write-Host "Restoring database..." -ForegroundColor Cyan
            $databaseBackupPath = Join-Path $BackupPath "Database"
            if (Test-Path $databaseBackupPath) {
                $backupFile = Get-ChildItem -Path $databaseBackupPath -Filter "*.bak" | Select-Object -First 1
                if ($backupFile) {
                    if (-not $WhatIf) {
                        $restoreQuery = @"
                        RESTORE DATABASE [LicenseReleaseService]
                        FROM DISK = '$($backupFile.FullName)'
                        WITH REPLACE,
                        MOVE 'LicenseReleaseService_Data' TO 'C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA\LicenseReleaseService.mdf',
                        MOVE 'LicenseReleaseService_Log' TO 'C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA\LicenseReleaseService.ldf'
"@

                        Invoke-Sqlcmd -ServerInstance $ComputerName -Database "master" -Query $restoreQuery -ErrorAction Stop
                    }
                    Write-Host "Database restored" -ForegroundColor Green
                }
            }
        }

        # Validate configuration
        Write-Host "Validating recovered configuration..." -ForegroundColor Cyan
        if (-not $WhatIf) {
            $configValid = Test-Configuration -ComputerName $ComputerName
            if (-not $configValid) {
                throw "Configuration validation failed after recovery"
            }
            Write-Host "Configuration validation passed" -ForegroundColor Green
        }

        # Start service
        Write-Host "Starting service..." -ForegroundColor Cyan
        if (-not $WhatIf) {
            Start-Service -Name "LicenseReleaseService" -ComputerName $ComputerName -ErrorAction Stop
        }

        # Verify service health
        Write-Host "Verifying service health..." -ForegroundColor Cyan
        if (-not $WhatIf) {
            $health = Get-ServiceHealth -ComputerName $ComputerName
            if (-not $health.IsHealthy) {
                throw "Service health check failed after recovery: $($health.HealthMessage)"
            }
            Write-Host "Service health check passed" -ForegroundColor Green
        }

        Write-Host "System recovery completed successfully" -ForegroundColor Green

        # Generate recovery report
        $recoveryReport = @{
            ComputerName = $ComputerName
            RecoveryTimestamp = Get-Date
            BackupTimestamp = $manifest.BackupTimestamp
            BackupPath = $BackupPath
            RecoverySuccessful = $true
            ComponentsRestored = @(
                if (Test-Path (Join-Path $BackupPath "Config")) { "Configuration" }
                if (Test-Path (Join-Path $BackupPath "Service")) { "Service Files" }
                if (Test-Path (Join-Path $BackupPath "registry_backup.reg")) { "Registry" }
                if ($manifest.IncludesDatabase) { "Database" }
            )
        }

        $reportFile = "C:\Logs\LicenseReleaseService\RecoveryReports\Recovery_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
        $reportDir = Split-Path $reportFile -Parent
        if (-not (Test-Path $reportDir)) {
            New-Item -ItemType Directory -Path $reportDir -Force
        }

        $recoveryReport | ConvertTo-Json -Depth 5 | Out-File $reportFile
        Write-Host "Recovery report saved to: $reportFile" -ForegroundColor Cyan

        return $recoveryReport
    }
    catch {
        Write-Error "System recovery failed: $($_.Exception.Message)"
        throw
    }
}
```

## Maintenance Automation

### Scheduled Maintenance Scripts

#### 1. Daily Maintenance Script
```powershell
# DailyMaintenance.ps1
param (
    [string]$ComputerName = $env:COMPUTERNAME,
    [string]$LogPath = "C:\Logs\LicenseReleaseService\Maintenance",
    [switch]$SendReport
)

# Initialize logging
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $LogPath "DailyMaintenance_$timestamp.log"

function Write-Log {
    param (
        [string]$Message,
        [string]$Level = "INFO"
    )

    $logEntry = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Add-Content -Path $logFile -Value $logEntry

    switch ($Level) {
        "ERROR" { Write-Error $Message }
        "WARNING" { Write-Warning $Message }
        "INFO" { Write-Host $Message -ForegroundColor Cyan }
        "SUCCESS" { Write-Host $Message -ForegroundColor Green }
    }
}

try {
    Write-Log "Starting daily maintenance for $ComputerName"

    # Create log directory if it doesn't exist
    if (-not (Test-Path $LogPath)) {
        New-Item -ItemType Directory -Path $LogPath -Force
    }

    $maintenanceResults = @{
        ComputerName = $ComputerName
        Timestamp = Get-Date
        Tasks = @()
        Success = $true
        Errors = @()
    }

    # Task 1: Service Health Check
    Write-Log "Performing service health check..."
    try {
        $health = Get-ServiceHealth -ComputerName $ComputerName
        $maintenanceResults.Tasks += @{
            Name = "Service Health Check"
            Status = if ($health.IsHealthy) { "SUCCESS" } else { "FAILED" }
            Details = $health.HealthMessage
            Critical = -not $health.IsHealthy
        }

        if (-not $health.IsHealthy) {
            $maintenanceResults.Success = $false
            $maintenanceResults.Errors += "Service health check failed: $($health.HealthMessage)"
        }

        Write-Log "Service health check completed - $($health.Status)" "SUCCESS"
    }
    catch {
        $errorMsg = "Service health check failed: $($_.Exception.Message)"
        Write-Log $errorMsg "ERROR"
        $maintenanceResults.Tasks += @{
            Name = "Service Health Check"
            Status = "FAILED"
            Details = $_.Exception.Message
            Critical = $true
        }
        $maintenanceResults.Success = $false
        $maintenanceResults.Errors += $errorMsg
    }

    # Task 2: Resource Usage Check
    Write-Log "Checking resource usage..."
    try {
        $resources = Get-SystemResourceMetrics -ComputerName $ComputerName -SampleCount 5
        if ($resources) {
            $cpuUsage = $resources | Where-Object { $_.Counter -like "*% Processor Time" -and $_.Instance -eq "_Total" } | Select-Object -ExpandProperty Average
            $memoryUsage = $resources | Where-Object { $_.Counter -like "*Private Bytes" } | Select-Object -ExpandProperty Average
            $memoryMB = $memoryUsage / 1MB

            $resourceStatus = "SUCCESS"
            $resourceDetails = "CPU: $([Math]::Round($cpuUsage, 1))%, Memory: $([Math]::Round($memoryMB, 1))MB"

            if ($cpuUsage -gt 80 -or $memoryMB -gt 500) {
                $resourceStatus = "WARNING"
                $maintenanceResults.Errors += "High resource usage detected: $resourceDetails"
            }

            $maintenanceResults.Tasks += @{
                Name = "Resource Usage Check"
                Status = $resourceStatus
                Details = $resourceDetails
                Critical = $cpuUsage -gt 90 -or $memoryMB -gt 1000
            }

            Write-Log "Resource usage check completed - $resourceDetails" "SUCCESS"
        }
    }
    catch {
        $errorMsg = "Resource usage check failed: $($_.Exception.Message)"
        Write-Log $errorMsg "ERROR"
        $maintenanceResults.Tasks += @{
            Name = "Resource Usage Check"
            Status = "FAILED"
            Details = $_.Exception.Message
            Critical = $true
        }
        $maintenanceResults.Success = $false
        $maintenanceResults.Errors += $errorMsg
    }

    # Task 3: Log File Management
    Write-Log "Managing log files..."
    try {
        Manage-LogFiles -ComputerName $ComputerName -RetentionDays 30 -MaxSizeMB 500 -CompressOldLogs -WhatIf:$false -ErrorAction Stop
        $maintenanceResults.Tasks += @{
            Name = "Log File Management"
            Status = "SUCCESS"
            Details = "Log files rotated and cleaned"
            Critical = $false
        }
        Write-Log "Log file management completed" "SUCCESS"
    }
    catch {
        $errorMsg = "Log file management failed: $($_.Exception.Message)"
        Write-Log $errorMsg "ERROR"
        $maintenanceResults.Tasks += @{
            Name = "Log File Management"
            Status = "FAILED"
            Details = $_.Exception.Message
            Critical = $false
        }
        $maintenanceResults.Errors += $errorMsg
    }

    # Task 4: Configuration Validation
    Write-Log "Validating configuration..."
    try {
        $configValid = Test-Configuration -ComputerName $ComputerName
        $maintenanceResults.Tasks += @{
            Name = "Configuration Validation"
            Status = if ($configValid) { "SUCCESS" } else { "FAILED" }
            Details = if ($configValid) { "Configuration is valid" } else { "Configuration validation failed" }
            Critical = -not $configValid
        }

        if (-not $configValid) {
            $maintenanceResults.Success = $false
            $maintenanceResults.Errors += "Configuration validation failed"
        }

        Write-Log "Configuration validation completed - $(if ($configValid) { 'Valid' } else { 'Invalid' })" "SUCCESS"
    }
    catch {
        $errorMsg = "Configuration validation failed: $($_.Exception.Message)"
        Write-Log $errorMsg "ERROR"
        $maintenanceResults.Tasks += @{
            Name = "Configuration Validation"
            Status = "FAILED"
            Details = $_.Exception.Message
            Critical = $true
        }
        $maintenanceResults.Success = $false
        $maintenanceResults.Errors += $errorMsg
    }

    # Task 5: Performance Metrics Collection
    Write-Log "Collecting performance metrics..."
    try {
        $performance = Get-DetectionSuccessMetrics -ComputerName $ComputerName -StartTime (Get-Date).AddHours(-24)
        if ($performance) {
            $performanceStatus = "SUCCESS"
            $performanceDetails = "Success Rate: $([Math]::Round($performance.SuccessRate * 100, 1))%, Detections: $($performance.TotalDetections)"

            if ($performance.SuccessRate -lt 0.9) {
                $performanceStatus = "WARNING"
                $maintenanceResults.Errors += "Low detection success rate: $([Math]::Round($performance.SuccessRate * 100, 1))%"
            }

            $maintenanceResults.Tasks += @{
                Name = "Performance Metrics Collection"
                Status = $performanceStatus
                Details = $performanceDetails
                Critical = $performance.SuccessRate -lt 0.8
            }

            Write-Log "Performance metrics collected - $performanceDetails" "SUCCESS"
        }
    }
    catch {
        $errorMsg = "Performance metrics collection failed: $($_.Exception.Message)"
        Write-Log $errorMsg "ERROR"
        $maintenanceResults.Tasks += @{
            Name = "Performance Metrics Collection"
            Status = "FAILED"
            Details = $_.Exception.Message
            Critical = $false
        }
        $maintenanceResults.Errors += $errorMsg
    }

    # Generate maintenance report
    $maintenanceResults | ConvertTo-Json -Depth 5 | Out-File (Join-Path $LogPath "DailyMaintenance_$timestamp.json")

    Write-Log "Daily maintenance completed - Overall Status: $(if ($maintenanceResults.Success) { 'SUCCESS' } else { 'FAILED' })" "SUCCESS"

    # Send report if requested or if there were critical failures
    if ($SendReport -or (-not $maintenanceResults.Success)) {
        $reportSubject = if ($maintenanceResults.Success) {
            "Daily Maintenance Report - SUCCESS"
        }
        else {
            "Daily Maintenance Report - FAILED"
        }

        $reportBody = @"
Daily Maintenance Report for $ComputerName
================================
Timestamp: $($maintenanceResults.Timestamp)
Overall Status: $(if ($maintenanceResults.Success) { 'SUCCESS' } else { 'FAILED' })

Tasks Completed:
$($maintenanceResults.Tasks | ForEach-Object { "- $($_.Name): $($_.Status)$(if (-not [string]::IsNullOrEmpty($_.Details)) { " - $($_.Details)" })" })

$($maintenanceResults.Errors.Count) Errors Encountered:
$($maintenanceResults.Errors | ForEach-Object { "- $_" })

Full report available at: $logFile
"@

        Send-MaintenanceNotification -Subject $reportSubject -Body $reportBody
    }

    exit 0
}
catch {
    Write-Log "Daily maintenance failed with unhandled error: $($_.Exception.Message)" "ERROR"
    exit 1
}
```

This Maintenance Guide provides comprehensive procedures for maintaining the Idle Detection system. Regular maintenance, proper backup procedures, and proactive monitoring are essential for ensuring system reliability and performance.