# Idle Detection Troubleshooting Guide

## Overview

This guide provides comprehensive troubleshooting steps for common issues encountered with the Idle Detection system. The guide covers error diagnosis, performance issues, configuration problems, and integration failures.

## Quick Reference

### Common Issues and Solutions

| Issue | Possible Cause | Solution |
|-------|---------------|----------|
| Detection not working | Service not running | Start the Idle Detection service |
| High false positive rate | Thresholds too low | Increase detection thresholds |
| High false negative rate | Thresholds too high | Decrease detection thresholds |
| Service crashes | Configuration error | Validate configuration file |
| Performance issues | Resource constraints | Optimize configuration |
| License release failures | License manager issues | Check license manager health |

## Troubleshooting Methodology

### 1. Problem Identification
- **Symptom Analysis**: Identify specific symptoms and error messages
- **Reproduction Steps**: Document steps to reproduce the issue
- **Impact Assessment**: Determine the impact on users and systems
- **Environment Context**: Note the environment and configuration

### 2. Data Collection
- **Log Analysis**: Review system and application logs
- **Performance Metrics**: Collect performance data
- **Configuration Review**: Examine current configuration
- **System State**: Check system health and resources

### 3. Diagnosis
- **Root Cause Analysis**: Identify the underlying cause
- **Component Isolation**: Isolate the failing component
- **Correlation Analysis**: Correlate with other events
- **Hypothesis Testing**: Test potential causes

### 4. Resolution
- **Solution Implementation**: Apply the appropriate fix
- **Testing**: Verify the solution works
- **Monitoring**: Monitor for recurrence
- **Documentation**: Document the resolution

## Common Issues and Solutions

### 1. Service-Related Issues

#### Service Not Starting

**Symptoms**:
- Service fails to start
- Timeout errors during startup
- Event log errors related to service startup

**Diagnosis**:
```powershell
# Check service status
Get-Service -Name "LicenseReleaseService"

# Check service logs
Get-EventLog -LogName Application -Source "LicenseReleaseService" -Newest 50

# Check dependencies
Get-Service -Name "LicenseReleaseService" | Select-Object -ExpandProperty DependentServices
```

**Solutions**:

1. **Check Dependencies**:
```powershell
# Ensure all required services are running
Get-Service -Name "EventLog", "Winmgmt", "WinRM" | Where-Object { $_.Status -ne "Running" }
```

2. **Validate Configuration**:
```powershell
# Validate configuration file
.\LicenseReleaseService.exe --validate-config
```

3. **Check Permissions**:
```powershell
# Verify service account permissions
Get-Acl -Path "C:\Program Files\LicenseReleaseService" | Format-List
```

#### Service Crashes

**Symptoms**:
- Service stops unexpectedly
- Application event log errors
- Memory access violations

**Diagnosis**:
```powershell
# Check crash dumps
Get-ChildItem -Path "C:\ProgramData\Microsoft\Windows\WER\ReportArchive" -Recurse -Filter "*.dmp"

# Check application logs
Get-EventLog -LogName Application -Source "LicenseReleaseService" -EntryType Error -Newest 100

# Monitor service in real-time
Get-EventLog -LogName Application -Source "LicenseReleaseService" -Wait
```

**Solutions**:

1. **Update Configuration**:
```xml
<!-- Reduce resource usage -->
<timeBasedDetection
  detectionIntervalSeconds="120"
  monitoringSampleRateMs="200"
  maxDetectionTimeMs="10000"
/>
```

2. **Increase Resources**:
```powershell
# Increase memory limits
Set-ItemProperty -Path "HKLM:\SOFTWARE\LicenseReleaseService" -Name "MaxMemoryMB" -Value 1024
```

3. **Enable Debug Logging**:
```xml
<logging
  logLevel="Debug"
  enableDetectionLogging="true"
  enablePerformanceLogging="true"
  enableErrorLogging="true"
/>
```

### 2. Detection Issues

#### False Positives (Detecting Idle When Active)

**Symptoms**:
- Licenses released when users are active
- Users complain about unexpected license releases
- Detection logs show idle state during active periods

**Diagnosis**:
```powershell
# Check detection logs
Select-String -Path "C:\Logs\LicenseReleaseService\detection.log" -Pattern "IsIdle.*true"

# Analyze detection patterns
Get-Content -Path "C:\Logs\LicenseReleaseService\detection.log" |
    Where-Object { $_ -match "IsIdle.*true" } |
    Group-Object -Property { $_.Split(',')[0] } |
    Select-Object Name, Count
```

