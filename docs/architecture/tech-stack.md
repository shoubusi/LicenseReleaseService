# Tech Stack

This is the DEFINITIVE technology selection for the entire testing framework project. All development must use these exact versions to ensure compatibility and consistency across the testing infrastructure.

### Technology Stack Table

| Category | Technology | Version | Purpose | Rationale |
|----------|------------|---------|---------|-----------|
| **Frontend Language** | N/A | N/A | No frontend components | Testing framework focuses on backend service testing |
| **Frontend Framework** | N/A | N/A | No frontend components | PRD explicitly states no frontend requirements |
| **UI Component Library** | N/A | N/A | No UI components | Backend testing framework only |
| **State Management** | N/A | N/A | No state management | No frontend components requiring state management |
| **Backend Language** | C# | .NET 9.0 | Core development language | Modern .NET with enhanced performance and features |
| **Backend Framework** | Windows Service + WCF | .NET 9.0 | Service integration | Preserves existing service architecture with modern framework |
| **API Style** | Interface-Based | N/A | Internal API design | Uses existing service interfaces for testing integration |
| **Database** | File-based | N/A | Configuration and test data | Maintains consistency with existing file-based approach |
| **Cache** | Memory Cache | .NET 9.0 | Test data caching | Fast in-memory caching for test performance |
| **File Storage** | File System | Windows NTFS | Test data and configurations | Native Windows file system integration |
| **Authentication** | Windows Auth | N/A | Test environment access | Uses Windows authentication for service testing |
| **Frontend Testing** | N/A | N/A | No frontend testing | No frontend components to test |
| **Backend Testing** | MSTest + xUnit + Moq | 17.8.0 / 2.6.1 / 4.20.69 | Unit and integration testing | Modern testing frameworks with .NET 9.0 compatibility |
| **E2E Testing** | N/A | N/A | No E2E testing requirements | Service-based architecture without user interface |
| **Build Tool** | MSBuild | 17.8+ | Solution building and packaging | Modern .NET SDK build system |
| **Bundler** | N/A | N/A | No bundling requirements | Backend-only architecture |
| **IaC Tool** | N/A | N/A | No infrastructure as code | Testing framework runs on existing infrastructure |
| **CI/CD** | Azure DevOps / GitHub Actions | Latest | Automated testing and deployment | Industry-standard CI/CD with Windows agent support |
| **Monitoring** | Windows Performance Counters | Built-in | Test execution monitoring | Native Windows performance monitoring |
| **Logging** | Microsoft.Extensions.Logging | 8.0.0 | Test logging and reporting | Maintains consistency with existing service logging |
| **CSS Framework** | N/A | N/A | No CSS requirements | No frontend components |
