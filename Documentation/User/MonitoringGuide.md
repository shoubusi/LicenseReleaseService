# License Release Service - Monitoring Guide

## Overview

This guide provides comprehensive information for monitoring the Idle Detection system. It covers monitoring setup, metrics collection, alerting, and troubleshooting for optimal system performance and reliability.

## Monitoring Architecture

### Monitoring Components

```
License Release Service Monitoring
├── System Health Monitoring
│   ├── Service Status
│   ├── Process Health
│   ├── Resource Usage
│   └── Component Health
├── Performance Monitoring
│   ├── Detection Performance
│   ├── Response Times
│   ├── Success Rates
│   └── Resource Utilization
├── Business Metrics
│   ├── License Utilization
│   ├── User Experience
│   ├── Cost Savings
│   └── Compliance Metrics
└── Alerting & Notification
    ├── Threshold Monitoring
    ├── Anomaly Detection
    ├── Notification Delivery
    └── Escalation Procedures
```

### Monitoring Data Flow

```
Data Sources → Collection → Processing → Storage → Analysis → Alerting
     ↓            ↓           ↓          ↓         ↓          ↓
   Service → Metrics → Aggregation → Database → Dashboard → Notification
   System   → Logs      → Analysis   → Archive  → Reports  → Escalation
   Network  → Events    → Correlation→ History  → Insights → Action
```

## System Health Monitoring

### Service Health Checks

#### 1. Service Status Monitoring
```powershell
function Get-ServiceHealthStatus {
    param (
        [string[]]$ComputerNames = $env:COMPUTERNAME,
        [int]$TimeoutSeconds = 30
    )

    $results = @()

    foreach ($computer in $ComputerNames) {
        try {
            # Basic service status
            $service = Get-Service -Name "LicenseReleaseService" -ComputerName $computer -ErrorAction Stop

            # Detailed health check via API
            $health = Invoke-RestMethod -Uri "http://$($computer):8080/api/idledetection/health" -TimeoutSec $TimeoutSeconds

            $result = [PSCustomObject]@{
                ComputerName = $computer
                ServiceName = $service.Name
                ServiceStatus = $service.Status
                IsHealthy = $health.IsHealthy
                EngineStatus = $health.Status
                HealthMessage = $health.StatusMessage
                CheckTime = $health.CheckTimestamp
                DetectorCount = $health.Statistics.DetectorCount
                EnabledDetectorCount = $health.Statistics.EnabledDetectorCount
                TotalDetections = $health.Statistics.TotalDetections
                SuccessRate = $health.Statistics.SuccessRate
                LastExecutionTime = $health.Statistics.LastExecutionTime
                Uptime = (Get-Date) - $health.Statistics.LastExecutionTime
            }

            # Check component health
            $componentHealth = @{}
            foreach ($component in $health.DetectorHealth) {
                $componentHealth[$component.ComponentName] = @{
                    IsHealthy = $component.IsHealthy
                    StatusMessage = $component.StatusMessage
                    LastError = $component.LastError
                }
            }

            $result.ComponentHealth = $componentHealth
            $results += $result
        }
        catch {
            $results += [PSCustomObject]@{
                ComputerName = $computer
                ServiceName = "LicenseReleaseService"
                ServiceStatus = "Error"
                IsHealthy = $false
                EngineStatus = "Unknown"
                HealthMessage = $_.Exception.Message
                CheckTime = Get-Date
                DetectorCount = 0
                EnabledDetectorCount = 0
                TotalDetections = 0
                SuccessRate = 0
                LastExecutionTime = $null
                Uptime = $null
                ComponentHealth = @{}
            }
        }
    }

    return $results
}
```

#### 2. Component Health Monitoring
```powershell
function Get-ComponentHealthStatus {
    param (
        [string]$ComputerName = $env:COMPUTERNAME
    )

    try {
        $health = Invoke-RestMethod -Uri "http://$($ComputerName):8080/api/idledetection/health" -TimeoutSec 10

        $componentStatus = @()

        # Timer Service Health
        $componentStatus += [PSCustomObject]@{
            ComponentName = "TimerService"
            IsHealthy = $health.TimerServiceHealth.IsHealthy
            StatusMessage = $health.TimerServiceHealth.StatusMessage
            LastError = $health.TimerServiceHealth.LastError
            AdditionalInfo = $health.TimerServiceHealth.AdditionalInfo
            Priority = "High"
        }

        # Detector Health
        foreach ($detector in $health.DetectorHealth) {
            $componentStatus += [PSCustomObject]@{
                ComponentName = $detector.ComponentName
                IsHealthy = $detector.IsHealthy
                StatusMessage = $detector.StatusMessage
                LastError = $detector.LastError
                AdditionalInfo = $detector.AdditionalInfo
                Priority = "Medium"
            }
        }

        return $componentStatus
    }
    catch {
        Write-Warning "Unable to get component health: $($_.Exception.Message)"
        return @()
    }
}
```