**Solutions**:

1. **Adjust Thresholds**:
```xml
<timeBasedDetection
  warningThresholdMinutes="10"
  imminentThresholdMinutes="20"
  criticalThresholdMinutes="30"
  hysteresisMinutes="5"
/>
```

2. **Enable More Monitoring**:
```xml
<timeBasedDetection
  enableKeyboardMonitoring="true"
  enableMouseMonitoring="true"
  enableSystemMonitoring="true"
  enableSolidWorksMonitoring="true"
/>
```

3. **Tune Consensus Engine**:
```xml
<consensusEngine
  consensusThreshold="0.8"
  minDetectorsForConsensus="3"
  enableHysteresis="true"
  hysteresisDurationMinutes="5"
/>
```

#### False Negatives (Not Detecting Idle When Idle)

**Symptoms**:
- Licenses not released when users are idle
- Resource utilization remains high
- Detection logs show active state during idle periods

**Diagnosis**:
```powershell
# Check for stuck detection
Get-Content -Path "C:\Logs\LicenseReleaseService\detection.log" |
    Where-Object { $_ -match "IsIdle.*false" } |
    Select-Object -Last 100

# Check detector health
Get-Content -Path "C:\Logs\LicenseReleaseService\health.log" |
    Where-Object { $_ -match "Unhealthy" }
```

**Solutions**:

1. **Decrease Thresholds**:
```xml
<timeBasedDetection
  warningThresholdMinutes="3"
  imminentThresholdMinutes="5"
  criticalThresholdMinutes="10"
/>
```

2. **Enable Adaptive Learning**:
```xml
<timeBasedDetection
  enableAdaptiveThresholds="true"
  adaptiveLearningRate="0.2"
  minAdaptiveThreshold="0.5"
  maxAdaptiveThreshold="1.5"
/>
```

3. **Adjust Detector Weights**:
```xml
<consensusEngine
  timeBasedWeight="0.6"
  pingBasedWeight="0.3"
  activityMonitoringWeight="0.1"
/>
```

### 3. Performance Issues

#### High CPU Usage

**Symptoms**:
- CPU usage consistently above 80%
- System becomes sluggish
- Detection cycles take too long

**Diagnosis**:
```powershell
# Monitor CPU usage
Get-Counter -Counter "\Processor(_Total)\% Processor Time" -SampleInterval 1 -MaxSamples 60

# Check process CPU usage
Get-Process -Name "LicenseReleaseService" | Select-Object CPU, Id, ProcessName

# Analyze performance logs
Get-Content -Path "C:\Logs\LicenseReleaseService\performance.log" |
    Where-Object { $_ -match "CPU.*>" } |
    Select-Object -Last 20
```

**Solutions**:

1. **Reduce Monitoring Frequency**:
```xml
<timeBasedDetection
  detectionIntervalSeconds="120"
  monitoringSampleRateMs="500"
/>
```

2. **Disable Unnecessary Monitoring**:
```xml
<timeBasedDetection
  enableKeyboardMonitoring="true"
  enableMouseMonitoring="true"
  enableSystemMonitoring="false"
  enableSolidWorksMonitoring="false"
/>
```

3. **Optimize Configuration**:
```xml
<activityMonitoring
  activityHistorySize="500"
  activityAnalysisIntervalMs="10000"
  performanceSampleIntervalMs="5000"
/>
```

#### High Memory Usage

**Symptoms**:
- Memory usage continuously increases
- Memory leaks suspected
- System becomes unresponsive

**Diagnosis**:
```powershell
# Monitor memory usage
Get-Counter -Counter "\Memory\Available MBytes" -SampleInterval 5 -MaxSamples 60

# Check process memory
Get-Process -Name "LicenseReleaseService" | Select-Object WorkingSet, PrivateMemorySize, Id

# Check for memory leaks
Get-Counter -Counter "\Process(LicenseReleaseService)\Private Bytes" -SampleInterval 10 -MaxSamples 100
```

**Solutions**:

1. **Reduce History Size**:
```xml
<activityMonitoring
  activityHistorySize="100"
/>
```

2. **Enable Memory Management**:
```xml
<configuration>
  <runtime>
    <gcServer enabled="true" />
    <gcConcurrent enabled="true" />
  </runtime>
</configuration>
```

3. **Schedule Regular Restarts**:
```powershell
# Create scheduled task for service restart
$action = New-ScheduledTaskAction -Execute "powershell" -Argument "-Command Restart-Service -Name 'LicenseReleaseService'"
$trigger = New-ScheduledTaskTrigger -Daily -At 2:00AM
Register-ScheduledTask -TaskName "RestartLicenseService" -Action $action -Trigger $trigger
```

