using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    public class LmutilOutputParserTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<LmutilOutputParser>> _mockLogger;
        private readonly LmutilOutputParser _parser;

        public LmutilOutputParserTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<LmutilOutputParser>>();
            _parser = new LmutilOutputParser(_mockLogger.Object);
        }

        [Fact]
        public void ParseLmstatOutput_WithValidOutput_ReturnsCorrectStatus()
        {
            // Arrange
            var lmstatOutput = @"
License server status: 27000@server1
License server UP (MASTER)

Users of maya:
  Total of 10 licenses issued; Total of 3 licenses in use

Users of autocad:
  Total of 5 licenses issued; Total of 5 licenses in use

john workstation1 (v1.0) (john@workstation1), start Mon 9/25 12:30
jane workstation2 (jane@workstation2), start Mon 9/25 13:45
bob workstation3 (bob@workstation3), start Mon 9/25 14:20
";

            // Act
            var result = _parser.ParseLmstatOutput(lmstatOutput, "27000@server1");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsServerUp);
            Assert.Equal("27000@server1", result.ServerAddress);
            Assert.Equal(2, result.Features.Count);

            Assert.True(result.Features.ContainsKey("maya"));
            Assert.True(result.Features.ContainsKey("autocad"));

            var mayaFeature = result.Features["maya"];
            Assert.Equal(10, mayaFeature.TotalLicenses);
            Assert.Equal(3, mayaFeature.LicensesInUse);
            Assert.Equal(3, mayaFeature.Users.Count);

            var autocadFeature = result.Features["autocad"];
            Assert.Equal(5, autocadFeature.TotalLicenses);
            Assert.Equal(5, autocadFeature.LicensesInUse);
        }

        [Fact]
        public void ParseLmstatOutput_WithServerDown_ReturnsCorrectStatus()
        {
            // Arrange
            var lmstatOutput = @"
License server status: 27000@server1
License server DOWN

Users of maya:
  Total of 10 licenses issued; Total of 0 licenses in use
";

            // Act
            var result = _parser.ParseLmstatOutput(lmstatOutput, "27000@server1");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsServerUp);
            Assert.Equal("27000@server1", result.ServerAddress);
        }

        [Fact]
        public void ParseLmstatOutput_WithError_ThrowsOutputParsingException()
        {
            // Arrange
            var errorOutput = @"
License server status: 27000@server1
ERROR: Cannot connect to license server
Users of maya:
";

            // Act & Assert
            var exception = Assert.Throws<OutputParsingException>(() =>
                _parser.ParseLmstatOutput(errorOutput, "27000@server1"));

            Assert.Contains("Cannot connect to license server", exception.Message);
            Assert.Equal("lmstat", exception.ParsingOperation);
        }

        [Fact]
        public void ParseLmstatOutput_WithEmptyOutput_ThrowsOutputParsingException()
        {
            // Arrange
            var emptyOutput = "";

            // Act & Assert
            var exception = Assert.Throws<OutputParsingException>(() =>
                _parser.ParseLmstatOutput(emptyOutput, "27000@server1"));

            Assert.Equal("Empty lmstat output", exception.Message);
            Assert.Equal("lmstat", exception.ParsingOperation);
        }

        [Fact]
        public void ParseLmremoveOutput_WithSuccess_ReturnsSuccessResult()
        {
            // Arrange
            var lmremoveOutput = "Removed user john from feature maya";

            // Act
            var result = _parser.ParseLmremoveOutput(lmremoveOutput, "maya", "john");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("maya", result.ReleasedFeature);
            Assert.Equal("john", result.ReleasedUser);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void ParseLmremoveOutput_WithError_ReturnsFailureResult()
        {
            // Arrange
            var errorOutput = "ERROR: User john not found for feature maya";

            // Act
            var result = _parser.ParseLmremoveOutput(errorOutput, "maya", "john");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("maya", result.ReleasedFeature);
            Assert.Equal("john", result.ReleasedUser);
            Assert.Equal("User john not found for feature maya", result.ErrorMessage);
        }

        [Fact]
        public void ParseLmremoveOutput_WithAmbiguousOutput_ReturnsExpectedResult()
        {
            // Arrange
            var ambiguousOutput = "License removal operation completed";

            // Act
            var result = _parser.ParseLmremoveOutput(ambiguousOutput, "maya", "john");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success); // No error keywords found
        }

        [Fact]
        public async Task ParseOutputIncrementallyAsync_WithValidLines_ReturnsCorrectResult()
        {
            // Arrange
            var lines = new[]
            {
                "License server status: 27000@server1",
                "License server UP",
                "",
                "Users of maya:",
                "  Total of 10 licenses issued; Total of 2 licenses in use"
            };

            // Act
            var result = await _parser.ParseOutputIncrementallyAsync(lines, "lmstat");

            // Assert
            Assert.NotNull(result);
            var status = Assert.IsType<LicenseServerStatus>(result);
            Assert.True(status.IsServerUp);
            Assert.True(status.Features.ContainsKey("maya"));
        }

        [Fact]
        public async Task ParseOutputIncrementallyAsync_WithCancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            var lines = Enumerable.Repeat("Sample line", 10000).ToArray();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _parser.ParseOutputIncrementallyAsync(lines, "lmstat", cts.Token));
        }

        [Fact]
        public void ValidateParsedData_WithValidData_ReturnsTrue()
        {
            // Arrange
            var status = new LicenseServerStatus
            {
                ServerAddress = "27000@server1",
                IsServerUp = true,
                Features = new Dictionary<string, LicenseFeatureInfo>
                {
                    ["maya"] = new LicenseFeatureInfo
                    {
                        FeatureName = "maya",
                        TotalLicenses = 10,
                        LicensesInUse = 5,
                        Users = new List<LicenseUserInfo>
                        {
                            new LicenseUserInfo { Username = "john", Hostname = "workstation1" }
                        }
                    }
                }
            };

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateParsedData_WithInvalidData_ReturnsFalse()
        {
            // Arrange
            var status = new LicenseServerStatus
            {
                ServerAddress = "27000@server1",
                IsServerUp = true,
                Features = new Dictionary<string, LicenseFeatureInfo>
                {
                    ["maya"] = new LicenseFeatureInfo
                    {
                        FeatureName = "maya",
                        TotalLicenses = 10,
                        LicensesInUse = 15, // More than total
                        Users = new List<LicenseUserInfo>
                        {
                            new LicenseUserInfo { Username = "", Hostname = "workstation1" } // Empty username
                        }
                    }
                }
            };

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateParsedData_WithNullStatus_ReturnsFalse()
        {
            // Act
            var result = _parser.ValidateParsedData(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ParseLmstatOutput_WithComplexUserInfo_ParsesCorrectly()
        {
            // Arrange
            var complexOutput = @"
License server status: 27000@server1
License server UP

Users of maya:
  Total of 10 licenses issued; Total of 2 licenses in use

john workstation1 (v1.0) (john@workstation.com), start Mon 9/25 12:30 (licensing)
jane workstation2 (jane@company.com) (v2.1), start Mon 9/25 13:45
";

            // Act
            var result = _parser.ParseLmstatOutput(complexOutput, "27000@server1");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Features.ContainsKey("maya"));

            var mayaFeature = result.Features["maya"];
            Assert.Equal(2, mayaFeature.Users.Count);

            var user1 = mayaFeature.Users[0];
            Assert.Equal("john", user1.Username);
            Assert.Equal("workstation1", user1.Hostname);
            Assert.Equal("v1.0", user1.Version);
            Assert.Equal("john@workstation.com", user1.DisplayName);

            var user2 = mayaFeature.Users[1];
            Assert.Equal("jane", user2.Username);
            Assert.Equal("workstation2", user2.Hostname);
            Assert.Equal("v2.1", user2.Version);
            Assert.Equal("jane@company.com", user2.DisplayName);
        }

        [Fact]
        public void ParseLmstatOutput_WithNoUsers_ReturnsEmptyUserList()
        {
            // Arrange
            var noUsersOutput = @"
License server status: 27000@server1
License server UP

Users of maya:
  Total of 10 licenses issued; Total of 0 licenses in use
";

            // Act
            var result = _parser.ParseLmstatOutput(noUsersOutput, "27000@server1");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Features.ContainsKey("maya"));

            var mayaFeature = result.Features["maya"];
            Assert.Equal(10, mayaFeature.TotalLicenses);
            Assert.Equal(0, mayaFeature.LicensesInUse);
            Assert.Empty(mayaFeature.Users);
        }

        [Fact]
        public void ParseLmstatOutput_WithMultipleServers_HandlesCorrectly()
        {
            // Arrange
            var multiServerOutput = @"
License server status: 27000@server1
License server UP

License server status: 27000@server2
License server DOWN

Users of maya:
  Total of 10 licenses issued; Total of 2 licenses in use
";

            // Act
            var result = _parser.ParseLmstatOutput(multiServerOutput, "27000@server1");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsServerUp);
            Assert.Equal("27000@server1", result.ServerAddress);
        }

        [Fact]
        public void ParseOutputIncrementallyAsync_WithTooManyLines_TruncatesOutput()
        {
            // Arrange
            var lines = Enumerable.Repeat("Sample line", 15000).ToArray();

            // Act
            var result = _parser.ParseOutputIncrementallyAsync(lines, "lmstat").Result;

            // Assert
            Assert.NotNull(result);
            var status = Assert.IsType<LicenseServerStatus>(result);
            // Should not throw an exception despite large input
        }

        [Fact]
        public void ParseLmremoveOutput_WithEmptyOutput_ThrowsOutputParsingException()
        {
            // Arrange
            var emptyOutput = "";

            // Act & Assert
            var exception = Assert.Throws<OutputParsingException>(() =>
                _parser.ParseLmremoveOutput(emptyOutput, "maya", "john"));

            Assert.Equal("Empty lmremove output", exception.Message);
            Assert.Equal("lmremove", exception.ParsingOperation);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}