### Resource Monitoring

#### 1. System Resource Monitoring
```powershell
function Get-SystemResourceMetrics {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [int]$SampleCount = 5
    )

    $counters = @(
        "\Processor(_Total)\% Processor Time",
        "\Memory\Available MBytes",
        "\Memory\% Committed Bytes In Use",
        "\PhysicalDisk(_Total)\% Disk Time",
        "\Network Interface(*)\Bytes Total/sec",
        "\Process(LicenseReleaseService)\% Processor Time",
        "\Process(LicenseReleaseService)\Private Bytes",
        "\Process(LicenseReleaseService)\Handle Count",
        "\Process(LicenseReleaseService)\Thread Count"
    )

    $samples = Get-Counter -Counter $counters -ComputerName $ComputerName -MaxSamples $SampleCount -ErrorAction SilentlyContinue

    if ($samples) {
        $metrics = @()

        foreach ($sample in $samples.CounterSamples) {
            $metrics += [PSCustomObject]@{
                Timestamp = $sample.Timestamp
                Counter = $sample.Path
                Instance = $sample.InstanceName
                Value = $sample.CookedValue
            }
        }

        # Calculate statistics
        $statistics = $metrics | Group-Object -Property Counter | ForEach-Object {
            $values = $_.Group.Value
            [PSCustomObject]@{
                Counter = $_.Name
                Instance = $_.Group[0].Instance
                Average = ($values | Measure-Object -Average).Average
                Maximum = ($values | Measure-Object -Maximum).Maximum
                Minimum = ($values | Measure-Object -Minimum).Minimum
                LastValue = $values[-1]
                StandardDeviation = if ($values.Count -gt 1) {
                    [Math]::Sqrt(($values | ForEach-Object { [Math]::Pow($_ - $values.Average, 2) } | Measure-Object -Average).Average)
                } else { 0 }
            }
        }

        return $statistics
    }
    else {
        Write-Warning "Unable to collect resource metrics from $ComputerName"
        return $null
    }
}
```

#### 2. Memory Usage Analysis
```powershell
function Get-MemoryUsageAnalysis {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [int]$MonitoringMinutes = 60
    )

    $processName = "LicenseReleaseService"
    $startTime = (Get-Date).AddMinutes(-$MonitoringMinutes)

    # Get memory usage over time
    $memoryCounters = Get-Counter -Counter "\Process($processName)\Private Bytes" -ComputerName $ComputerName -MaxSamples ($MonitoringMinutes * 2) -ErrorAction SilentlyContinue

    if ($memoryCounters) {
        $memoryData = $memoryCounters.CounterSamples | ForEach-Object {
            [PSCustomObject]@{
                Timestamp = $_.Timestamp
                MemoryBytes = $_.CookedValue
                MemoryMB = $_.CookedValue / 1MB
            }
        }

        # Analyze trends
        $analysis = [PSCustomObject]@{
            CurrentMemoryMB = $memoryData[-1].MemoryMB
            AverageMemoryMB = ($memoryData.MemoryMB | Measure-Object -Average).Average
            MaximumMemoryMB = ($memoryData.MemoryMB | Measure-Object -Maximum).Maximum
            MinimumMemoryMB = ($memoryData.MemoryMB | Measure-Object -Minimum).Minimum
            MemoryTrend = if ($memoryData.Count -gt 1) {
                $first = $memoryData[0].MemoryMB
                $last = $memoryData[-1].MemoryMB
                if ($last -gt $first) { "Increasing" } elseif ($last -lt $first) { "Decreasing" } else { "Stable" }
            } else { "Unknown" }
            MemoryGrowthRateMB = if ($memoryData.Count -gt 1) {
                ($memoryData[-1].MemoryMB - $memoryData[0].MemoryMB) / $MonitoringMinutes
            } else { 0 }
            MemoryLeakSuspected = $false
            Recommendations = @()
        }

        # Check for memory leaks
        if ($analysis.MemoryGrowthRateMB -gt 1) {
            $analysis.MemoryLeakSuspected = $true
            $analysis.Recommendations += "Potential memory leak detected. Growth rate: $($analysis.MemoryGrowthRateMB:F2) MB/min"
        }

        if ($analysis.MaximumMemoryMB -gt 500) {
            $analysis.Recommendations += "High memory usage detected. Consider reducing configuration settings."
        }

        if ($analysis.MemoryTrend -eq "Increasing" -and $analysis.MemoryLeakSuspected) {
            $analysis.Recommendations += "Schedule service restart during maintenance window."
        }

        return $analysis
    }
    else {
        Write-Warning "Unable to collect memory usage data"
        return $null
    }
}
```

