# License Release Service - Deployment Guide

This guide provides comprehensive instructions for deploying the License Release Service with idle detection capabilities.

## Quick Start

### Prerequisites
- Windows Server 2008 R2 or later
- .NET Framework 4.8 or higher
- PowerShell 5.0 or higher
- Administrator privileges

### Basic Deployment
```powershell
# Run deployment script
.\deployment\deploy.ps1 -Environment Production -ConfigPath .\deployment\production-config.json
```

## Deployment Components

### 1. Configuration Files

#### production-config.json
Main configuration file containing:
- Idle detection settings
- Monitoring configuration
- Logging settings
- Deployment parameters
- Service configuration

#### monitoring-config.json
Monitoring and alerting configuration:
- Health check settings
- Alert thresholds
- Email notifications
- Performance monitoring
- Dashboard configuration

### 2. Deployment Scripts

#### deploy.ps1
Main deployment script that:
- Validates prerequisites
- Backs up existing installation
- Installs service files
- Configures monitoring
- Sets up permissions
- Starts service
- Validates deployment

**Usage:**
```powershell
# Standard deployment
.\deploy.ps1 -Environment Production

# With custom config
.\deploy.ps1 -Environment Production -ConfigPath .\custom-config.json

# Configuration update only
.\deploy.ps1 -Environment Production -SkipServiceInstall

# Force deployment despite validation failures
.\deploy.ps1 -Environment Production -Force
```

#### health-check.ps1
Comprehensive health monitoring script that:
- Checks service status
- Validates configuration
- Monitors performance
- Analyzes logs
- Tests network connectivity
- Generates health reports

**Usage:**
```powershell
# Basic health check
.\health-check.ps1

# Custom configuration
.\health-check.ps1 -ConfigPath "C:\Program Files\LicenseReleaseService\config.json"

# Output formats
.\health-check.ps1 -OutputFormat Json
.\health-check.ps1 -OutputFormat Html -OutputFile "C:\temp\health-report.html"

# Custom thresholds
.\thresholds = @{ MemoryUsageMB = 200; CpuUsagePercent = 90 }
.\health-check.ps1 -Thresholds $thresholds
```

### 3. Monitoring and Alerting

#### Health Monitoring
The service includes comprehensive health monitoring:

**Health Checks:**
- Service status and response time
- Configuration validation
- Performance metrics (CPU, Memory, Disk)
- Log file health
- Network connectivity
- Security settings

**Performance Metrics:**
- Detection response times
- Memory usage trends
- CPU utilization
- Handle and thread counts
- Error rates
- Throughput metrics

#### Alerting System
Multi-channel alerting with configurable thresholds:

**Alert Types:**
- Email notifications
- Windows Event Log entries
- Performance counter alerts
- Resource usage alerts
- Service failure alerts

**Alert Levels:**
- Critical (immediate attention required)
- Warning (investigation recommended)
- Information (for awareness)

#### Web Dashboard
Real-time monitoring dashboard (port 8080):

**Dashboard Features:**
- Service status widget
- Performance metrics charts
- Resource usage gauges
- Recent alerts list
- Health check results
- System logs viewer

## Configuration Reference

### Idle Detection Configuration

```json
{
  "idleDetection": {
    "isEnabled": true,
    "detectionInterval": "00:00:30",
    "confidenceThreshold": 0.8,
    "maxConcurrentDetections": 100,
    "enableTimeBasedDetection": true,
    "enablePingBasedDetection": true,
    "enableActivityMonitoring": true
  }
}
```

**Key Settings:**
- `detectionInterval`: How often to check for idle processes
- `confidenceThreshold`: Minimum confidence level for detection
- `maxConcurrentDetections`: Maximum simultaneous detections
- Detection strategy toggles for each method

### Monitoring Configuration

```json
{
  "monitoring": {
    "enableHealthMonitoring": true,
    "healthCheckInterval": "00:01:00",
    "enablePerformanceMonitoring": true,
    "performanceMetricsInterval": "00:05:00"
  }
}
```

**Key Settings:**
- `healthCheckInterval`: Frequency of health checks
- `performanceMetricsInterval`: Performance data collection frequency
- Various monitoring toggles for different components

### Alerting Configuration

```json
{
  "alerting": {
    "enableEmailAlerts": true,
    "criticalAlertThreshold": 3,
    "warningAlertThreshold": 5,
    "alertSuppressionWindow": "00:15:00"
  }
}
```

**Key Settings:**
- Alert thresholds for different severity levels
- Alert suppression to prevent spam
- Escalation settings for critical alerts

## Deployment Scenarios

### 1. Fresh Installation

```powershell
# Step 1: Validate system requirements
.\deployment\deploy.ps1 -Environment Production -WhatIf

# Step 2: Perform deployment
.\deployment\deploy.ps1 -Environment Production

# Step 3: Validate deployment
.\health-check.ps1 -OutputFormat Html -OutputFile "deployment-validation.html"
```

### 2. Configuration Update

```powershell
# Update configuration without reinstalling service
.\deployment\deploy.ps1 -Environment Production -SkipServiceInstall

# Restart service to apply changes
Restart-Service -Name "LicenseReleaseService"

# Validate configuration
.\health-check.ps1 -ConfigPath "C:\Program Files\LicenseReleaseService\config.json"
```

