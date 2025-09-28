# Idle Detection Integration Guide

## Overview

This guide provides comprehensive instructions for integrating the Idle Detection system with existing components and external systems. The Idle Detection system is designed to integrate seamlessly with the License Release Service architecture while providing extensible integration points for custom solutions.

## System Integration Architecture

### Integration Points
```
License Release Service
├── Idle Detection System
│   ├── IdleDetectionEngine (Main orchestrator)
│   ├── Detection Strategies
│   │   ├── TimeBasedDetection
│   │   ├── PingBasedDetection
│   │   └── ActivityMonitoring
│   └── Consensus Engine
├── License Manager Integration
├── Timer Service Integration
├── Configuration Management
└── Event System
```

## Core System Integration

### 1. License Manager Integration

#### Integration Overview
The Idle Detection system integrates with the license manager to:
- Verify license status before making release decisions
- Track user sessions and license associations
- Coordinate license release operations
- Handle license-related errors and recovery

#### Implementation Details
**File**: `LicenseReleaseService/IdleDetection/LicenseManagerIntegration.cs`

```csharp
public class LicenseManagerIntegration
{
    private readonly ILicenseManager _licenseManager;
    private readonly ILogger<LicenseManagerIntegration> _logger;

    public async Task<LicenseVerificationResult> VerifyLicenseAsync(int processId, string userName)
    {
        // Verify license exists and is valid
        var licenseInfo = await _licenseManager.GetLicenseInfoAsync(processId, userName);

        return new LicenseVerificationResult
        {
            IsValid = licenseInfo != null && licenseInfo.Status == LicenseStatus.Active,
            LicenseInfo = licenseInfo,
            VerificationTime = DateTime.UtcNow
        };
    }

    public async Task<LicenseReleaseResult> ReleaseLicenseAsync(int processId, string userName, string reason)
    {
        // Release license with proper audit trail
        var result = await _licenseManager.ReleaseLicenseAsync(processId, userName, reason);

        // Log the release operation
        _logger.LogInformation("License released for process {ProcessId}, user {User}, reason: {Reason}",
            processId, userName, reason);

        return result;
    }
}
```

#### Integration Events
The system raises events for license-related activities:
- `LicenseVerified`: Raised when license verification completes
- `LicenseReleased`: Raised when license is successfully released
- `LicenseReleaseFailed`: Raised when license release fails
- `LicenseNotFound`: Raised when license is not found

### 2. Timer Service Integration

#### Integration Overview
The Idle Detection system uses the timer service for:
- Scheduled detection cycles
- Periodic health checks
- Configuration refresh operations
- Maintenance and cleanup tasks

#### Implementation Details
**File**: `LicenseReleaseService/IdleDetection/TimerServiceIntegration.cs`

```csharp
public class TimerServiceIntegration
{
    private readonly ITimerExecutionService _timerService;
    private readonly IdleDetectionEngine _engine;

    public async Task InitializeAsync()
    {
        // Register detection cycle timer
        await _timerService.RegisterTimerAsync(
            "IdleDetectionCycle",
            TimeSpan.FromSeconds(_engine.Configuration.DetectionIntervalSeconds),
            ExecuteDetectionCycle
        );

        // Register health check timer
        await _timerService.RegisterTimerAsync(
            "IdleDetectionHealthCheck",
            TimeSpan.FromMinutes(5),
            ExecuteHealthCheck
        );
    }

    private async Task ExecuteDetectionCycle(Guid executionId)
    {
        try
        {
            // Execute detection for all active sessions
            var sessions = await GetActiveSessions();
            foreach (var session in sessions)
            {
                await _engine.DetectIdleAsync(session.ProcessId, session.UserName, session.ComputerName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in detection cycle {ExecutionId}", executionId);
        }
    }
}
```

#### Timer Configuration
Configure timers in the application configuration:
```xml
<timerService>
  <timers>
    <add name="IdleDetectionCycle" interval="00:01:00" enabled="true" />
    <add name="IdleDetectionHealthCheck" interval="00:05:00" enabled="true" />
    <add name="ConfigurationRefresh" interval="01:00:00" enabled="true" />
  </timers>
</timerService>
```

### 3. Configuration Management Integration

#### Integration Overview
The system integrates with the configuration management system for:
- Dynamic configuration loading and validation
- Configuration change notifications
- Configuration backup and restore
- Environment-specific settings

#### Implementation Details
**File**: `LicenseReleaseService/IdleDetection/ConfigurationIntegration.cs`