## Performance Monitoring

### Detection Performance Metrics

#### 1. Detection Success Rate Monitoring
```powershell
function Get-DetectionSuccessMetrics {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [DateTime]$StartTime = (Get-Date).AddHours(-1),
        [DateTime]$EndTime = (Get-Date)
    )

    try {
        # Get detection events from event log
        $events = Get-WinEvent -ComputerName $ComputerName -LogName "Application" -ProviderName "LicenseReleaseService" -StartTime $StartTime -EndTime $EndTime |
            Where-Object { $_.Id -in 1001, 1002, 1003 } |
            ForEach-Object {
                $properties = $_.Properties
                [PSCustomObject]@{
                    Timestamp = $_.TimeCreated
                    EventId = $_.Id
                    ProcessId = $properties[0].Value
                    UserName = $properties[1].Value
                    ComputerName = $properties[2].Value
                    DetectionMethod = $properties[3].Value
                    Confidence = $properties[4].Value
                    IsIdle = $properties[5].Value
                    DurationMs = $properties[6].Value
                    Success = if ($_.Id -eq 1002) { $true } else { $false }
                }
            }

        if ($events) {
            $metrics = [PSCustomObject]@{
                TotalDetections = $events.Count
                SuccessfulDetections = ($events | Where-Object { $_.Success }).Count
                FailedDetections = ($events | Where-Object { -not $_.Success }).Count
                SuccessRate = if ($events.Count -gt 0) {
                    ($events | Where-Object { $_.Success }).Count / $events.Count
                } else { 0 }
                AverageConfidence = ($events.Confidence | Measure-Object -Average).Average
                AverageResponseTimeMs = ($events.DurationMs | measure-object -Average).Average
                MethodsUsed = $events.DetectionMethod | Select-Object -Unique
                TimeRange = @{
                    Start = $StartTime
                    End = $EndTime
                    Duration = $EndTime - $StartTime
                }
            }

            # Calculate detection rate
            $metrics.DetectionsPerMinute = $metrics.TotalDetections / $metrics.TimeRange.Duration.TotalMinutes

            return $metrics
        }
        else {
            Write-Warning "No detection events found in specified time range"
            return $null
        }
    }
    catch {
        Write-Warning "Error collecting detection metrics: $($_.Exception.Message)"
        return $null
    }
}
```

#### 2. Detection Method Performance
```powershell
function Get-DetectionMethodPerformance {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [DateTime]$StartTime = (Get-Date).AddHours(-24),
        [DateTime]$EndTime = (Get-Date)
    )

    $events = Get-WinEvent -ComputerName $ComputerName -LogName "Application" -ProviderName "LicenseReleaseService" -StartTime $StartTime -EndTime $EndTime |
        Where-Object { $_.Id -in 1001, 1002, 1003 } |
        ForEach-Object {
            $properties = $_.Properties
            [PSCustomObject]@{
                Timestamp = $_.TimeCreated
                EventId = $_.Id
                DetectionMethod = $properties[3].Value
                Confidence = $properties[4].Value
                IsIdle = $properties[5].Value
                DurationMs = $properties[6].Value
                Success = if ($_.Id -eq 1002) { $true } else { $false }
            }
        }

    if ($events) {
        $methodPerformance = $events | Group-Object -Property DetectionMethod | ForEach-Object {
            $methodEvents = $_.Group

            [PSCustomObject]@{
                Method = $_.Name
                TotalDetections = $methodEvents.Count
                SuccessfulDetections = ($methodEvents | Where-Object { $_.Success }).Count
                FailedDetections = ($methodEvents | Where-Object { -not $_.Success }).Count
                SuccessRate = if ($methodEvents.Count -gt 0) {
                    ($methodEvents | Where-Object { $_.Success }).Count / $methodEvents.Count
                } else { 0 }
                AverageConfidence = ($methodEvents.Confidence | Measure-Object -Average).Average
                AverageResponseTimeMs = ($methodEvents.DurationMs | Measure-Object -Average).Average
                IdleDetectionRate = if ($methodEvents.Count -gt 0) {
                    ($methodEvents | Where-Object { $_.IsIdle }).Count / $methodEvents.Count
                } else { 0 }
            }
        }

        return $methodPerformance
    }
    else {
        Write-Warning "No detection events found"
        return @()
    }
}
```

### Response Time Monitoring

