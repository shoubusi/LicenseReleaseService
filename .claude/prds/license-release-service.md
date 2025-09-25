---
name: license-release-service
description: Windows service for automatic management and release of idle SolidWorks network licenses
status: backlog
created: 2025-09-25T04:54:55Z
---

# PRD: license-release-service

## Executive Summary

The License Release Service is a Windows-based background service that automatically manages SolidWorks network licenses by detecting and releasing idle or abandoned licenses. The service runs continuously on a single Windows server, monitoring license usage and freeing up licenses that are no longer actively being used, thereby optimizing license utilization for organizations with limited SolidWorks network licenses (2-10 licenses).

## Problem Statement

SolidWorks network licenses are a limited and expensive resource that can become tied up by users who leave applications running without actively using them or disconnect from the network without properly closing SolidWorks. This results in:

- **License Exhaustion**: Active users cannot access licenses when all slots are occupied by idle sessions
- **Resource Waste**: Organizations pay for licenses that aren't being productively used
- **Manual Intervention**: IT administrators must manually monitor and release licenses, which is time-consuming and inefficient
- **User Frustration**: Employees experience delays and interruptions when licenses are unavailable

This problem is particularly acute for organizations with 2-10 licenses where each license represents a significant percentage of total capacity.

## User Stories

### Primary User: IT Administrator
**As an IT administrator**, I need to:
- Automatically release idle SolidWorks licenses without manual intervention
- Monitor license usage patterns and release activities through logs
- Configure the service based on our specific environment and policies
- Ensure the service runs reliably without disrupting active users

**Acceptance Criteria:**
- Service installs and runs as a Windows Service with automatic startup
- Configuration file allows customization of idle thresholds, check intervals, and license limits
- Detailed logging captures all license queries, detections, and release actions
- Service continues running through server reboots

### Secondary User: SolidWorks User
**As a SolidWorks user**, I need to:
- Access available licenses when I need them without waiting for manual releases
- Continue working without interruption if I'm actively using the software
- Receive minimal disruption from license management activities

**Acceptance Criteria:**
- Active users are never interrupted by the license release service
- Idle licenses are released within the configured time threshold
- Users can immediately obtain released licenses for new sessions

### Tertiary User: IT Manager
**As an IT manager**, I need to:
- Track license utilization efficiency and cost optimization
- Ensure compliance with software licensing agreements
- Maintain audit trails for license management activities

**Acceptance Criteria:**
- Log files provide sufficient detail for utilization reporting
- Service operation complies with SolidWorks licensing terms
- All license release actions are documented and traceable

## Requirements

### Functional Requirements

#### Core License Management
- **License Query**: Service must query SolidWorks Network License Manager (SNL) for current license status using lmutil.exe
- **Idle Detection**: Automatically detect licenses that are idle based on:
  - Time threshold (configurable, default 30 minutes)
  - Client host unreachability (via ping detection)
  - Borrow duration exceeding configured limits
- **License Release**: Safely release identified idle licenses using lmremove command
- **Rate Limiting**: Limit maximum releases per execution cycle (configurable, default 2 licenses)

#### Multi-Version Support
- **Version Compatibility**: Support SolidWorks versions 2020 through 2025
- **Path Configuration**: Allow configuration of different SNL installation paths for each version
- **Feature Recognition**: Handle different license feature names across versions

#### Configuration Management
- **Config File Settings**: All operational parameters configurable via App.config:
  - SNL installation paths for different versions
  - Idle time thresholds (in minutes)
  - Maximum releases per cycle
  - Service execution intervals
  - License quantity limits (2-10)
  - Log file location and verbosity levels
- **Runtime Validation**: Validate configuration parameters on service startup

#### Service Management
- **Windows Service Integration**: Install and run as proper Windows Service
- **Automatic Startup**: Configure service to start automatically with Windows
- **Timer-Based Execution**: Run license checks on configurable intervals (default 60 minutes)
- **Graceful Shutdown**: Properly handle service stop requests without orphaning operations

#### Logging and Monitoring
- **Operation Logging**: Detailed file-based logging of all service activities
- **Error Handling**: Comprehensive error logging and graceful degradation
- **Performance Metrics**: Log execution times and license release statistics
- **Log Rotation**: Basic log file management to prevent unlimited growth

### Non-Functional Requirements

#### Performance
- **Low Resource Usage**: Service must consume minimal CPU and memory (<50MB RAM, <1% CPU)
- **Fast Execution**: Complete license scan and release cycle within 10 seconds
- **Responsive Startup**: Service must start and be ready within 30 seconds
- **Minimal Network Impact**: Ping detection should use minimal bandwidth

#### Reliability
- **24/7 Operation**: Service must run continuously without manual intervention
- **Error Recovery**: Automatically recover from transient errors (network issues, SNL downtime)
- **No Data Loss**: Ensure all operations are logged before completion
- **Service Resilience**: Continue operating even if individual license operations fail

#### Security
- **Least Privilege**: Run with minimal required permissions (LocalSystem or NetworkService)
- **Secure Configuration**: Protect sensitive configuration settings
- **Audit Trail**: Maintain complete audit log of all license release actions
- **No Credential Storage**: Service must not store or transmit authentication credentials

