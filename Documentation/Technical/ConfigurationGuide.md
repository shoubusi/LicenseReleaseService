# Idle Detection Configuration Guide

## Overview

This guide provides comprehensive instructions for configuring the Idle Detection system. The configuration system is designed to be flexible, self-validating, and easily manageable while providing fine-grained control over detection behavior.

## Configuration Structure

### Main Configuration Location
The idle detection configuration is stored in the `app.config` or `web.config` file within the `<idleDetection>` section.

### Configuration Hierarchy
```
<configuration>
  <configSections>
    <section name="idleDetection" type="LicenseReleaseService.IdleDetection.IdleDetectionConfiguration, LicenseReleaseService" />
  </configSections>
  <idleDetection>
    <timeBasedDetection ... />
    <pingBasedDetection ... />
    <activityMonitoring ... />
  </idleDetection>
</configuration>
```

## Time-Based Detection Configuration

### Section: `timeBasedDetection`

#### Detection Parameters
```xml
<timeBasedDetection
  detectionIntervalSeconds="60"
  warningThresholdMinutes="5"
  imminentThresholdMinutes="10"
  criticalThresholdMinutes="15"
  hysteresisMinutes="2"
  confidenceThreshold="0.7"
/>
```

**Parameters**:
- `detectionIntervalSeconds`: How often to check for idle state (10-3600 seconds)
- `warningThresholdMinutes`: Time before warning state (1-120 minutes)
- `imminentThresholdMinutes`: Time before imminent state (2-240 minutes)
- `criticalThresholdMinutes`: Time before critical state (5-480 minutes)
- `hysteresisMinutes`: Hysteresis period to prevent false positives (0-30 minutes)
- `confidenceThreshold`: Minimum confidence for detection (0.0-1.0)

#### Work Hour Configuration
```xml
<timeBasedDetection
  enableWorkHours="true"
  workHourStart="09:00"
  workHourEnd="17:00"
  workDayMultiplier="1.0"
  offHourMultiplier="0.5"
  weekendMultiplier="0.3"
  holidaysMultiplier="0.2"
/>
```

**Parameters**:
- `enableWorkHours`: Enable work hour sensitivity
- `workHourStart`/`workHourEnd`: Work hour boundaries (HH:MM format)
- `workDayMultiplier`: Multiplier during work hours
- `offHourMultiplier`: Multiplier outside work hours
- `weekendMultiplier`: Multiplier during weekends
- `holidaysMultiplier`: Multiplier during holidays

#### Adaptive Learning Configuration
```xml
<timeBasedDetection
  enableAdaptiveThresholds="true"
  adaptiveLearningRate="0.1"
  minAdaptiveThreshold="0.1"
  maxAdaptiveThreshold="2.0"
  enableGraduatedDetection="true"
/>
```

**Parameters**:
- `enableAdaptiveThresholds`: Enable adaptive threshold learning
- `adaptiveLearningRate`: Learning rate for threshold adjustment (0.01-0.5)
- `minAdaptiveThreshold`/`maxAdaptiveThreshold`: Bounds for adaptive thresholds
- `enableGraduatedDetection`: Enable graduated detection levels

#### Monitoring Configuration
```xml
<timeBasedDetection
  enableKeyboardMonitoring="true"
  enableMouseMonitoring="true"
  enableSystemMonitoring="true"
  enableSolidWorksMonitoring="true"
  monitoringSampleRateMs="100"
  maxDetectionTimeMs="5000"
/>
```

**Parameters**:
- `enableKeyboardMonitoring`: Enable keyboard activity monitoring
- `enableMouseMonitoring`: Enable mouse activity monitoring
- `enableSystemMonitoring`: Enable system activity monitoring
- `enableSolidWorksMonitoring`: Enable SolidWorks-specific monitoring
- `monitoringSampleRateMs`: Sample rate for monitoring (10-1000ms)
- `maxDetectionTimeMs`: Maximum time for detection (1000-30000ms)

## Ping-Based Detection Configuration

### Section: `pingBasedDetection`

#### Basic Configuration
```xml
<pingBasedDetection
  pingIntervalSeconds="30"
  pingTimeoutMs="5000"
  maxPingFailures="3"
  enableProcessMonitoring="true"
  enableWindowMonitoring="true"
  enableNetworkMonitoring="true"
/>
```

