# External APIs

The testing framework requires integration with several external systems to enable comprehensive testing of the License Release Service. These integrations are carefully designed to provide realistic testing scenarios while maintaining isolation from production systems.

### SolidWorks Network License Manager API

**Purpose:** Mock integration with SolidWorks Network License Manager (SNL) for testing license query, parsing, and release operations without requiring actual license server access.

- **Documentation:** SolidWorks Network License Manager Administration Guide
- **Base URL(s):** N/A (lmutil.exe command-line tool)
- **Authentication:** N/A (Command-line execution)
- **Rate Limits:** N/A (Controlled by test framework)

**Key Endpoints Used:**
- `lmutil lmstat -c <port>@<server> -a` - License status query - **Purpose:** Query current license usage and availability
- `lmutil lmremove -c <port>@<server> <feature> <user> <host>` - License release - **Purpose:** Release specific user licenses
- `lmutil lmstat -c <port>@<server> -f <feature>` - Feature-specific query - **Purpose:** Query specific license feature details

**Integration Notes:**
The testing framework uses mock implementations of lmutil.exe command-line tool. Mock responses are based on real license server outputs and cover various scenarios including normal operation, license exhaustion, server unavailability, and malformed responses. Test data includes realistic license usage patterns for different SolidWorks versions (2020-2025).

### Windows Event Log API

**Purpose:** Integration with Windows Event Log for testing service logging, error reporting, and audit trail functionality.

- **Documentation:** Microsoft Windows Event Log API Documentation
- **Base URL(s):** N/A (System API)
- **Authentication:** Windows authentication (requires appropriate permissions)
- **Rate Limits:** N/A (System-controlled)

**Key Endpoints Used:**
- `EventLog.WriteEntry()` - Write event log entries - **Purpose:** Test service logging and error reporting
- `EventLog.GetEventLogs()` - Read event logs for validation - **Purpose:** Test logging functionality and audit trail

**Integration Notes:**
The testing framework uses mock EventLog implementations for unit testing and real EventLog access for integration testing. Mock implementations capture all log entries for verification during test execution. Tests validate log levels, message formats, and error handling patterns. Integration tests verify that the service correctly writes to the Windows Event Log and that log messages contain appropriate information.

### Windows Service Control Manager API

**Purpose:** Integration with Windows Service Control Manager for testing service lifecycle, startup/shutdown procedures, and service state management.

- **Documentation:** Windows Service Control Manager API
- **Base URL(s):** N/A (System API)
- **Authentication:** Windows authentication (elevated privileges required)
- **Rate Limits:** N/A (System-controlled)

**Key Endpoints Used:**
- `ServiceController.Start()` - Start service - **Purpose:** Test service startup behavior
- `ServiceController.Stop()` - Stop service - **Purpose:** Test service shutdown behavior
- `ServiceController.Pause()` - Pause service - **Purpose:** Test service pause functionality
- `ServiceController.Continue()` - Resume service - **Purpose:** Test service resume functionality
- `ServiceController.Status` - Get service status - **Purpose:** Monitor service state transitions

**Integration Notes:**
The testing framework uses test-specific service hosts that implement the same interfaces as the Windows Service but can be executed without actual service installation. This enables testing of service lifecycle, configuration hot-reload, and error recovery scenarios without requiring administrative privileges or affecting production services.

### File System API

**Purpose:** Integration with Windows File System for testing configuration hot-reload, file watching, and data persistence functionality.

- **Documentation:** .NET Framework File System API Documentation
- **Base URL(s):** Local file system paths
- **Authentication:** Windows file system permissions
- **Rate Limits:** File system I/O limitations

**Key Endpoints Used:**
- `File.ReadAllText()` / `File.WriteAllText()` - Configuration file access - **Purpose:** Test configuration management
- `FileSystemWatcher` - Monitor file changes - **Purpose:** Test configuration hot-reload functionality
- `Directory.CreateDirectory()` - Directory operations - **Purpose:** Test file and directory management

**Integration Notes:**
The framework provides comprehensive file system mocking for unit testing and real file system access for integration testing. Mock implementations simulate file changes, access errors, permission issues, and other file system scenarios to ensure robust service behavior.

### Windows Registry API

**Purpose:** Integration with Windows Registry for testing service configuration storage, registration, and system integration.

- **Documentation:** Windows Registry API Documentation
- **Base URL(s):** N/A (System API)
- **Authentication:** Windows authentication (requires appropriate permissions)
- **Rate Limits:** N/A (System-controlled)

**Key Endpoints Used:**
- `Registry.GetValue()` - Read registry values - **Purpose:** Test service configuration reading
- `Registry.SetValue()` - Write registry values - **Purpose:** Test service configuration persistence
- `RegistryKey.OpenSubKey()` - Access registry keys - **Purpose:** Test registry navigation and access

**Integration Notes:**
The testing framework uses mock registry implementations to avoid modifying production registry settings. Mock registry captures all read/write operations for verification during test execution. Tests validate configuration persistence, service registration, and error handling for registry access failures.

### Process Execution API

**Purpose:** Integration with Windows Process execution for testing external process management, timeout handling, and error recovery.

- **Documentation:** .NET Process Class Documentation
- **Base URL(s):** N/A (System API)
- **Authentication:** Windows authentication (process permissions)
- **Rate Limits:** N/A (System-controlled)

**Key Endpoints Used:**
- `Process.Start()` - Start external processes - **Purpose:** Test lmutil.exe execution and process management
- `Process.WaitForExit()` - Wait for process completion - **Purpose:** Test timeout handling and process lifecycle
- `Process.StandardOutput` - Read process output - **Purpose:** Test command output parsing and error handling
- `Process.Kill()` - Terminate processes - **Purpose:** Test process cleanup and resource management

**Integration Notes:**
The framework uses configurable process execution that can use real processes in integration tests and mock processes in unit tests. Mock process executor enables testing of timeout handling, error recovery, and output parsing without actual process execution. Test scenarios cover process failures, timeouts, and malformed output.
