---
framework: mstest
test_command: dotnet test
test_directory: LicenseReleaseService.Tests
config_file: packages.config
created: 2025-09-29T00:00:00Z
---

# Testing Configuration

## Framework
- Type: MSTest (Microsoft.VisualStudio.TestTools.UnitTesting)
- Version: Legacy .NET Framework 4.8
- Config File: packages.config
- Project: Single project with test files

## Test Structure
- Test Directory: LicenseReleaseService.Tests
- Test Files: 138 files found
- Naming Pattern: *Tests.cs
- Test Classes: [TestClass] attribute
- Test Methods: [TestMethod] attribute

## Commands
- Run All Tests: `dotnet test`
- Run Specific Test: `dotnet test --filter "TestName"`
- Run with Debugging: `dotnet test --verbosity normal`

## Environment
- Required ENV vars: None
- Test Database: None
- Test Servers: None
- Dependencies: NuGet packages via packages.config

## Test Runner Agent Configuration
- Use verbose output for debugging
- Run tests sequentially (no parallel)
- Capture full stack traces
- No mocking - use real implementations
- Wait for each test to complete

## Issues to Address
- Missing dependencies (package resolution warnings)
- Legacy .NET Framework setup vs modern .NET
- Test files not in separate project (legacy structure)

## Next Steps
1. Restore NuGet packages to resolve dependency warnings
2. Consider migrating to modern .NET test project structure
3. Add MSTest.TestAdapter and MSTest.TestFramework packages if missing