### 4. Configuration Issues

#### Configuration Validation Errors

**Symptoms**:
- Service fails to start with configuration errors
- Configuration validation fails
- Event log shows configuration parsing errors

**Diagnosis**:
```powershell
# Validate configuration
.\LicenseReleaseService.exe --validate-config

# Check configuration syntax
Get-Content -Path "C:\Program Files\LicenseReleaseService\app.config" |
    Select-String -Pattern "error|Error"

# Check configuration backups
Get-ChildItem -Path "C:\Program Files\LicenseReleaseService\Backup" -Filter "*.config"
```

**Solutions**:

1. **Fix Configuration Errors**:
```xml
<!-- Fix threshold progression -->
<timeBasedDetection
  warningThresholdMinutes="5"
  imminentThresholdMinutes="10"
  criticalThresholdMinutes="15"
/>
```

2. **Restore from Backup**:
```powershell
# Restore last known good configuration
Copy-Item -Path "C:\Program Files\LicenseReleaseService\Backup\app.config.20240101" -Destination "C:\Program Files\LicenseReleaseService\app.config"

# Restart service
Restart-Service -Name "LicenseReleaseService"
```

3. **Reset to Defaults**:
```powershell
# Reset configuration to defaults
.\LicenseReleaseService.exe --reset-config
```

#### Configuration Not Applied

**Symptoms**:
- Configuration changes not taking effect
- Service behavior doesn't match configuration
- Configuration changes lost after restart

**Diagnosis**:
```powershell
# Check configuration file permissions
Get-Acl -Path "C:\Program Files\LicenseReleaseService\app.config" | Format-List

# Check if service is reading from correct file
Get-Process -Name "LicenseReleaseService" | Select-Object Path

# Monitor configuration file access
Get-WinEvent -LogName "Security" -FilterXPath "*System[Provider[@Name='Microsoft-Windows-Security-Auditing'] and (EventID=4663)]" |
    Where-Object { $_.Message -match "app.config" }
```

**Solutions**:

1. **Check File Permissions**:
```powershell
# Ensure service account has read access
$acl = Get-Acl -Path "C:\Program Files\LicenseReleaseService\app.config"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("NT SERVICE\LicenseReleaseService", "Read", "Allow")
$acl.SetAccessRule($rule)
Set-Acl -Path "C:\Program Files\LicenseReleaseService\app.config" -AclObject $acl
```

2. **Restart Service**:
```powershell
# Force restart to reload configuration
Restart-Service -Name "LicenseReleaseService" -Force
```

3. **Verify Configuration Path**:
```powershell
# Check if configuration is in correct location
Test-Path -Path "C:\Program Files\LicenseReleaseService\app.config"
```

### 5. Integration Issues

#### License Manager Integration Issues

**Symptoms**:
- License verification fails
- License release operations fail
- License manager timeouts

**Diagnosis**:
```powershell
# Check license manager health
Test-Connection -ComputerName "LicenseServer" -Count 4

# Check license manager service
Get-Service -Name "LicenseManager" -ComputerName "LicenseServer"

# Check license manager logs
Get-EventLog -LogName Application -Source "LicenseManager" -Newest 50
```

**Solutions**:

1. **Check Network Connectivity**:
```powershell
# Test connectivity to license server
Test-NetConnection -ComputerName "LicenseServer" -Port 27000

# Test license manager API
Invoke-WebRequest -Uri "http://LicenseServer:8080/api/health" -UseBasicParsing
```

2. **Adjust Timeouts**:
```xml
<licenseManager>
  <settings
    timeoutSeconds="30"
    retryCount="3"
    retryDelaySeconds="5"
  />
</licenseManager>
```

3. **Enable Circuit Breaker**:
```xml
<licenseManager>
  <circuitBreaker
    enabled="true"
    failureThreshold="5"
    recoveryTimeoutSeconds="60"
  />
</licenseManager>
```

#### Timer Service Integration Issues

**Symptoms**:
- Detection cycles not running
- Timer execution errors
- Scheduled tasks not executing

**Diagnosis**:
```powershell
# Check timer service status
Get-Service -Name "TimerExecutionService"

# Check scheduled timers
Get-ScheduledTask -TaskPath "\LicenseReleaseService\"

# Check timer execution logs
Get-Content -Path "C:\Logs\LicenseReleaseService\timer.log" |
    Where-Object { $_ -match "error|Error" }
```

