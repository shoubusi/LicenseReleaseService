# License Release Service - Migration Guide

## Overview

This migration guide provides comprehensive instructions for migrating to the License Release Service with Idle Detection capabilities. This guide covers upgrading from previous versions, migrating from other license management systems, and handling data migration.

## Migration Scenarios

### 1. Upgrade from Previous Version

#### Version Compatibility

| From Version | To Version | Migration Complexity | Downtime Required |
|--------------|------------|---------------------|-------------------|
| 1.x.x | 2.0.0 | Moderate | 1-2 hours |
| 2.0.0 | 2.1.0 | Low | 30 minutes |
| No previous installation | 2.0.0+ | High | 2-4 hours |

#### Prerequisites

- **Current Version**: Verify current installed version
- **Backup**: Complete system backup including configuration and database
- **System Requirements**: Ensure target system meets requirements
- **Maintenance Window**: Schedule appropriate maintenance window
- **Rollback Plan**: Have rollback procedure ready

#### Migration Process

1. **Pre-Migration Assessment**

   ```powershell
   # Check current version
   Get-ItemProperty -Path "HKLM:\SOFTWARE\LicenseReleaseService" -Name "Version"

   # Backup current configuration
   Copy-Item "C:\ProgramData\LicenseReleaseService\Config\" -Destination "C:\Backup\Config\" -Recurse

   # Backup database
   Backup-SqlDatabase -ServerInstance <server> -Database <database> -BackupFile "C:\Backup\LicenseReleaseService.bak"
   ```

2. **Stop Current Service**

   ```powershell
   Stop-Service -Name "LicenseReleaseService" -Force
   ```

3. **Database Migration**

   ```sql
   -- Create database backup
   BACKUP DATABASE [LicenseReleaseService]
   TO DISK = N'C:\Backup\LicenseReleaseService_PreMigration.bak'
   WITH NOFORMAT, NOINIT, NAME = N'LicenseReleaseService-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10

   -- Run migration script
   :r C:\Migration\DatabaseMigration.sql
   ```

4. **Configuration Migration**

   ```powershell
   # Migrate configuration settings
   $oldConfig = Get-Content "C:\Backup\Config\LicenseReleaseService.config"
   $newConfig = ConvertTo-NewConfigurationFormat -OldConfig $oldConfig
   $newConfig | Set-Content "C:\ProgramData\LicenseReleaseService\Config\LicenseReleaseService.config"
   ```

5. **Install New Version**

   ```powershell
   # Install new service version
   .\LicenseReleaseServiceSetup.exe /quiet /norestart

   # Configure service
   Set-ServiceConfiguration -ConfigFile "C:\ProgramData\LicenseReleaseService\Config\LicenseReleaseService.config"
   ```

### 2. Migration from Other License Management Systems

#### Supported Migration Sources

- **SolidWorks License Manager Only**
- **Custom License Management Solutions**
- **Third-party License Managers**
- **Manual License Management**

#### Migration Assessment

1. **Current System Analysis**

   ```powershell
   # Analyze current license usage patterns
   Get-LicenseUsageStatistics -Server <current-server> -Period Last30Days

   # Identify active users and usage patterns
   Get-ActiveUsers -Server <current-server> | Export-Csv "C:\Migration\CurrentUsers.csv"

   # Document current license allocation
   Get-LicenseAllocation -Server <current-server> | Format-Table
   ```

2. **License Inventory**

   ```powershell
   # Inventory current licenses
   $licenses = @{
       "SolidWorks Professional" = 50
       "SolidWorks Premium" = 25
       "SolidWorks Simulation" = 10
       "SolidWorks PDM" = 5
   }

   # Export license inventory
   $licenses | Export-Clixml "C:\Migration\LicenseInventory.xml"
   ```

#### Migration Process

1. **Data Extraction**

   ```powershell
   # Extract user data from current system
   Extract-UserData -SourceSystem <current-system> -OutputFile "C:\Migration\Users.json"

   # Extract license usage history
   Extract-UsageHistory -SourceSystem <current-system> -OutputFile "C:\Migration\UsageHistory.csv"

   # Extract configuration settings
   Extract-Configuration -SourceSystem <current-system> -OutputFile "C:\Migration\CurrentConfig.json"
   ```