#### 1. Response Time Analysis
```powershell
function Get-ResponseTimeAnalysis {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [DateTime]$StartTime = (Get-Date).AddHours(-24),
        [DateTime]$EndTime = (Get-Date)
    )

    $events = Get-WinEvent -ComputerName $ComputerName -LogName "Application" -ProviderName "LicenseReleaseService" -StartTime $StartTime -EndTime $EndTime |
        Where-Object { $_.Id -in 1001, 1002, 1003 } |
        ForEach-Object {
            $properties = $_.Properties
            [PSCustomObject]@{
                Timestamp = $_.TimeCreated
                DurationMs = $properties[6].Value
                Success = if ($_.Id -eq 1002) { $true } else { $false }
                DetectionMethod = $properties[3].Value
            }
        }

    if ($events) {
        $analysis = [PSCustomObject]@{
            OverallAverageMs = ($events.DurationMs | Measure-Object -Average).Average
            OverallMaximumMs = ($events.DurationMs | Measure-Object -Maximum).Maximum
            OverallMinimumMs = ($events.DurationMs | Measure-Object -Minimum).Minimum
            Overall95thPercentile = Get-Percentile -Values $events.DurationMs -Percentile 95
            Overall99thPercentile = Get-Percentile -Values $events.DurationMs -Percentile 99
            SlowResponseCount = ($events | Where-Object { $_.DurationMs -gt 5000 }).Count
            FastResponseCount = ($events | Where-Object { $_.DurationMs -lt 100 }).Count
            SuccessRateByResponseTime = @{
                Fast = ($events | Where-Object { $_.DurationMs -lt 100 -and $_.Success }).Count / ($events | Where-Object { $_.DurationMs -lt 100 }).Count
                Normal = ($events | Where-Object { $_.DurationMs -ge 100 -and $_.DurationMs -lt 1000 -and $_.Success }).Count / ($events | Where-Object { $_.DurationMs -ge 100 -and $_.DurationMs -lt 1000 }).Count
                Slow = ($events | Where-Object { $_.DurationMs -ge 1000 -and $_.Success }).Count / ($events | Where-Object { $_.DurationMs -ge 1000 }).Count
            }
        }

        # Response time by detection method
        $analysis.MethodPerformance = $events | Group-Object -Property DetectionMethod | ForEach-Object {
            $methodEvents = $_.Group

            [PSCustomObject]@{
                Method = $_.Name
                AverageMs = ($methodEvents.DurationMs | Measure-Object -Average).Average
                MaximumMs = ($methodEvents.DurationMs | Measure-Object -Maximum).Maximum
                95thPercentile = Get-Percentile -Values $methodEvents.DurationMs -Percentile 95
                SampleSize = $methodEvents.Count
            }
        }

        return $analysis
    }
    else {
        Write-Warning "No response time data found"
        return $null
    }
}

function Get-Percentile {
    param (
        [double[]]$Values,
        [double]$Percentile
    )

    if ($Values.Count -eq 0) { return 0 }

    $sorted = $Values | Sort-Object
    $index = [Math]::Floor(($Percentile / 100) * ($sorted.Count - 1))

    return $sorted[$index]
}
```

## Business Metrics Monitoring

### License Utilization Metrics

#### 1. License Usage Analysis
```powershell
function Get-LicenseUtilizationMetrics {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [DateTime]$StartTime = (Get-Date).AddDays(-7),
        [DateTime]$EndTime = (Get-Date)
    )

    try {
        # Get license manager data
        $licenseData = Invoke-RestMethod -Uri "http://$($ComputerName):8080/api/licenses/usage" -TimeoutSec 10

        if ($licenseData) {
            $metrics = [PSCustomObject]@{
                TotalLicenses = $licenseData.TotalLicenses
                AvailableLicenses = $licenseData.AvailableLicenses
                UsedLicenses = $licenseData.UsedLicenses
                UtilizationPercentage = if ($licenseData.TotalLicenses -gt 0) {
                    ($licenseData.UsedLicenses / $licenseData.TotalLicenses) * 100
                } else { 0 }
                PeakUsage = $licenseData.PeakUsage
                AverageUsage = $licenseData.AverageUsage
                UserCount = $licenseData.ActiveUsers.Count
                Sessions = $licenseData.ActiveSessions
                TimeRange = @{
                    Start = $StartTime
                    End = $EndTime
                    Duration = $EndTime - $StartTime
                }
            }

            # Calculate efficiency metrics
            $metrics.EfficiencyMetrics = [PSCustomObject]@{
                LicensesPerUser = if ($metrics.UserCount -gt 0) { $metrics.UsedLicenses / $metrics.UserCount } else { 0 }
                IdleDetectionRate = ($licenseData.ReleasedLicenses.ByIdleDetection / $licenseData.ReleasedLicenses.Total) * 100
                UserComplianceRate = ($licenseData.ActiveUsers | Where-Object { $_.ComplianceScore -gt 80 }).Count / $metrics.UserCount * 100
            }

            # Calculate cost savings
            $metrics.CostSavings = [PSCustomObject]@{
                IdleDetectionSavings = $licenseData.ReleasedLicenses.ByIdleDetection * $licenseData.LicenseCostPerMonth / 30 * ($EndTime - $StartTime).TotalDays
                TotalSavings = $licenseData.ReleasedLicenses.Total * $licenseData.LicenseCostPerMonth / 30 * ($EndTime - $StartTime).TotalDays
                SavingsPercentage = if ($licenseData.TotalLicenseCost -gt 0) {
                    ($metrics.CostSavings.TotalSavings / $licenseData.TotalLicenseCost) * 100
                } else { 0 }
            }

            return $metrics
        }
        else {
            Write-Warning "Unable to retrieve license utilization data"
            return $null
        }
    }
    catch {
        Write-Warning "Error collecting license utilization metrics: $($_.Exception.Message)"
        return $null
    }
}
```