**Parameters**:
- `pingIntervalSeconds`: Interval between ping attempts (10-300 seconds)
- `pingTimeoutMs`: Timeout for each ping attempt (1000-30000ms)
- `maxPingFailures`: Maximum consecutive failures before considering process inactive (1-10)
- `enableProcessMonitoring`: Enable process existence monitoring
- `enableWindowMonitoring`: Enable window state monitoring
- `enableNetworkMonitoring`: Enable network activity monitoring

#### Process Configuration
```xml
<pingBasedDetection
  processNames="SLDWORKS.exe,solidworks.exe"
  includeChildProcesses="false"
  processPriorityClass="Normal"
  minProcessMemoryMB="100"
  maxProcessCpuPercent="95"
/>
```

**Parameters**:
- `processNames`: Comma-separated list of process names to monitor
- `includeChildProcesses`: Include child processes in monitoring
- `processPriorityClass`: Minimum priority class for active processes
- `minProcessMemoryMB`: Minimum memory usage for active processes
- `maxProcessCpuPercent`: Maximum CPU usage for inactive detection

#### Window Configuration
```xml
<pingBasedDetection
  checkWindowVisibility="true"
  checkWindowFocus="true"
  checkWindowPosition="false"
  minVisibleAreaPercent="10"
  focusChangeTimeoutMs="2000"
/>
```

**Parameters**:
- `checkWindowVisibility`: Check if application windows are visible
- `checkWindowFocus`: Check if application windows have focus
- `checkWindowPosition`: Check window position changes
- `minVisibleAreaPercent`: Minimum visible area percentage (1-100)
- `focusChangeTimeoutMs`: Timeout for focus changes (100-10000ms)

## Activity Monitoring Configuration

### Section: `activityMonitoring`

#### General Settings
```xml
<activityMonitoring
  enableFileSystemMonitoring="true"
  enablePerformanceMonitoring="true"
  enableSystemActivityTracking="true"
  activityHistorySize="1000"
  activityAnalysisIntervalMs="5000"
/>
```

**Parameters**:
- `enableFileSystemMonitoring`: Enable file system activity monitoring
- `enablePerformanceMonitoring`: Enable performance monitoring
- `enableSystemActivityTracking`: Enable system activity tracking
- `activityHistorySize`: Number of activities to keep in history (100-10000)
- `activityAnalysisIntervalMs`: Interval for activity analysis (1000-60000ms)

#### File System Monitoring
```xml
<activityMonitoring
  monitoredExtensions=".sldprt,.sldasm,.slddrw"
  monitoredDirectories=""
  includeSubdirectories="true"
  fileAccessThresholdMs="100"
  directoryChangeBufferSize="8192"
/>
```

**Parameters**:
- `monitoredExtensions`: Comma-separated file extensions to monitor
- `monitoredDirectories`: Comma-separated directories to monitor
- `includeSubdirectories`: Include subdirectories in monitoring
- `fileAccessThresholdMs`: Threshold for file access activity (10-10000ms)
- `directoryChangeBufferSize`: Buffer size for directory changes (1024-65536 bytes)

#### Performance Monitoring
```xml
<activityMonitoring
  cpuUsageThreshold="10"
  memoryUsageThresholdMB="50"
  diskIOThresholdMB="1"
  networkActivityThresholdKB="100"
  performanceSampleIntervalMs="1000"
/>
```

**Parameters**:
- `cpuUsageThreshold`: CPU usage threshold for activity (1-100%)
- `memoryUsageThresholdMB`: Memory usage threshold (1-1000MB)
- `diskIOThresholdMB`: Disk I/O threshold (0.1-100MB)
- `networkActivityThresholdKB`: Network activity threshold (1-10000KB)
- `performanceSampleIntervalMs`: Performance sampling interval (100-10000ms)

## Consensus Engine Configuration

### Section: `consensusEngine`

#### Basic Settings
```xml
<consensusEngine
  enableConsensusLogic="true"
  consensusThreshold="0.7"
  minDetectorsForConsensus="2"
  enableHysteresis="true"
  hysteresisDurationMinutes="2"
/>
```

**Parameters**:
- `enableConsensusLogic`: Enable consensus-based decision making
- `consensusThreshold`: Threshold for consensus decision (0.0-1.0)
- `minDetectorsForConsensus`: Minimum detectors required for consensus (1-10)
- `enableHysteresis`: Enable hysteresis to prevent rapid changes
- `hysteresisDurationMinutes`: Hysteresis duration (0-60 minutes)