2. **Data Transformation**

   ```powershell
   # Transform user data
   Transform-UserData -InputFile "C:\Migration\Users.json" -OutputFile "C:\Migration\TransformedUsers.json"

   # Transform usage history
   Transform-UsageHistory -InputFile "C:\Migration\UsageHistory.csv" -OutputFile "C:\Migration\TransformedUsage.csv"

   # Generate configuration for new system
   New-ConfigurationTemplate -InputFile "C:\Migration\CurrentConfig.json" -OutputFile "C:\Migration\NewConfig.json"
   ```

3. **Data Import**

   ```powershell
   # Import user data
   Import-UserData -InputFile "C:\Migration\TransformedUsers.json"

   # Import license inventory
   Import-LicenseInventory -InputFile "C:\Migration\LicenseInventory.xml"

   # Apply configuration
   Import-Configuration -InputFile "C:\Migration\NewConfig.json"
   ```

### 3. Database Migration

#### Database Schema Changes

**Version 1.x to 2.0 Schema Changes:**

```sql
-- Add new tables for idle detection
CREATE TABLE [dbo].[IdleDetectionSessions] (
    [SessionId] [uniqueidentifier] NOT NULL,
    [UserId] [nvarchar](50) NOT NULL,
    [ComputerName] [nvarchar](100) NOT NULL,
    [StartTime] [datetime] NOT NULL,
    [EndTime] [datetime] NULL,
    [LastActivityTime] [datetime] NOT NULL,
    [DetectionMethod] [nvarchar](50) NOT NULL,
    [Status] [nvarchar](20) NOT NULL,
    [CreatedDate] [datetime] NOT NULL,
    [ModifiedDate] [datetime] NOT NULL,
    CONSTRAINT [PK_IdleDetectionSessions] PRIMARY KEY CLUSTERED ([SessionId] ASC)
);

-- Add performance monitoring tables
CREATE TABLE [dbo].[PerformanceMetrics] (
    [MetricId] [uniqueidentifier] NOT NULL,
    [MetricType] [nvarchar](50) NOT NULL,
    [MetricName] [nvarchar](100) NOT NULL,
    [MetricValue] [float] NOT NULL,
    [Timestamp] [datetime] NOT NULL,
    [ComputerName] [nvarchar](100) NOT NULL,
    [AdditionalData] [nvarchar](max) NULL,
    CONSTRAINT [PK_PerformanceMetrics] PRIMARY KEY CLUSTERED ([MetricId] ASC)
);

-- Add configuration versioning
ALTER TABLE [dbo].[Configuration] ADD [ConfigVersion] [int] NOT NULL DEFAULT 1;
ALTER TABLE [dbo].[Configuration] ADD [LastModifiedBy] [nvarchar](100) NULL;
ALTER TABLE [dbo].[Configuration] ADD [LastModifiedDate] [datetime] NULL;
```

#### Database Migration Script

```sql
-- Database Migration Script for Version 2.0
-- Run this script during upgrade process

USE [LicenseReleaseService]
GO

-- Enable versioning
BEGIN TRANSACTION;
GO

-- Create backup table for existing configuration
SELECT * INTO [dbo].[Configuration_Backup] FROM [dbo].[Configuration];
GO

-- Add new columns to existing tables
ALTER TABLE [dbo].[LicenseSessions] ADD [IdleDetectionSessionId] [uniqueidentifier] NULL;
ALTER TABLE [dbo].[LicenseSessions] ADD [DetectionMethod] [nvarchar](50) NULL;
ALTER TABLE [dbo].[LicenseSessions] ADD [ActivityScore] [float] NULL;
GO

-- Create new indexes
CREATE NONCLUSTERED INDEX [IX_IdleDetectionSessions_UserId] ON [dbo].[IdleDetectionSessions] ([UserId]);
CREATE NONCLUSTERED INDEX [IX_IdleDetectionSessions_ComputerName] ON [dbo].[IdleDetectionSessions] ([ComputerName]);
CREATE NONCLUSTERED INDEX [IX_IdleDetectionSessions_Status] ON [dbo].[IdleDetectionSessions] ([Status]);
CREATE NONCLUSTERED INDEX [IX_PerformanceMetrics_Timestamp] ON [dbo].[PerformanceMetrics] ([Timestamp]);
CREATE NONCLUSTERED INDEX [IX_PerformanceMetrics_MetricType] ON [dbo].[PerformanceMetrics] ([MetricType]);
GO

-- Update existing data
UPDATE [dbo].[Configuration]
SET [ConfigVersion] = 1, [LastModifiedDate] = GETDATE()
WHERE [ConfigVersion] IS NULL;
GO

COMMIT TRANSACTION;
GO
```

