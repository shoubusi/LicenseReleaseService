# Idle Detection Architecture

## Overview

The Idle Detection system provides a robust, multi-strategy approach to detecting inactive SolidWorks sessions. This architecture combines time-based and ping-based detection methods with configurable thresholds, consensus logic, and comprehensive monitoring to ensure accurate license release decisions.

## Core Components

### 1. IdleDetectionEngine
**File**: `LicenseReleaseService/IdleDetection/IdleDetectionEngine.cs`

The main orchestration engine that coordinates multiple detection strategies and manages the overall detection lifecycle.

**Key Features**:
- Manages multiple detector implementations
- Implements consensus-based decision making
- Provides comprehensive event system
- Handles lifecycle management (initialize, start, stop, dispose)
- Offers detailed statistics and health monitoring
- Supports graceful degradation and error recovery

**Responsibilities**:
- Register and manage idle detectors
- Coordinate detection cycles via timer service
- Aggregate results using consensus engine
- Monitor detector health and performance
- Provide status and health information

### 2. DetectionConsensusEngine
**File**: `LicenseReleaseService/IdleDetection/DetectionConsensusEngine.cs`

Implements weighted consensus logic to combine results from multiple detection strategies.

**Key Features**:
- Configurable confidence thresholds
- Weighted voting system
- Hysteresis to prevent rapid state changes
- Graceful handling of detector failures
- Real-time confidence scoring

### 3. SessionStateManager
**File**: `LicenseReleaseService/IdleDetection/SessionStateManager.cs`

Manages session state information and implements hysteresis logic to prevent false positives.

**Key Features**:
- Session lifecycle management
- State transition validation
- Historical state tracking
- Configurable hysteresis periods
- Session isolation and security

## Detection Strategies

### 1. Time-Based Detection
**File**: `LicenseReleaseService/IdleDetection/TimeBasedIdleDetector.cs`

Implements time-based idle detection using configurable thresholds and adaptive learning.

**Key Features**:
- Multi-level graduated detection (Warning, Imminent, Critical, Release)
- Configurable time thresholds
- Work hour sensitivity with multipliers
- Weekend and holiday adjustments
- Adaptive threshold learning
- System activity monitoring integration
- Comprehensive activity tracking

### 2. Ping-Based Detection
**File**: `LicenseReleaseService/IdleDetection/PingBasedIdleDetector.cs`

Implements Windows API-based process monitoring to detect application activity.

**Key Features**:
- Process existence verification
- Application window state monitoring
- CPU and memory utilization tracking
- Network activity detection
- Configurable ping intervals
- Process health validation

### 3. Activity Monitoring Service
**File**: `LicenseReleaseService/IdleDetection/ActivityMonitoringService.cs`

Comprehensive activity monitoring that combines multiple monitoring strategies.

**Key Features**:
- File system activity monitoring
- Performance monitoring
- System activity tracking
- Real-time event processing
- Activity pattern analysis
- Anomaly detection

### 4. Supporting Components

#### FileSystemMonitor
**File**: `LicenseReleaseService/IdleDetection/FileSystemMonitor.cs`

Monitors file system activity for SolidWorks document access and modifications.

**Key Features**:
- Document access tracking
- File modification detection
- Directory monitoring
- File type filtering
- Activity correlation

#### SystemActivityTracker
**File**: `LicenseReleaseService/IdleDetection/SystemActivityTracker.cs`

Tracks system-wide activity including input devices, power state, and system events.

**Key Features**:
- Keyboard and mouse activity monitoring
- Power state change detection
- Screen saver activation tracking
- System event monitoring
- Activity pattern analysis

#### PerformanceMonitor
**File**: `LicenseReleaseService/IdleDetection/PerformanceMonitor.cs`

Monitors system and process performance metrics.

**Key Features**:
- CPU utilization tracking
- Memory usage monitoring
- Disk I/O monitoring
- Network activity monitoring
- Performance baseline establishment

## Configuration Management

### TimeBasedDetectionConfig
**File**: `LicenseReleaseService/IdleDetection/TimeBasedDetectionConfig.cs`

Comprehensive configuration for time-based detection parameters.