```csharp
public class ConfigurationIntegration
{
    private readonly IConfigurationManager _configManager;
    private readonly IdleDetectionConfiguration _config;

    public async Task InitializeAsync()
    {
        // Load configuration
        _config = await _configManager.GetConfigurationAsync<IdleDetectionConfiguration>();

        // Register for configuration changes
        _configManager.ConfigurationChanged += OnConfigurationChanged;

        // Validate initial configuration
        await ValidateConfigurationAsync();
    }

    private async void OnConfigurationChanged(object sender, ConfigurationChangedEventArgs e)
    {
        if (e.SectionName == "idleDetection")
        {
            // Reload configuration
            var newConfig = await _configManager.GetConfigurationAsync<IdleDetectionConfiguration>();

            // Validate new configuration
            var validationResult = await ValidateConfigurationAsync(newConfig);

            if (validationResult.IsValid)
            {
                // Apply new configuration
                await ApplyConfigurationAsync(newConfig);
            }
        }
    }
}
```

#### Configuration Schema
Define configuration schema for validation:
```xml
<configurationSchema>
  <section name="idleDetection">
    <element name="timeBasedDetection" type="TimeBasedDetectionConfig" />
    <element name="pingBasedDetection" type="PingBasedDetectionConfig" />
    <element name="activityMonitoring" type="ActivityMonitoringConfig" />
    <element name="consensusEngine" type="ConsensusEngineConfig" />
  </section>
</configurationSchema>
```

## External System Integration

### 1. Monitoring and Alerting Integration

#### Integration Overview
The system integrates with external monitoring systems for:
- Performance metrics collection
- Health status reporting
- Alert generation and notification
- Log aggregation and analysis

#### Implementation Details
**File**: `LicenseReleaseService/IdleDetection/MonitoringIntegration.cs`

```csharp
public class MonitoringIntegration
{
    private readonly IMonitoringClient _monitoringClient;
    private readonly IAlertService _alertService;

    public async Task ReportMetricsAsync(EngineStatistics statistics)
    {
        // Report engine metrics
        var metrics = new Dictionary<string, double>
        {
            ["idle_detection.success_rate"] = statistics.SuccessRate,
            ["idle_detection.total_detections"] = statistics.TotalDetections,
            ["idle_detection.detector_count"] = statistics.DetectorCount,
            ["idle_detection.enabled_detector_count"] = statistics.EnabledDetectorCount
        };

        await _monitoringClient.ReportMetricsAsync(metrics);
    }

    public async Task HandleAlertAsync(AlertEventArgs args)
    {
        // Handle different alert types
        switch (args.Severity)
        {
            case AlertSeverity.Critical:
                await _alertService.SendCriticalAlertAsync(args.Message, args.Details);
                break;
            case AlertSeverity.Warning:
                await _alertService.SendWarningAlertAsync(args.Message, args.Details);
                break;
            case AlertSeverity.Information:
                await _alertService.SendInformationAlertAsync(args.Message, args.Details);
                break;
        }
    }
}
```

#### Supported Monitoring Systems
- **Prometheus**: Metrics collection and alerting
- **Grafana**: Visualization and dashboards
- **Nagios**: Infrastructure monitoring
- **Splunk**: Log aggregation and analysis
- **ELK Stack**: Centralized logging
- **Datadog**: Full-stack monitoring

### 2. Event System Integration

#### Integration Overview
The system integrates with the event system for:
- Real-time event processing
- Event-driven architecture support
- External system notifications
- Audit trail and compliance

#### Implementation Details
**File**: `LicenseReleaseService/IdleDetection/EventIntegration.cs`

```csharp
public class EventIntegration
{
    private readonly IEventBus _eventBus;
    private readonly IdleDetectionEngine _engine;

    public async Task InitializeAsync()
    {
        // Subscribe to engine events
        _engine.IdleDetected += OnIdleDetected;
        _engine.ActivityDetected += OnActivityDetected;
        _engine.ErrorOccurred += OnErrorOccurred;

        // Register event handlers
        await _eventBus.SubscribeAsync<IdleDetectionEvent>("idle.detection.detected", HandleIdleDetectionEvent);
        await _eventBus.SubscribeAsync<ActivityEvent>("idle.detection.activity", HandleActivityEvent);
    }

    private async void OnIdleDetected(object sender, IdleDetectionEventArgs e)
    {
        // Publish event to external systems
        var eventData = new IdleDetectionEvent
        {
            ProcessId = e.ProcessId,
            UserName = e.UserName,
            ComputerName = e.ComputerName,
            DetectionTime = e.DetectionTime,
            Confidence = e.Confidence,
            DetectionMethod = e.DetectionMethod
        };

        await _eventBus.PublishAsync("idle.detection.detected", eventData);
    }
}
```

