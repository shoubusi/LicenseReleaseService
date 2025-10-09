---
framework: mstest
test_command: dotnet test LicenseReleaseService.Tests/
test_directory: LicenseReleaseService.Tests
config_file: LicenseReleaseService.Tests.csproj
created: 2025-10-09T00:00:00Z
last_updated: 2025-10-09T00:00:00Z
---

# Testing Environment Configuration

## Framework Detection
- **Primary Framework**: MSTest
- **Secondary Framework**: xUnit (detected but not actively used)
- **Test Project**: LicenseReleaseService.Tests.csproj
- **Target Framework**: net9.0-windows

## Dependencies Status
✅ Microsoft.NET.Test.Sdk (17.8.0)
✅ MSTest.TestAdapter (3.1.1)
✅ MSTest.TestFramework (3.1.1)
✅ xunit (2.6.1)
✅ xunit.runner.visualstudio (2.5.3)
✅ Moq (4.20.69)
✅ Microsoft.Extensions.* packages (8.0.0)

## Test Infrastructure
- **Working Tests**: 5 tests in SimpleTimerTests.cs (all passing)
- **Test Files**: 135 total (most in Backup directory)
- **Test Discovery**: MSTest adapter properly configured
- **Test Runner**: Visual Studio Test Platform

## Core Components Tested
- TimerStatus enum functionality
- TimerErrorEventArgs event handling
- TimerErrorSeverity level validation
- TimerErrorCategory classification
- TimerRecoveryAction execution

## Test Execution Commands
```bash
# Run all tests
dotnet test LicenseReleaseService.Tests/

# Run with verbosity
dotnet test LicenseReleaseService.Tests/ --verbosity normal

# Run specific test file
dotnet test LicenseReleaseService.Tests/ --filter "SimpleTimerTests"
```

## Configuration Notes
- ImplicitUsings disabled (matches main project)
- Nullable enabled
- No compilation errors in test project
- All test references properly resolved
- Main project builds successfully with zero compilation errors

## Test Runner Agent Configuration
- Use verbose output for debugging
- Run tests sequentially (no parallel)
- Capture full stack traces
- No mocking - use real implementations (per project rules)
- Wait for each test to complete before proceeding
- Tests designed to be verbose for debugging purposes