**Key Configuration Sections**:
- **Detection Intervals**: Configurable detection frequency
- **Threshold Settings**: Warning, imminent, and critical thresholds
- **Work Hour Configuration**: Work hour definitions and multipliers
- **Adaptive Learning**: Learning rate and threshold bounds
- **Monitoring Settings**: Sample rates and timeouts
- **Feature Flags**: Enable/disable specific features

### Configuration Validation
All configuration classes include built-in validation to ensure:
- Proper threshold progression
- Valid time formats
- Reasonable multiplier values
- Valid confidence ranges
- Proper parameter relationships

## Event System

The architecture implements a comprehensive event system for real-time monitoring and integration:

### Engine Events
- `IdleDetected`: Raised when idle state is confirmed
- `ActivityDetected`: Raised when activity is detected
- `StateChanged`: Raised when engine state changes
- `ErrorOccurred`: Raised when errors occur
- `DetectionCycleCompleted`: Raised when detection cycle completes

### Detector Events
- Individual detector events for fine-grained monitoring
- Error handling and recovery events
- Health status events

## Consensus Algorithm

The system uses a sophisticated consensus algorithm that:

1. **Weighted Voting**: Each detector contributes based on reliability
2. **Confidence Scoring**: Results include confidence levels (0.0-1.0)
3. **Hysteresis**: Prevents rapid state changes
4. **Graceful Degradation**: Handles detector failures gracefully
5. **Dynamic Adjustment**: Adapts to changing conditions

## Error Handling and Resilience

### Error Classification
- **Critical Errors**: Stop detection and alert administrators
- **Medium Errors**: Log and continue with reduced functionality
- **Low Errors**: Log and continue normally

### Recovery Strategies
- Automatic retry with exponential backoff
- Circuit breaker pattern for repeated failures
- Graceful degradation when detectors fail
- Health monitoring and self-healing

## Performance Characteristics

### Resource Usage
- CPU: <5% overhead during normal operation
- Memory: Minimal footprint with efficient data structures
- Disk: Limited to configuration and logging
- Network: Minimal, only for license management integration

### Scalability
- Supports hundreds of concurrent sessions
- Horizontal scaling through distributed architecture
- Configurable concurrency limits
- Efficient resource utilization

## Integration Points

### License Management Integration
- Seamless integration with existing license query logic
- License status verification
- User session tracking
- Release decision coordination

### Timer Service Integration
- Configurable detection intervals
- Scheduled execution
- Performance monitoring
- Error handling

### Configuration Management Integration
- Dynamic configuration updates
- Configuration validation
- Migration support
- Health monitoring

## Security Considerations

### Data Protection
- No sensitive user data collection
- Anonymous activity tracking
- Secure configuration storage
- Audit logging for compliance

### Access Control
- Service-based architecture with limited permissions
- Process isolation
- Secure inter-process communication
- Role-based access control

## Monitoring and Observability

### Metrics Collection
- Detection success rates
- Response times
- Error rates
- Resource utilization
- Detector health status

### Health Monitoring
- Component health checks
- Service availability monitoring
- Performance baseline tracking
- Anomaly detection

## Extensibility

### Plugin Architecture
- Easy addition of new detection strategies
- Configurable detector priority
- Dynamic detector registration
- Custom event handling

### Customization Points
- Custom consensus algorithms
- Configurable thresholds and multipliers
- Custom activity monitors
- Integration with external systems

## Deployment Architecture

### Service Requirements
- Windows Service installation
- .NET Framework 4.8 or later
- Windows API access for process monitoring
- Configuration file access
- Event log access

### Dependencies
- License Query Logic (Task 004)
- Timer Execution Service (Task 006)
- Configuration Management (Task 002)
- Multi-Version Support (Issue #8)
- Process Execution System

## Best Practices

### Configuration
- Start with conservative thresholds
- Monitor detection accuracy
- Adjust based on usage patterns
- Regular configuration reviews
- Backup configurations before changes

### Monitoring
- Monitor detection success rates
- Track false positive/negative rates
- Monitor resource utilization
- Set up appropriate alerts
- Regular health checks

### Maintenance
- Regular log reviews
- Performance optimization
- Configuration updates
- Security patches
- Capacity planning

This architecture provides a solid foundation for accurate, reliable idle detection that integrates seamlessly with the existing License Release Service while providing the flexibility to adapt to changing requirements and usage patterns.