#### Maintainability
- **Configuration-Driven**: All operational parameters configurable without code changes
- **Clear Logging**: Human-readable log messages with appropriate detail levels
- **Simple Deployment**: Single executable with configuration file deployment
- **Version Compatibility**: Support multiple SolidWorks versions without service recompilation

## Success Criteria

### Measurable Outcomes
- **License Availability**: >95% license availability during business hours
- **Idle License Recovery**: >90% of idle licenses detected and released within threshold period
- **Service Uptime**: >99.5% service availability (less than 4 hours downtime per month)
- **User Impact**: Zero confirmed cases of active user interruptions
- **Resource Efficiency**: Average CPU usage <1%, memory usage <50MB

### Key Performance Indicators
- **License Utilization Rate**: Increase from baseline to >85% efficient utilization
- **Manual Intervention Reduction**: Eliminate 100% of manual license release tasks
- **Average Wait Time**: Reduce license wait time for users to <2 minutes
- **Service Response Time**: Complete license cycle in <10 seconds average
- **Error Rate**: <0.1% failed license operations

## Constraints & Assumptions

### Technical Constraints
- **Windows Only**: Service runs exclusively on Windows Server environments
- **.NET Framework**: Built on .NET Framework 4.8 using C#
- **SNL Dependency**: Requires SolidWorks Network License Manager to be installed
- **No External APIs**: Integration limited to lmutil.exe command-line tool
- **Single Instance**: Designed for single-server deployment, no clustering or load balancing

### Environmental Assumptions
- **Network Access**: Server can ping client workstations for idle detection
- **Administrative Rights**: Service runs with sufficient privileges to manage licenses
- **File System Access**: Write access to log file directory
- **SNL Installation**: SolidWorks Network License Manager properly installed and configured
- **Windows Environment**: Standard Windows Server configuration with required .NET Framework

### Operational Constraints
- **No GUI**: No graphical user interface - configuration file only
- **No High Availability**: Single point of failure acceptable
- **No Database**: File-based configuration and logging only
- **Limited Integration**: No external system integrations
- **Manual Updates**: Configuration changes require service restart

## Out of Scope

### Features Not Included
- **Web Dashboard**: No web-based monitoring or management interface
- **Mobile App**: No mobile device management capabilities
- **Database Integration**: No database storage for historical data
- **Email Notifications**: No automated email alerts or reports
- **REST API**: No programmatic API for external integration
- **Multi-Server Coordination**: No clustering or distributed operation
- **License Borrowing**: No management of borrowed licenses
- **Usage Analytics**: No advanced analytics or reporting
- **User Notifications**: No direct notifications to affected users

### Technical Limitations
- **Cross-Platform Support**: Windows only, no Linux or macOS support
- **Cloud Deployment**: Not designed for cloud environments
- **Container Support**: Not optimized for containerized deployment
- **Real-Time Monitoring**: No real-time dashboards or live monitoring
- **Advanced Scheduling**: Basic timer-based execution only
- **Machine Learning**: No predictive analytics or intelligent optimization

## Dependencies

### External Dependencies
- **SolidWorks Network License Manager 2020-2025**: Must be installed on target server
- **.NET Framework 4.8**: Required runtime environment
- **Windows Server**: Compatible Windows Server operating system
- **lmutil.exe**: SolidWorks license management utility (included with SNL)
- **Windows Service Infrastructure**: Standard Windows service management components

### Internal Dependencies
- **IT Administrative Access**: Installation and configuration require local administrator rights
- **Network Configuration**: Server must be able to communicate with client workstations
- **File System Permissions**: Write access to log file directory
- **Service Management**: Access to Windows Service control mechanisms
- **System Resources**: Adequate CPU, memory, and disk space for operation

### Third-Party Dependencies
- **None**: No third-party libraries or services required beyond Windows and SolidWorks components

## Implementation Timeline

### Phase 1: Core Service (2-3 weeks)
- Basic Windows Service implementation
- License query and parsing functionality
- Simple idle detection based on time threshold
- Configuration file structure
- Basic logging system

### Phase 2: Advanced Features (1-2 weeks)
- Multi-version SolidWorks support
- Ping-based idle detection
- Rate limiting and release controls
- Enhanced error handling
- Service installation and deployment

### Phase 3: Testing & Deployment (1 week)
- Integration testing with actual SNL server
- Performance and reliability testing
- Documentation and deployment guides
- User acceptance testing
- Production deployment

## Risk Assessment

### Technical Risks
- **SNL Version Compatibility**: Risk of breaking changes between SolidWorks versions
- **Network Reliability**: Ping detection may fail in restricted network environments
- **Service Stability**: Windows Service crashes could disrupt license management
- **Configuration Errors**: Invalid configuration could cause service failures

### Operational Risks
- **License Interruption**: Risk of releasing active user licenses
- **Service Downtime**: Service failures could prevent automatic license management
- **Resource Contention**: Could conflict with other SNL management tools
- **Security Concerns**: Running with elevated privileges poses security risks

### Mitigation Strategies
- **Comprehensive Testing**: Test with all supported SolidWorks versions
- **Conservative Thresholds**: Use conservative idle detection defaults
- **Graceful Degradation**: Continue operating with reduced functionality when possible
- **Detailed Logging**: Provide thorough logging for troubleshooting
- **Configuration Validation**: Validate all parameters on startup