**Solutions**:

1. **Restart Timer Service**:
```powershell
# Restart timer service
Restart-Service -Name "TimerExecutionService"

# Check if timers are registered
.\LicenseReleaseService.exe --list-timers
```

2. **Re-register Timers**:
```powershell
# Re-register all timers
.\LicenseReleaseService.exe --register-timers
```

3. **Adjust Timer Configuration**:
```xml
<timerService>
  <timers>
    <add name="IdleDetectionCycle" interval="00:02:00" enabled="true" />
    <add name="IdleDetectionHealthCheck" interval="00:10:00" enabled="true" />
  </timers>
</timerService>
```

## Advanced Troubleshooting

### 1. Debug Mode

**Enable Debug Logging**:
```xml
<logging
  logLevel="Debug"
  enableDetectionLogging="true"
  enablePerformanceLogging="true"
  enableErrorLogging="true"
  maxLogFileSizeMB="50"
/>
```

**Collect Debug Information**:
```powershell
# Collect system information
.\LicenseReleaseService.exe --system-info > system_info.txt

# Collect performance data
.\LicenseReleaseService.exe --performance-data > performance_data.txt

# Collect configuration information
.\LicenseReleaseService.exe --config-info > config_info.txt
```

### 2. Remote Diagnostics

**Enable Remote Access**:
```powershell
# Enable WinRM for remote access
Enable-PSRemoting -Force

# Set up remote diagnostics session
Enter-PSSession -ComputerName "Server01" -Credential "Domain\Admin"
```

**Remote Diagnostics Commands**:
```powershell
# Check service status on remote machine
Invoke-Command -ComputerName "Server01" -ScriptBlock {
    Get-Service -Name "LicenseReleaseService"
}

# Collect logs from remote machine
Invoke-Command -ComputerName "Server01" -ScriptBlock {
    Get-Content -Path "C:\Logs\LicenseReleaseService\detection.log" |
        Where-Object { $_ -match "error|Error" } |
        Select-Object -Last 20
}
```

### 3. Performance Profiling

**Enable Performance Counters**:
```powershell
# Create performance counters
$counterList = @(
    "\Processor(_Total)\% Processor Time",
    "\Memory\Available MBytes",
    "\Process(LicenseReleaseService)\% Processor Time",
    "\Process(LicenseReleaseService)\Private Bytes"
)

# Start performance monitoring
Start-PerformanceDataCollection -CounterList $counterList -Interval 5 -MaxSamples 100
```

**Analyze Performance Data**:
```powershell
# Analyze performance data
Get-PerformanceData |
    Where-Object { $_.Counter -like "*LicenseReleaseService*" } |
    Group-Object -Property Counter |
    Select-Object Name, @{Name="Average"; Expression={$_.Group | Measure-Object -Property Value -Average}},
        @{Name="Maximum"; Expression={$_.Group | Measure-Object -Property Value -Maximum}}
```

## Prevention and Maintenance

### 1. Regular Maintenance Tasks

**Daily Tasks**:
- Review detection logs for errors
- Monitor system performance
- Check service health status

**Weekly Tasks**:
- Analyze detection accuracy
- Review configuration settings
- Clean up old log files

**Monthly Tasks**:
- Update detection thresholds based on usage patterns
- Review and optimize configuration
- Perform comprehensive system health check

### 2. Monitoring Setup

**Essential Monitoring**:
- Service health and availability
- Detection success rates
- System resource usage
- Error rates and patterns

**Alert Configuration**:
- Service down alerts
- High error rate alerts
- Performance threshold alerts
- Configuration change alerts

### 3. Documentation

**Maintain**:
- Configuration change log
- Known issues and solutions
- Troubleshooting procedures
- Contact information for support

## Escalation Procedures

### 1. When to Escalate

**Escalate if**:
- Issue affects multiple users
- System performance severely impacted
- Security concerns identified
- Unable to resolve with standard procedures

### 2. Escalation Contacts

**Internal Resources**:
- System Administrator
- Database Administrator
- Network Administrator
- Security Team

**External Resources**:
- Software Vendor Support
- Hardware Vendor Support
- Consulting Services

### 3. Escalation Information

**Provide**:
- Detailed description of the issue
- Steps already taken to resolve
- Impact assessment
- System and configuration information
- Log files and diagnostic data

This troubleshooting guide provides comprehensive procedures for diagnosing and resolving issues with the Idle Detection system. Always document the troubleshooting process and solutions for future reference.