#### Detector Weights
```xml
<consensusEngine
  timeBasedWeight="0.4"
  pingBasedWeight="0.4"
  activityMonitoringWeight="0.2"
  confidenceBoostFactor="1.1"
  confidencePenaltyFactor="0.9"
/>
```

**Parameters**:
- `timeBasedWeight`: Weight for time-based detection (0.0-1.0)
- `pingBasedWeight`: Weight for ping-based detection (0.0-1.0)
- `activityMonitoringWeight`: Weight for activity monitoring (0.0-1.0)
- `confidenceBoostFactor`: Factor for confidence boosting (0.5-2.0)
- `confidencePenaltyFactor`: Factor for confidence penalty (0.1-1.0)

## Logging and Diagnostics Configuration

### Section: `logging`

```xml
<logging
  enableDetectionLogging="true"
  enablePerformanceLogging="true"
  enableErrorLogging="true"
  logLevel="Information"
  logRetentionDays="30"
  maxLogFileSizeMB="10"
/>
```

**Parameters**:
- `enableDetectionLogging`: Enable detection activity logging
- `enablePerformanceLogging`: Enable performance metrics logging
- `enableErrorLogging`: Enable error logging
- `logLevel`: Minimum log level (Debug, Information, Warning, Error, Critical)
- `logRetentionDays`: Log file retention period (1-365 days)
- `maxLogFileSizeMB`: Maximum log file size (1-100MB)

## Example Configuration

### Complete Configuration Example
```xml
<configuration>
  <configSections>
    <section name="idleDetection" type="LicenseReleaseService.IdleDetection.IdleDetectionConfiguration, LicenseReleaseService" />
  </configSections>

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

    <logging
      enableDetectionLogging="true"
      enablePerformanceLogging="true"
      enableErrorLogging="true"
      logLevel="Information"
      logRetentionDays="30"
      maxLogFileSizeMB="10"
    />
  </idleDetection>
</configuration>
```

## Configuration Validation

### Automatic Validation
The configuration system automatically validates all parameters and reports errors:

1. **Range Validation**: Ensures numeric values are within acceptable ranges
2. **Type Validation**: Verifies data types are correct
3. **Relationship Validation**: Checks parameter relationships (e.g., thresholds must be progressive)
4. **Format Validation**: Validates string formats (e.g., time formats, file extensions)

### Validation Errors
Common validation errors and their solutions:

| Error | Solution |
|-------|----------|
| "Warning threshold must be less than imminent threshold" | Ensure WarningThresholdMinutes < ImminentThresholdMinutes |
| "Invalid work hour start format" | Use HH:MM format for time values |
| "Confidence threshold must be between 0.0 and 1.0" | Set value between 0.0 and 1.0 |
| "Process names cannot be empty" | Provide valid process names |
| "File extensions must start with dot" | Use format ".ext" for extensions |

## Configuration Management Best Practices

### 1. Start Conservative
- Begin with longer thresholds and conservative settings
- Monitor detection accuracy and adjust gradually
- Test in non-production environments first

### 2. Document Changes
- Keep a change log for configuration modifications
- Document reasons for parameter changes
- Record the impact of changes on detection accuracy

### 3. Monitor Performance
- Regularly review detection success rates
- Monitor system resource usage
- Track false positive and negative rates

### 4. Regular Reviews
- Schedule quarterly configuration reviews
- Adjust settings based on usage patterns
- Update for seasonal usage changes

### 5. Backup and Restore
- Always backup configuration before making changes
- Test configuration changes in staging
- Have rollback procedures ready

## Configuration Tools and Utilities

### Configuration Validator
The system includes a built-in configuration validator that can be run via:
```powershell
.\LicenseReleaseService.exe --validate-config
```

### Configuration Export/Import
Export and import configurations for backup and deployment:
```powershell
.\LicenseReleaseService.exe --export-config backup.json
.\LicenseReleaseService.exe --import-config backup.json
```

### Configuration Testing
Test configuration changes without restarting the service:
```powershell
.\LicenseReleaseService.exe --test-config new_config.json
```

This configuration guide provides the foundation for effectively managing the Idle Detection system. Always refer to the specific version documentation for the most up-to-date configuration options and best practices.