### 4. Configuration Migration

#### Configuration File Changes

**Legacy Configuration (1.x):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <add key="LicenseServer" value="lic-server-01" />
    <add key="CheckInterval" value="30000" />
    <add key="IdleTimeout" value="900000" />
  </appSettings>
</configuration>
```

**New Configuration (2.0):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="licenseReleaseService" type="LicenseReleaseService.Configuration.LicenseReleaseServiceConfiguration, LicenseReleaseService" />
  </configSections>

  <licenseReleaseService>
    <licenseManager server="lic-server-01" port="25734" timeout="30000" />
    <detection checkInterval="00:00:30" />
    <thresholds warning="00:05:00" imminent="00:10:00" critical="00:15:00" />
    <monitoring enabled="true" metricsCollectionInterval="00:01:00" />
  </licenseReleaseService>
</configuration>
```

#### Configuration Migration Script

```powershell
# Configuration Migration Script
param(
    [string]$SourceConfigFile,
    [string]$TargetConfigFile
)

# Load source configuration
$sourceConfig = [xml](Get-Content $SourceConfigFile)

# Create target configuration structure
$targetConfig = New-Object xml
$targetConfig.LoadXml(@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="licenseReleaseService" type="LicenseReleaseService.Configuration.LicenseReleaseServiceConfiguration, LicenseReleaseService" />
  </configSections>

  <licenseReleaseService>
    <licenseManager />
    <detection />
    <thresholds />
    <monitoring />
  </licenseReleaseService>
</configuration>
"@)

# Migrate license manager settings
$licenseServer = $sourceConfig.configuration.appSettings.add | Where-Object { $_.key -eq "LicenseServer" }
if ($licenseServer) {
    $targetConfig.configuration.licenseReleaseService.licenseManager.server = $licenseServer.value
}

# Migrate timing settings
$checkInterval = $sourceConfig.configuration.appSettings.add | Where-Object { $_.key -eq "CheckInterval" }
if ($checkInterval) {
    $interval = [TimeSpan]::FromMilliseconds([int]$checkInterval.value)
    $targetConfig.configuration.licenseReleaseService.detection.checkInterval = $interval.ToString()
}

$idleTimeout = $sourceConfig.configuration.appSettings.add | Where-Object { $_.key -eq "IdleTimeout" }
if ($idleTimeout) {
    $timeout = [TimeSpan]::FromMilliseconds([int]$idleTimeout.value)
    $targetConfig.configuration.licenseReleaseService.thresholds.critical = $timeout.ToString()
}

# Save target configuration
$targetConfig.Save($TargetConfigFile)
Write-Host "Configuration migrated successfully to $TargetConfigFile"
```

## Testing and Validation

### Pre-Migration Testing

1. **Environment Validation**

   ```powershell
   # Test source system connectivity
   Test-SourceSystemConnectivity -Server <source-server>

   # Test target system readiness
   Test-TargetSystemReadiness -Server <target-server>

   # Validate network connectivity
   Test-NetworkConnectivity -Source <source-server> -Destination <target-server>
   ```

2. **Data Validation**

   ```powershell
   # Validate data integrity
   Test-DataIntegrity -Database <database> -Table <table>

   # Validate user data completeness
   Test-UserDataCompleteness -InputFile "C:\Migration\Users.json"

   # Validate license inventory accuracy
   Test-LicenseInventoryAccuracy -InputFile "C:\Migration\LicenseInventory.xml"
   ```

### Post-Migration Testing