#### 2. User Experience Metrics
```powershell
function Get-UserExperienceMetrics {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [DateTime]$StartTime = (Get-Date).AddDays(-7),
        [DateTime]$EndTime = (Get-Date)
    )

    try {
        # Get user experience data
        $userData = Invoke-RestMethod -Uri "http://$($ComputerName):8080/api/users/experience" -TimeoutSec 10

        if ($userData) {
            $metrics = [PSCustomObject]@{
                TotalUsers = $userData.TotalUsers
                ActiveUsers = $userData.ActiveUsers
                SatisfiedUsers = ($userData.Users | Where-Object { $_.SatisfactionScore -ge 4 }).Count
                DissatisfiedUsers = ($userData.Users | Where-Object { $_.SatisfactionScore -le 2 }).Count
                AverageSatisfactionScore = ($userData.Users.SatisfactionScore | Measure-Object -Average).Average
                AverageResponseTime = ($userData.Users.AverageResponseTime | Measure-Object -Average).Average
                LicenseAcquisitionTime = ($userData.Users.LicenseAcquisitionTime | Measure-Object -Average).Average
                TimeRange = @{
                    Start = $StartTime
                    End = $EndTime
                    Duration = $EndTime - $StartTime
                }
            }

            # Calculate satisfaction metrics
            $metrics.SatisfactionMetrics = [PSCustomObject]@{
                SatisfactionRate = if ($metrics.TotalUsers -gt 0) {
                    ($metrics.SatisfiedUsers / $metrics.TotalUsers) * 100
                } else { 0 }
                DissatisfactionRate = if ($metrics.TotalUsers -gt 0) {
                    ($metrics.DissatisfiedUsers / $metrics.TotalUsers) * 100
                } else { 0 }
                NetPromoterScore = (($userData.Users | Where-Object { $_.SatisfactionScore -ge 9 }).Count - ($userData.Users | Where-Object { $_.SatisfactionScore -le 6 }).Count) / $metrics.TotalUsers * 100
            }

            # Calculate performance metrics
            $metrics.PerformanceMetrics = [PSCustomObject]@{
                FastResponseRate = ($userData.Users | Where-Object { $_.AverageResponseTime -lt 1 }).Count / $metrics.TotalUsers * 100
                SlowResponseRate = ($userData.Users | Where-Object { $_.AverageResponseTime -gt 5 }).Count / $metrics.TotalUsers * 100
                FastLicenseAcquisitionRate = ($userData.Users | Where-Object { $_.LicenseAcquisitionTime -lt 2 }).Count / $metrics.TotalUsers * 100
            }

            # Calculate user engagement
            $metrics.EngagementMetrics = [PSCustomObject]@{
                ActiveUserRate = if ($metrics.TotalUsers -gt 0) {
                    ($metrics.ActiveUsers / $metrics.TotalUsers) * 100
                } else { 0 }
                AverageSessionDuration = ($userData.Users.AverageSessionDuration | Measure-Object -Average).Average
                SessionsPerUser = ($userData.Users | Measure-Object -Property SessionCount -Average).Average
            }

            return $metrics
        }
        else {
            Write-Warning "Unable to retrieve user experience data"
            return $null
        }
    }
    catch {
        Write-Warning "Error collecting user experience metrics: $($_.Exception.Message)"
        return $null
    }
}
```

## Alerting and Notification

### Alert Configuration