#### Event Schema
Define event schemas for integration:
```json
{
  "IdleDetectionEvent": {
    "type": "object",
    "properties": {
      "processId": { "type": "integer" },
      "userName": { "type": "string" },
      "computerName": { "type": "string" },
      "detectionTime": { "type": "string", "format": "date-time" },
      "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
      "detectionMethod": { "type": "string" }
    }
  }
}
```

### 3. Database Integration

#### Integration Overview
The system integrates with databases for:
- Session state persistence
- Historical data storage
- Audit trail and compliance
- Configuration history

#### Implementation Details
**File**: `LicenseReleaseService/IdleDetection/DatabaseIntegration.cs`

```csharp
public class DatabaseIntegration
{
    private readonly IDbContext _dbContext;

    public async Task SaveSessionStateAsync(SessionState state)
    {
        var sessionRecord = new SessionRecord
        {
            ProcessId = state.ProcessId,
            UserName = state.UserName,
            ComputerName = state.ComputerName,
            State = state.State.ToString(),
            LastActivity = state.LastActivityTime,
            DetectionMethod = state.DetectionMethod,
            Confidence = state.Confidence,
            Timestamp = DateTime.UtcNow
        };

        await _dbContext.SessionRecords.AddAsync(sessionRecord);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<SessionState[]> GetSessionHistoryAsync(string userName, DateTime startDate, DateTime endDate)
    {
        return await _dbContext.SessionRecords
            .Where(s => s.UserName == userName && s.Timestamp >= startDate && s.Timestamp <= endDate)
            .OrderBy(s => s.Timestamp)
            .Select(s => new SessionState
            {
                ProcessId = s.ProcessId,
                UserName = s.UserName,
                ComputerName = s.ComputerName,
                State = Enum.Parse<SessionStateEnum>(s.State),
                LastActivityTime = s.LastActivity,
                DetectionMethod = s.DetectionMethod,
                Confidence = s.Confidence
            })
            .ToArrayAsync();
    }
}
```

#### Database Schema
```sql
CREATE TABLE SessionRecords (
    Id INT PRIMARY KEY IDENTITY,
    ProcessId INT NOT NULL,
    UserName NVARCHAR(100) NOT NULL,
    ComputerName NVARCHAR(100) NOT NULL,
    State NVARCHAR(50) NOT NULL,
    LastActivity DATETIME2 NOT NULL,
    DetectionMethod NVARCHAR(100) NOT NULL,
    Confidence DECIMAL(5,2) NOT NULL,
    Timestamp DATETIME2 NOT NULL,
    INDEX IX_SessionRecords_UserName (UserName),
    INDEX IX_SessionRecords_Timestamp (Timestamp)
);
```

## API Integration

### 1. REST API Integration

#### Integration Overview
The system provides REST API endpoints for:
- Remote configuration management
- Health status monitoring
- Manual detection triggering
- Statistics and reporting

#### API Endpoints
```csharp
[ApiController]
[Route("api/idledetection")]
public class IdleDetectionController : ControllerBase
{
    [HttpGet("health")]
    public async Task<ActionResult<EngineHealth>> GetHealth()
    {
        var health = await _engine.GetHealthAsync();
        return Ok(health);
    }

    [HttpPost("detect")]
    public async Task<ActionResult<IdleDetectionResult>> DetectIdle([FromBody] DetectionRequest request)
    {
        var result = await _engine.DetectIdleAsync(request.ProcessId, request.UserName, request.ComputerName);
        return Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<EngineStatistics>> GetStatistics()
    {
        var stats = _engine.GetStatistics();
        return Ok(stats);
    }

    [HttpPost("configuration")]
    public async Task<IActionResult> UpdateConfiguration([FromBody] IdleDetectionConfiguration config)
    {
        await _configManager.UpdateConfigurationAsync(config);
        return Ok();
    }
}
```

#### API Usage Examples
```bash
# Get health status
GET /api/idledetection/health

# Trigger detection
POST /api/idledetection/detect
{
  "processId": 1234,
  "userName": "johndoe",
  "computerName": "WORKSTATION01"
}

# Get statistics
GET /api/idledetection/statistics

# Update configuration
POST /api/idledetection/configuration
{
  "timeBasedDetection": {
    "warningThresholdMinutes": 10
  }
}
```

