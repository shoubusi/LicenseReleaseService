---
name: license-release-service
status: in-progress
created: 2025-09-25T04:56:46Z
progress: 18%
updated: 2025-09-25T07:39:38Z
prd: .claude/prds/license-release-service.md
github: https://github.com/shoubusi/LicenseReleaseService/issues/1
---

# Epic: license-release-service

## Overview

A Windows Service implementation for automatic SolidWorks network license management that detects idle licenses and releases them to optimize utilization. The service integrates with SolidWorks Network License Manager (SNL) via lmutil.exe command-line tool and provides configurable idle detection, rate limiting, and comprehensive logging for organizations with 2-10 licenses.

## Architecture Decisions

### Key Technical Decisions
- **Windows Service Architecture**: Use .NET Framework 4.8 Windows Service template for background operation and automatic startup
- **Configuration-Driven Design**: All parameters configurable via App.config to avoid recompilation for different environments
- **Command-Line Integration**: Leverage existing lmutil.exe instead of reimplementing license management protocols
- **Timer-Based Execution**: System.Timers.Timer for periodic license checks with configurable intervals
- **File-Based Logging**: Simple text file logging with rotation for audit trails and troubleshooting

### Technology Stack
- **Runtime**: .NET Framework 4.8 (C#)
- **Service Framework**: System.ServiceProcess
- **Process Execution**: System.Diagnostics.Process
- **Network Detection**: System.Net.NetworkInformation.Ping
- **Configuration**: System.Configuration
- **Logging**: System.IO.File with basic rotation
- **Timer**: System.Timers.Timer

### Design Patterns
- **Service Pattern**: Windows Service lifecycle management
- **Configuration Pattern**: External configuration file with validation
- **Command Pattern**: Encapsulate license operations as discrete commands
- **Observer Pattern**: Timer event handling for periodic execution
- **Strategy Pattern**: Pluggable idle detection strategies (time-based, ping-based)

## Technical Approach

### Backend Components

#### Core Service Classes
- **LicenseReleaserService**: Main Windows Service class inheriting from ServiceBase
- **LicenseManager**: Business logic for license queries, detection, and releases
- **ConfigurationManager**: Handles config file loading and validation
- **LoggingService**: Centralized logging with file rotation
- **ProcessExecutor**: Wrapper for lmutil.exe command execution
- **IdleDetector**: Implements multiple idle detection strategies

#### Data Models
```csharp
// License information model
public class LicenseInfo
{
    public string UserHost { get; set; }
    public string Feature { get; set; }
    public DateTime BorrowTime { get; set; }
    public string ClientHost { get; set; }
    public bool IsIdle { get; set; }
    public string IdleReason { get; set; }
}

// Configuration model
public class ServiceConfig
{
    public Dictionary<string, string> SnlPaths { get; set; }
    public int IdleMinutes { get; set; }
    public int MaxReleasesPerRun { get; set; }
    public double TimerIntervalMinutes { get; set; }
    public string LogFile { get; set; }
    public string LogLevel { get; set; }
}
```

#### Key Integration Points
- **lmutil.exe Integration**: Process execution with output parsing using regex
- **SNL Server Communication**: Command-line interface via localhost
- **Windows Service Control**: Integration with Services.msc for lifecycle management
- **File System**: Log file management with basic rotation

### Implementation Strategy

#### Phase 1: Core Service Foundation
1. **Windows Service Setup**: Create service base with proper lifecycle management
2. **Configuration System**: Implement App.config with validation
3. **Process Integration**: Build lmutil.exe wrapper with error handling
4. **Basic License Query**: Implement lmstat command parsing
5. **Simple Logging**: File-based logging with level filtering

#### Phase 2: Advanced Features
1. **Multi-Version Support**: Add configuration for multiple SolidWorks versions
2. **Idle Detection**: Implement time-based and ping-based detection strategies
3. **Rate Limiting**: Add controlled license release limits per cycle
4. **Error Handling**: Comprehensive exception handling and recovery
5. **Service Installation**: Create installer for deployment

#### Phase 3: Testing & Deployment
1. **Integration Testing**: Test with actual SNL server environments
2. **Performance Testing**: Validate resource usage and response times
3. **Deployment Automation**: Create installation scripts and documentation
4. **Monitoring Setup**: Establish log monitoring and alerting procedures

## Task Breakdown Preview

### Core Service Tasks
- [ ] **Windows Service Implementation**: Create main service class with lifecycle management
- [ ] **Configuration Management**: Implement App.config structure with validation
- [ ] **Process Execution Wrapper**: Build lmutil.exe integration with error handling
- [ ] **License Query Engine**: Parse lmstat output and extract license information
- [ ] **Basic Logging System**: File-based logging with rotation and levels

### Advanced Feature Tasks
- [ ] **Multi-Version Support**: Add configuration for SolidWorks 2020-2025
- [ ] **Idle Detection Strategies**: Implement time-based and ping-based detection
- [ ] **License Release Logic**: Add rate limiting and safe release mechanisms
- [ ] **Error Recovery**: Implement graceful degradation and retry logic
- [ ] **Service Installation**: Create Windows Service installer and deployment scripts

### Testing & Quality Tasks
- [ ] **Unit Testing**: Test individual components with mocked dependencies
- [ ] **Integration Testing**: Test end-to-end with actual SNL server
- [ ] **Performance Testing**: Validate resource usage and response times
- [ ] **Documentation**: Create deployment guide and configuration reference
- [ ] **Production Deployment**: Install and configure in production environment

## Dependencies

### External Dependencies
- **SolidWorks Network License Manager 2020-2025**: Must be pre-installed on target server
- **.NET Framework 4.8**: Runtime environment requirement
- **Windows Server 2016+**: Operating system requirement
- **lmutil.exe**: SolidWorks license management utility

### Internal Dependencies
- **Administrative Access**: Required for service installation and configuration
- **Network Connectivity**: Server must be able to ping client workstations
- **File System Permissions**: Write access for log files in configured directory
- **Windows Service Infrastructure**: Standard Windows service management components

### Prerequisites
- SolidWorks Network License Manager installation and configuration
- Windows Server with .NET Framework 4.8 installed
- Network configuration allowing server-to-client workstation communication
- Local administrator rights for installation and initial configuration

## Success Criteria (Technical)

### Performance Benchmarks
- **Memory Usage**: <50MB RAM during normal operation
- **CPU Usage**: <1% average CPU utilization
- **Execution Time**: <10 seconds for complete license scan cycle
- **Startup Time**: <30 seconds from service start to operational readiness
- **Network Impact**: Minimal bandwidth usage for ping operations

### Quality Gates
- **Test Coverage**: >80% unit test coverage for core components
- **Error Handling**: 100% of operations have proper error handling and logging
- **Configuration Validation**: All configuration parameters validated on startup
- **Service Stability**: 99.5% uptime with automatic recovery capabilities
- **License Safety**: Zero releases of active user licenses in testing

### Acceptance Criteria
- Service installs successfully as Windows Service with automatic startup
- License queries correctly identify all active licenses for configured versions
- Idle detection accurately identifies licenses exceeding time thresholds
- Ping detection properly identifies unreachable client workstations
- License releases execute successfully via lmutil commands
- All operations are logged with appropriate detail levels
- Service continues operating through individual operation failures
- Configuration changes are validated and applied on service restart

## Estimated Effort

### Overall Timeline: 4-6 weeks
- **Phase 1 (Core Service)**: 2-3 weeks
- **Phase 2 (Advanced Features)**: 1-2 weeks
- **Phase 3 (Testing & Deployment)**: 1 week

### Resource Requirements
- **Developer**: 1 full-time developer with C# and Windows Service experience
- **Testing Environment**: Windows Server with SolidWorks SNL installation
- **IT Support**: Limited IT support for installation and network configuration

### Critical Path Items
1. Windows Service foundation and lifecycle management
2. lmutil.exe integration and output parsing
3. Multi-version SNL path configuration
4. Idle detection algorithm implementation
5. Service installation and deployment automation

### Risk Mitigation
- Start with single SolidWorks version support, then expand
- Implement conservative idle detection defaults initially
- Build comprehensive logging from the beginning for troubleshooting
- Create configuration validation to prevent service startup with invalid settings
- Plan for incremental testing with each major component

## Tasks Created
- [ ] 001.md - Windows Service Implementation (parallel: true)
- [ ] 002.md - Configuration Management (parallel: true)
- [ ] 003.md - Process Execution Wrapper (parallel: true)
- [ ] 004.md - License Query Engine (parallel: false, depends_on: [003])
- [ ] 005.md - Basic Logging System (parallel: false, depends_on: [003])
- [ ] 006.md - Timer-Based Execution (parallel: false, depends_on: [003])
- [ ] 007.md - Multi-Version Support (parallel: false, depends_on: [002])
- [ ] 008.md - Idle Detection Strategies (parallel: false, depends_on: [004, 006])
- [ ] 009.md - License Release Logic (parallel: false, depends_on: [003, 008])
- [ ] 010.md - Error Recovery and Service Installation (parallel: false, depends_on: [001, 003, 005])

### Task Summary
- **Total tasks**: 10
- **Parallel tasks**: 3 (can be worked simultaneously)
- **Sequential tasks**: 7 (have dependencies)
- **Total estimated effort**: 168 hours
- **Average task size**: Medium to Large
- **Critical path**: 001 → 003 → 004/006 → 008 → 009 → 010