#### 1. Alert Threshold Setup
```powershell
function Set-AlertThresholds {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [hashtable]$Thresholds
    )

    $defaultThresholds = @{
        ServiceDown = @{
            Enabled = $true
            Severity = "Critical"
            Threshold = 0
            Comparison = "Equals"
            WindowMinutes = 5
        }
        HighCpuUsage = @{
            Enabled = $true
            Severity = "Warning"
            Threshold = 80
            Comparison = "GreaterThan"
            WindowMinutes = 10
        }
        HighMemoryUsage = @{
            Enabled = $true
            Severity = "Warning"
            Threshold = 500
            Comparison = "GreaterThan"
            WindowMinutes = 10
        }
        LowSuccessRate = @{
            Enabled = $true
            Severity = "Warning"
            Threshold = 0.9
            Comparison = "LessThan"
            WindowMinutes = 30
        }
        SlowResponseTime = @{
            Enabled = $true
            Severity = "Warning"
            Threshold = 5000
            Comparison = "GreaterThan"
            WindowMinutes = 15
        }
        HighErrorRate = @{
            Enabled = $true
            Severity = "Critical"
            Threshold = 0.1
            Comparison = "GreaterThan"
            WindowMinutes = 15
        }
    }

    # Merge with provided thresholds
    $finalThresholds = Merge-Hashtables $defaultThresholds $Thresholds

    # Save thresholds to configuration
    $thresholdPath = "C:\Program Files\LicenseReleaseService\Config\alert_thresholds.json"
    $finalThresholds | ConvertTo-Json -Depth 5 | Out-File $thresholdPath -Force

    Write-Host "Alert thresholds configured successfully" -ForegroundColor Green
    return $finalThresholds
}

function Merge-Hashtables {
    param (
        [hashtable]$Default,
        [hashtable]$Override
    )

    $result = @{}

    foreach ($key in $Default.Keys) {
        if ($Override.ContainsKey($key)) {
            if ($Default[$key] -is [hashtable] -and $Override[$key] -is [hashtable]) {
                $result[$key] = Merge-Hashtables $Default[$key] $Override[$key]
            }
            else {
                $result[$key] = $Override[$key]
            }
        }
        else {
            $result[$key] = $Default[$key]
        }
    }

    foreach ($key in $Override.Keys) {
        if (-not $Default.ContainsKey($key)) {
            $result[$key] = $Override[$key]
        }
    }

    return $result
}
```