### 2. WebSocket Integration

#### Integration Overview
The system provides WebSocket connections for:
- Real-time event streaming
- Live status updates
- Interactive monitoring
- Push notifications

#### Implementation Details
```csharp
public class IdleDetectionWebSocketHandler : WebSocketHandler
{
    private readonly IdleDetectionEngine _engine;

    public override async Task OnConnectedAsync(WebSocket socket)
    {
        // Subscribe to engine events
        _engine.IdleDetected += OnIdleDetected;
        _engine.ActivityDetected += OnActivityDetected;

        // Send initial status
        await SendStatusAsync(socket);
    }

    private async void OnIdleDetected(object sender, IdleDetectionEventArgs e)
    {
        var message = new WebSocketMessage
        {
            Type = "IdleDetected",
            Data = new
            {
                e.ProcessId,
                e.UserName,
                e.ComputerName,
                e.Confidence,
                e.DetectionMethod
            }
        };

        await BroadcastAsync(message);
    }
}
```

## Integration Testing

### 1. Unit Testing Integration

#### Test Setup
```csharp
[TestClass]
public class IntegrationTests
{
    private Mock<ILicenseManager> _mockLicenseManager;
    private Mock<ITimerExecutionService> _mockTimerService;
    private IdleDetectionEngine _engine;

    [TestInitialize]
    public void Setup()
    {
        _mockLicenseManager = new Mock<ILicenseManager>();
        _mockTimerService = new Mock<ITimerExecutionService>();

        _engine = new IdleDetectionEngine(
            _mockTimerService.Object,
            new SessionStateManager(),
            new DetectionConsensusEngine(),
            GetTestConfiguration(),
            new Mock<ILogger<IdleDetectionEngine>>().Object
        );
    }

    [TestMethod]
    public async Task DetectIdle_ValidSession_ReturnsDetectionResult()
    {
        // Arrange
        var processId = 1234;
        var userName = "testuser";
        var computerName = "TESTPC";

        _mockLicenseManager.Setup(x => x.GetLicenseInfoAsync(processId, userName))
            .ReturnsAsync(new LicenseInfo { Status = LicenseStatus.Active });

        // Act
        var result = await _engine.DetectIdleAsync(processId, userName, computerName);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(processId, result.ProcessId);
        Assert.AreEqual(userName, result.UserName);
        Assert.AreEqual(computerName, result.ComputerName);
    }
}
```

### 2. Integration Testing Tools

#### Test Scenarios
- **License Integration**: Test license verification and release
- **Timer Integration**: Test scheduled detection cycles
- **Configuration Integration**: Test dynamic configuration updates
- **Event Integration**: Test event propagation and handling
- **Database Integration**: Test data persistence and retrieval

#### Performance Testing
```csharp
[TestMethod]
public async Task PerformanceTest_MultipleSessions()
{
    // Arrange
    var sessions = Enumerable.Range(1, 100).Select(i => new
    {
        ProcessId = i,
        UserName = $"user{i}",
        ComputerName = $"PC{i}"
    }).ToList();

    var stopwatch = Stopwatch.StartNew();

    // Act
    var tasks = sessions.Select(s => _engine.DetectIdleAsync(s.ProcessId, s.UserName, s.ComputerName));
    await Task.WhenAll(tasks);

    // Assert
    stopwatch.Stop();
    Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "Detection took too long");
}
```

## Integration Best Practices

### 1. Error Handling
- Implement comprehensive error handling for all integration points
- Use circuit breakers for external service calls
- Provide graceful degradation when services are unavailable
- Log all integration errors with sufficient context

### 2. Performance Optimization
- Cache frequently accessed data
- Use asynchronous patterns for I/O operations
- Implement connection pooling for database connections
- Monitor and optimize integration performance

### 3. Security
- Validate all external inputs
- Use secure authentication and authorization
- Encrypt sensitive data in transit and at rest
- Implement proper audit trails

### 4. Monitoring and Observability
- Monitor integration health and performance
- Set up appropriate alerts for integration failures
- Log all integration activities
- Provide comprehensive metrics for troubleshooting

### 5. Documentation
- Document all integration points and contracts
- Provide API documentation with examples
- Include integration troubleshooting guides
- Maintain up-to-date dependency information

This integration guide provides the foundation for successfully integrating the Idle Detection system with existing components and external systems. Always refer to the specific version documentation for the most up-to-date integration requirements and best practices.