1. **Functional Testing**

   ```powershell
   # Test license checkout
   Test-LicenseCheckout -User <test-user> -License <license-type>

   # Test license release
   Test-LicenseRelease -SessionId <session-id>

   # Test idle detection
   Test-IdleDetection -User <test-user> -Duration <minutes>
   ```

2. **Performance Testing**

   ```powershell
   # Test concurrent user load
   Test-ConcurrentUserLoad -UserCount <count> -Duration <minutes>

   # Test license operation performance
   Test-LicenseOperationPerformance -Operation <operation> -Iterations <count>

   # Test system resource usage
   Test-SystemResourceUsage -Duration <minutes>
   ```

## Rollback Procedures

### Immediate Rollback Triggers

- **Service Unavailability**: Service fails to start or remains unstable
- **Data Corruption**: Data integrity issues detected
- **Performance Degradation**: Performance falls below acceptable thresholds
- **License Manager Issues**: Unable to connect to license manager
- **Critical Errors**: Critical errors in log files

### Rollback Process

1. **Stop New Service**

   ```powershell
   Stop-Service -Name "LicenseReleaseService" -Force
   ```

2. **Restore Database**

   ```powershell
   Restore-SqlDatabase -ServerInstance <server> -Database <database> -BackupFile "C:\Backup\LicenseReleaseService_PreMigration.bak" -ReplaceDatabase
   ```

3. **Restore Configuration**

   ```powershell
   Copy-Item "C:\Backup\Config\" -Destination "C:\ProgramData\LicenseReleaseService\Config\" -Recurse -Force
   ```

4. **Restart Previous Version**

   ```powershell
   # Restore previous service binaries
   Copy-Item "C:\Backup\Service\" -Destination "C:\Program Files\LicenseReleaseService\" -Recurse -Force

   # Reinstall previous service version
   .\LicenseReleaseService_v1_Install.exe /quiet /norestart

   # Start service
   Start-Service -Name "LicenseReleaseService"
   ```

## Troubleshooting

### Common Migration Issues

**Configuration Migration Errors**
- Error: "Configuration section not recognized"
- Solution: Verify configuration section handler is properly registered

**Database Migration Errors**
- Error: "Database schema incompatible"
- Solution: Verify database backup integrity and run schema validation

**Service Startup Errors**
- Error: "Service failed to start"
- Solution: Check event logs and verify configuration file syntax

### Diagnostic Commands

```powershell
# Check migration logs
Get-Content "C:\ProgramData\LicenseReleaseService\Logs\Migration_*.log" -Tail 100

# Verify database schema
Invoke-Sqlcmd -ServerInstance <server> -Database <database> -Query "SELECT * FROM INFORMATION_SCHEMA.TABLES"

# Test service connectivity
Test-NetConnection -ComputerName <server> -Port 8080

# Check service status
Get-Service -Name "LicenseReleaseService" | Select-Object Status, StartType
```

## Best Practices

### Planning Best Practices

- **Test Environment**: Always test migration in a non-production environment first
- **Backup Strategy**: Maintain complete backups of all system components
- **Rollback Plan**: Have detailed rollback procedures documented and tested
- **Communication**: Communicate migration timeline and potential impact to stakeholders

### Execution Best Practices

- **Phased Approach**: Consider phased migration for large deployments
- **Monitoring**: Monitor system health throughout migration process
- **Validation**: Validate each migration step before proceeding to next
- **Documentation**: Document all migration steps, issues, and resolutions

### Post-Migration Best Practices

- **Monitoring**: Enhanced monitoring for first 72 hours post-migration
- **Performance**: Compare performance metrics with pre-migration baseline
- **User Feedback**: Gather user feedback and address issues promptly
- **Documentation**: Update all documentation to reflect new system state

## Support and Resources

### Contact Information

- **Migration Support**: migration-support@company.com
- **Technical Support**: tech-support@company.com
- **Database Team**: dba-team@company.com
- **Emergency Support**: emergency@company.com

### Resources

- **Migration Scripts**: Available in `C:\Migration\Scripts`
- **Documentation**: Available in `C:\Documentation\Migration`
- **Knowledge Base**: [Internal KB Link]
- **Training Materials**: [Training Portal Link]

---

*This migration guide should be customized based on your specific environment and requirements. Always test migration procedures in a non-production environment before applying to production systems.*