#### 2. Alert Processing Engine
```powershell
class AlertProcessor {
    [hashtable]$Thresholds
    [string]$ComputerName
    [System.Collections.ArrayList]$AlertHistory

    AlertProcessor([string]$computerName) {
        $this.ComputerName = $computerName
        $this.AlertHistory = New-Object System.Collections.ArrayList
        $this.LoadThresholds()
    }

    [void] LoadThresholds() {
        $thresholdPath = "C:\Program Files\LicenseReleaseService\Config\alert_thresholds.json"
        if (Test-Path $thresholdPath) {
            $this.Thresholds = Get-Content $thresholdPath | ConvertFrom-Json -AsHashtable
        }
        else {
            $this.Thresholds = Set-AlertThresholds -ComputerName $this.ComputerName
        }
    }

    [void] ProcessMetrics([hashtable]$metrics) {
        foreach ($thresholdName in $this.Thresholds.Keys) {
            $threshold = $this.Thresholds[$thresholdName]

            if (-not $threshold.Enabled) {
                continue
            }

            $currentValue = $this.GetMetricValue($thresholdName, $metrics)
            $shouldAlert = $this.EvaluateThreshold($currentValue, $threshold)

            if ($shouldAlert) {
                $alert = @{
                    Name = $thresholdName
                    Severity = $threshold.Severity
                    CurrentValue = $currentValue
                    Threshold = $threshold.Threshold
                    Timestamp = Get-Date
                    ComputerName = $this.ComputerName
                    Message = $this.GenerateAlertMessage($thresholdName, $currentValue, $threshold)
                }

                $this.TriggerAlert($alert)
            }
        }
    }

    [double] GetMetricValue([string]$metricName, [hashtable]$metrics) {
        switch ($metricName) {
            "ServiceDown" { return if ($metrics.ServiceStatus -eq "Running") { 1 } else { 0 } }
            "HighCpuUsage" { return $metrics.CpuUsage }
            "HighMemoryUsage" { return $metrics.MemoryUsageMB }
            "LowSuccessRate" { return $metrics.SuccessRate }
            "SlowResponseTime" { return $metrics.AverageResponseTimeMs }
            "HighErrorRate" { return $metrics.ErrorRate }
            default { return 0 }
        }
    }

    [bool] EvaluateThreshold([double]$currentValue, [hashtable]$threshold) {
        switch ($threshold.Comparison) {
            "Equals" { return $currentValue -eq $threshold.Threshold }
            "GreaterThan" { return $currentValue -gt $threshold.Threshold }
            "LessThan" { return $currentValue -lt $threshold.Threshold }
            "GreaterThanOrEqual" { return $currentValue -ge $threshold.Threshold }
            "LessThanOrEqual" { return $currentValue -le $threshold.Threshold }
            default { return $false }
        }
    }

    [string] GenerateAlertMessage([string]$thresholdName, [double]$currentValue, [hashtable]$threshold) {
        switch ($thresholdName) {
            "ServiceDown" { return "License Release Service is not running" }
            "HighCpuUsage" { return "High CPU usage detected: $currentValue% (threshold: $($threshold.Threshold)%)" }
            "HighMemoryUsage" { return "High memory usage detected: $currentValue MB (threshold: $($threshold.Threshold) MB)" }
            "LowSuccessRate" { return "Low detection success rate: $currentValue% (threshold: $($threshold.Threshold * 100)%)" }
            "SlowResponseTime" { return "Slow response time detected: $currentValue ms (threshold: $($threshold.Threshold) ms)" }
            "HighErrorRate" { return "High error rate detected: $currentValue% (threshold: $($threshold.Threshold * 100)%)" }
            default { return "Alert triggered for $thresholdName" }
        }
    }

    [void] TriggerAlert([hashtable]$alert) {
        # Check if this is a new alert or recurrence
        $lastAlert = $this.AlertHistory | Where-Object { $_.Name -eq $alert.Name } | Select-Object -Last 1

        if ($null -eq $lastAlert -or ($alert.Timestamp - $lastAlert.Timestamp).TotalMinutes -gt 30) {
            # Send notification
            $this.SendNotification($alert)

            # Add to history
            $this.AlertHistory.Add($alert) | Out-Null

            # Log alert
            $this.LogAlert($alert)
        }
    }

    [void] SendNotification([hashtable]$alert) {
        # Send email notification
        $emailParams = @{
            From = "licensing-alerts@company.com"
            To = "licensing-admins@company.com"
            Subject = "License Release Service Alert - $($alert.Severity): $($alert.Name)"
            Body = @"
Alert Details:
- Alert: $($alert.Name)
- Severity: $($alert.Severity)
- Computer: $($alert.ComputerName)
- Current Value: $($alert.CurrentValue)
- Threshold: $($alert.Threshold)
- Message: $($alert.Message)
- Time: $($alert.Timestamp)
"@
            SmtpServer = "smtp.company.com"
            Priority = if ($alert.Severity -eq "Critical") { "High" } else { "Normal" }
        }

        Send-MailMessage @emailParams -ErrorAction SilentlyContinue

        # Send to monitoring system
        try {
            $monitoringData = @{
                alert_name = $alert.Name
                severity = $alert.Severity
                current_value = $alert.CurrentValue
                threshold = $alert.Threshold
                message = $alert.Message
                computer_name = $alert.ComputerName
                timestamp = $alert.Timestamp.ToString("o")
            }

            Invoke-RestMethod -Uri "http://monitoring.company.com/api/alerts" -Method Post -Body ($monitoringData | ConvertTo-Json) -ContentType "application/json" -ErrorAction SilentlyContinue
        }
        catch {
            Write-Warning "Failed to send alert to monitoring system: $($_.Exception.Message)"
        }
    }

    [void] LogAlert([hashtable]$alert) {
        $logEntry = "[$($alert.Timestamp.ToString('yyyy-MM-dd HH:mm:ss'))] [$($alert.Severity)] Alert: $($alert.Name) - $($alert.Message)"
        Add-Content -Path "C:\Logs\LicenseReleaseService\alerts.log" -Value $logEntry

        # Also log to Windows Event Log
        $eventId = switch ($alert.Severity) {
            "Critical" { 1001 }
            "Warning" { 1002 }
            "Information" { 1003 }
            default { 1000 }
        }

        Write-EventLog -LogName "Application" -Source "LicenseReleaseService" -EntryType $alert.Severity -EventId $eventId -Message $alert.Message -ErrorAction SilentlyContinue
    }
}
```

### Dashboard and Reporting