### 3. Version Upgrade

```powershell
# Step 1: Stop service
Stop-Service -Name "LicenseReleaseService"

# Step 2: Backup current installation
Copy-Item "C:\Program Files\LicenseReleaseService" "C:\Backup\LicenseReleaseService_$(Get-Date -Format 'yyyyMMdd')" -Recurse

# Step 3: Deploy new version
.\deployment\deploy.ps1 -Environment Production

# Step 4: Validate upgrade
.\health-check.ps1 -OutputFormat Json -OutputFile "upgrade-validation.json"
```

### 4. Rollback

```powershell
# Manual rollback procedure
Stop-Service -Name "LicenseReleaseService"

# Restore from backup
Copy-Item "C:\Backup\LicenseReleaseService_YYYYMMDD\*" "C:\Program Files\LicenseReleaseService" -Recurse -Force

# Start service
Start-Service -Name "LicenseReleaseService"

# Validate rollback
.\health-check.ps1
```

## Troubleshooting

### Common Issues

#### Service Won't Start
1. Check event logs for error messages
2. Validate configuration file syntax
3. Ensure required dependencies are running
4. Verify file permissions

```powershell
# Check service status
Get-Service -Name "LicenseReleaseService"

# View recent errors
Get-WinEvent -LogName Application -Source "LicenseReleaseService" -MaxEvents 10 | Where-Object { $_.LevelDisplayName -eq "Error" }

# Validate configuration
Test-Path "C:\Program Files\LicenseReleaseService\config.json"
```

#### High Memory Usage
1. Check configuration settings
2. Review performance metrics
3. Consider adjusting detection intervals
4. Monitor for memory leaks

```powershell
# Check memory usage
Get-Process -Name "LicenseReleaseService" | Select-Object Name, WorkingSet, CPU

# View performance counters
Get-Counter "\Process(LicenseReleaseService)\Working Set" -MaxSamples 10
```

#### Detection Not Working
1. Verify idle detection is enabled
2. Check confidence threshold settings
3. Review detection logs
4. Validate process monitoring configuration

```powershell
# Check detection configuration
$config = Get-Content "C:\Program Files\LicenseReleaseService\config.json" | ConvertFrom-Json
$config.idleDetection

# View detection logs
Get-Content "C:\Program Files\LicenseReleaseService\Logs\detection.log" -Tail 20
```

### Advanced Diagnostics

#### Enable Debug Logging
```json
{
  "logging": {
    "logLevel": "Debug",
    "enableConsoleLogging": true,
    "enableFileLogging": true
  }
}
```

#### Performance Profiling
```powershell
# Enable performance counters
Get-Counter -ListSet "License Release Service" | Format-List

# Monitor specific metrics
Get-Counter "\License Release Service\Detections/sec" -MaxSamples 60 -SampleInterval 1
```

#### Network Diagnostics
```powershell
# Test service endpoints
Test-NetConnection localhost -Port 8080

# Check firewall rules
Get-NetFirewallRule -Name "*LicenseReleaseService*"
```

## Maintenance

### Scheduled Maintenance

#### Log Rotation
The service automatically rotates logs based on size and retention settings.

#### Performance Data Cleanup
Old performance data is automatically cleaned up based on retention policies.

#### Health Check Scheduling
Regular health checks are scheduled to monitor system health.

### Manual Maintenance

#### Manual Log Cleanup
```powershell
# Clean old log files
Get-ChildItem "C:\Program Files\LicenseReleaseService\Logs" -Filter "*.log" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item
```

#### Performance Counter Reset
```powershell
# Reset performance counters
Unregister-PerfCounter -Category "License Release Service"
```

#### Service Recycle
```powershell
# Graceful service restart
Restart-Service -Name "LicenseReleaseService" -Force
```

## Security Considerations

### File Permissions
- Service runs under NETWORK SERVICE account
- Log directory requires write permissions
- Configuration files should be read-only for service account

### Network Security
- Dashboard port (8080) should be firewall-protected
- Consider using HTTPS for dashboard access
- Restrict access to monitoring endpoints

### Certificate Management
- Consider using SSL certificates for web dashboard
- Regular certificate rotation recommended
- Store certificates securely

## Support

### Getting Help
1. Check health check reports
2. Review event logs
3. Consult documentation
4. Contact support team

### Reporting Issues
1. Collect health check reports
2. Gather relevant log files
3. Include configuration details
4. Document steps to reproduce

### Performance Optimization
1. Monitor performance metrics regularly
2. Adjust configuration based on load
3. Consider hardware upgrades if needed
4. Review detection intervals for efficiency

## Appendix

### Configuration Templates
See individual configuration files for detailed settings and examples.

### Performance Benchmarks
- Target response time: <100ms
- Memory usage: <100MB
- CPU usage: <80%
- Error rate: <1%

### Monitoring Dashboards
Access the web dashboard at `http://localhost:8080` for real-time monitoring.

---

*This deployment guide covers the complete setup and maintenance of the License Release Service with idle detection capabilities.*