#### 1. Performance Dashboard Setup
```powershell
function New-MonitoringDashboard {
    param (
        [string]$ComputerName = $env:COMPUTERNAME,
        [string]$OutputPath = "C:\Inetpub\wwwroot\monitoring\dashboard.html"
    )

    # Create HTML dashboard
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>License Release Service Monitoring Dashboard</title>
    <meta http-equiv="refresh" content="60">
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .dashboard { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 20px; }
        .card { border: 1px solid #ddd; border-radius: 8px; padding: 20px; background: #f9f9f9; }
        .card h3 { margin-top: 0; color: #333; }
        .metric { font-size: 2em; font-weight: bold; margin: 10px 0; }
        .status-healthy { color: #28a745; }
        .status-warning { color: #ffc107; }
        .status-critical { color: #dc3545; }
        .chart { height: 200px; margin-top: 15px; }
        table { width: 100%; border-collapse: collapse; margin-top: 15px; }
        th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }
        th { background-color: #f2f2f2; }
        .last-updated { text-align: right; font-size: 0.9em; color: #666; }
    </style>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
</head>
<body>
    <h1>License Release Service Monitoring Dashboard</h1>
    <div class="last-updated">Last Updated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</div>

    <div class="dashboard">
        <div class="card">
            <h3>Service Health</h3>
            <div id="serviceHealth" class="metric">Loading...</div>
            <div id="serviceStatus">Loading...</div>
        </div>

        <div class="card">
            <h3>Performance Metrics</h3>
            <div id="successRate" class="metric">Loading...</div>
            <div id="responseTime">Loading...</div>
        </div>

        <div class="card">
            <h3>Resource Usage</h3>
            <div id="cpuUsage" class="metric">Loading...</div>
            <div id="memoryUsage" class="metric">Loading...</div>
        </div>

        <div class="card">
            <h3>License Utilization</h3>
            <div id="licenseUtilization" class="metric">Loading...</div>
            <div id="licenseCount">Loading...</div>
        </div>

        <div class="card">
            <h3>Recent Alerts</h3>
            <div id="recentAlerts">Loading...</div>
        </div>

        <div class="card">
            <h3>Performance Chart</h3>
            <canvas id="performanceChart" class="chart"></canvas>
        </div>
    </div>

    <script>
        // Fetch and display metrics
        async function fetchMetrics() {
            try {
                const response = await fetch('/api/metrics');
                const metrics = await response.json();

                updateServiceHealth(metrics.serviceHealth);
                updatePerformanceMetrics(metrics.performance);
                updateResourceUsage(metrics.resources);
                updateLicenseUtilization(metrics.licenses);
                updateRecentAlerts(metrics.alerts);
                updatePerformanceChart(metrics.performanceHistory);
            } catch (error) {
                console.error('Error fetching metrics:', error);
            }
        }

        function updateServiceHealth(health) {
            const healthElement = document.getElementById('serviceHealth');
            const statusElement = document.getElementById('serviceStatus');

            healthElement.textContent = health.isHealthy ? 'Healthy' : 'Unhealthy';
            healthElement.className = 'metric status-' + (health.isHealthy ? 'healthy' : 'critical');
            statusElement.textContent = health.statusMessage;
        }

        function updatePerformanceMetrics(performance) {
            document.getElementById('successRate').textContent = (performance.successRate * 100).toFixed(1) + '%';
            document.getElementById('responseTime').textContent = performance.averageResponseTime.toFixed(0) + 'ms';
        }

        function updateResourceUsage(resources) {
            document.getElementById('cpuUsage').textContent = resources.cpuUsage.toFixed(1) + '%';
            document.getElementById('memoryUsage').textContent = resources.memoryUsage.toFixed(0) + 'MB';
        }

        function updateLicenseUtilization(licenses) {
            document.getElementById('licenseUtilization').textContent = (licenses.utilization * 100).toFixed(1) + '%';
            document.getElementById('licenseCount').textContent = licenses.used + ' / ' + licenses.total;
        }

        function updateRecentAlerts(alerts) {
            const alertsElement = document.getElementById('recentAlerts');
            if (alerts.length === 0) {
                alertsElement.innerHTML = '<div class="status-healthy">No recent alerts</div>';
            } else {
                alertsElement.innerHTML = alerts.map(alert =>
                    `<div class="status-${alert.severity.toLowerCase()}">${alert.timestamp}: ${alert.message}</div>`
                ).join('');
            }
        }

        function updatePerformanceChart(history) {
            const ctx = document.getElementById('performanceChart').getContext('2d');
            new Chart(ctx, {
                type: 'line',
                data: {
                    labels: history.map(h => h.timestamp),
                    datasets: [{
                        label: 'Success Rate',
                        data: history.map(h => h.successRate * 100),
                        borderColor: 'rgb(75, 192, 192)',
                        tension: 0.1
                    }, {
                        label: 'Response Time',
                        data: history.map(h => h.responseTime),
                        borderColor: 'rgb(255, 99, 132)',
                        tension: 0.1,
                        yAxisID: 'y1'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            type: 'linear',
                            display: true,
                            position: 'left',
                            title: {
                                display: true,
                                text: 'Success Rate (%)'
                            }
                        },
                        y1: {
                            type: 'linear',
                            display: true,
                            position: 'right',
                            title: {
                                display: true,
                                text: 'Response Time (ms)'
                            },
                            grid: {
                                drawOnChartArea: false,
                            },
                        }
                    }
                }
            });
        }

        // Initial load and refresh every 60 seconds
        fetchMetrics();
        setInterval(fetchMetrics, 60000);
    </script>
</body>
</html>
"@

    # Ensure directory exists
    $directory = Split-Path $OutputPath -Parent
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force
    }

    # Save dashboard
    $html | Out-File $OutputPath -Force

    Write-Host "Monitoring dashboard created at $OutputPath" -ForegroundColor Green
}
```

This Monitoring Guide provides comprehensive coverage of all aspects of monitoring the Idle Detection system. Regular monitoring, proper alert configuration, and comprehensive reporting are essential for maintaining system health and ensuring optimal performance.