using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using LicenseReleaseService.LicenseManagement.Parsing;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.Tests.LicenseManagement.Parsing
{
    /// <summary>
    /// Comprehensive unit tests for LmstatOutputParser
    /// </summary>
    public class LmstatOutputParserTests
    {
        private readonly LmstatOutputParser _parser;

        public LmstatOutputParserTests()
        {
            _parser = new LmstatOutputParser();
        }

        #region Sample lmstat Output Data

        private const string StandardLmstatOutput = @"License server status: 27000@license-server
    License file(s) on license-server:
    SOLIDWORKS: license server UP (MASTER) v11.16.2

Vendor daemon status (on license-server):
    solidworks: UP v11.16.2

Feature usage info:
Users of solidworks:  (Total of 100 licenses issued;  Total of 25 licenses in use)

""johndoe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45

Users of toolbox:  (Total of 50 licenses issued;  Total of 10 licenses in use)

""janedoe"" workstation2 (v2023.1) (license-server/27000 456), start Mon 9/25 22:30
""bobsmith"" workstation3 (v2023.1) (license-server/27000 789), start Mon 9/25 21:15";

        private const string VerboseLmstatOutput = @"License server status: 27000@license-server
    License file(s) on license-server:
    SOLIDWORKS: license server UP (MASTER) v11.16.2
    Server start: Mon 9/25 08:00
    Server uptime: 15 hours 30 minutes
    Platform: Windows x64

Vendor daemon status (on license-server):
    solidworks: UP v11.16.2
    lmgrd: UP v11.16.2

Feature usage info:
Users of solidworks:  (Total of 100 licenses issued;  Total of 30 licenses in use)

""johndoe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45
""janedoe"" workstation2 (v2023.1) (license-server/27000 456), start Mon 9/25 22:30 (idle 15 minutes)
""bobsmith"" workstation3 (v2023.1) (license-server/27000 789), start Mon 9/25 21:15

Users of toolbox:  (Total of 50 licenses issued;  Total of 15 licenses in use)
    expires: 12/31/2024

""alice"" workstation4 (v2023.1) (license-server/27000 101), start Mon 9/25 20:45
""charlie"" workstation5 (v2023.1) (license-server/27000 202), start Mon 9/25 19:30 (borrowed)";

        private const string ErrorLmstatOutput = @"ERROR: Cannot connect to license server
Connection refused to 27000@license-server
Please check if the license server is running.";

        private const string EmptyLmstatOutput = "";

        private const string MalformedLmstatOutput = @"License server status: 27000@license-server
Some malformed output here
Invalid data format
Total of invalid licenses issued;
""user"" host (v1.0) invalid format";

        private const string BorrowedLicensesOutput = @"License server status: 27000@license-server
    License file(s) on license-server:
    SOLIDWORKS: license server UP (MASTER) v11.16.2

Feature usage info:
Users of solidworks:  (Total of 100 licenses issued;  Total of 20 licenses in use)

""johndoe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45 (borrowed)
""janedoe"" workstation2 (v2023.1) (license-server/27000 456), start Mon 9/25 22:30 (detached)
""bobsmith"" workstation3 (v2023.1) (license-server/27000 789), start Mon 9/25 21:15";

        #endregion

        #region Basic Parsing Tests

        [Fact]
        public void ParseLmstatOutput_WithStandardOutput_ReturnsValidStatus()
        {
            // Arrange
            var serverAddress = "27000@license-server";

            // Act
            var result = _parser.ParseLmstatOutput(StandardLmstatOutput, serverAddress);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(serverAddress, result.Server);
            Assert.True(result.IsServerUp);
            Assert.True(result.IsHealthy);
            Assert.Equal(2, result.FeatureDetails.Count);

            // Check SOLIDWORKS feature
            Assert.True(result.FeatureDetails.ContainsKey("solidworks"));
            var solidworksFeature = result.FeatureDetails["solidworks"];
            Assert.Equal(100, solidworksFeature.TotalLicenses);
            Assert.Equal(25, solidworksFeature.UsedLicenses);
            Assert.Equal(75, solidworksFeature.AvailableLicenses);
            Assert.Equal(1, solidworksFeature.ActiveUsers);

            // Check toolbox feature
            Assert.True(result.FeatureDetails.ContainsKey("toolbox"));
            var toolboxFeature = result.FeatureDetails["toolbox"];
            Assert.Equal(50, toolboxFeature.TotalLicenses);
            Assert.Equal(10, toolboxFeature.UsedLicenses);
            Assert.Equal(40, toolboxFeature.AvailableLicenses);
            Assert.Equal(2, toolboxFeature.ActiveUsers);

            // Check aggregate statistics
            Assert.Equal(150, result.TotalLicenses);
            Assert.Equal(35, result.LicensesInUse);
            Assert.Equal(115, result.AvailableLicenses);
            Assert.Equal(3, result.TotalUsers);
        }

        [Fact]
        public void ParseLmstatOutput_WithEmptyOutput_ThrowsException()
        {
            // Arrange
            var serverAddress = "27000@license-server";

            // Act & Assert
            var exception = Assert.Throws<OutputParsingException>(() =>
                _parser.ParseLmstatOutput(EmptyLmstatOutput, serverAddress));

            Assert.Contains("Empty lmstat output", exception.Message);
            Assert.Equal("lmstat", exception.ParsingOperation);
        }

        [Fact]
        public void ParseLmstatOutput_WithErrorOutput_ThrowsException()
        {
            // Arrange
            var serverAddress = "27000@license-server";

            // Act & Assert
            var exception = Assert.Throws<OutputParsingException>(() =>
                _parser.ParseLmstatOutput(ErrorLmstatOutput, serverAddress));

            Assert.Contains("lmstat command failed", exception.Message);
            Assert.Contains("Cannot connect to license server", exception.RawOutput);
        }

        [Fact]
        public void ParseLmstatOutput_WithMalformedOutput_HandlesGracefully()
        {
            // Arrange
            var serverAddress = "27000@license-server";

            // Act & Assert
            // This should not throw an exception but should handle malformed data gracefully
            var result = _parser.ParseLmstatOutput(MalformedLmstatOutput, serverAddress);

            Assert.NotNull(result);
            Assert.Equal(serverAddress, result.Server);
            // The parser should still extract what it can from malformed output
        }

        #endregion

        #region Verbose Output Parsing Tests

        [Fact]
        public void ParseLmstatVerboseOutput_WithVerboseData_ReturnsEnhancedStatus()
        {
            // Arrange
            var serverAddress = "27000@license-server";

            // Act
            var result = _parser.ParseLmstatVerboseOutput(VerboseLmstatOutput, serverAddress);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsServerUp);
            Assert.True(result.IsHealthy);

            // Check server information
            Assert.Contains("Windows x64", result.Platform);
            Assert.Equal("64-bit", result.Architecture);
            Assert.Equal("SOLIDWORKS", result.Vendor);
            Assert.NotNull(result.ServerStartTime);
            Assert.True(result.UptimeSeconds > 0);

            // Check features
            Assert.Equal(2, result.FeatureDetails.Count);

            // Check SOLIDWORKS feature with idle users
            var solidworksFeature = result.FeatureDetails["solidworks"];
            Assert.Equal(100, solidworksFeature.TotalLicenses);
            Assert.Equal(30, solidworksFeature.UsedLicenses);
            Assert.Equal(2, solidworksFeature.ActiveUsers);
            Assert.Equal(1, solidworksFeature.IdleUsers); // jane with idle time

            // Check toolbox feature with expiration
            var toolboxFeature = result.FeatureDetails["toolbox"];
            Assert.Equal(50, toolboxFeature.TotalLicenses);
            Assert.Equal(15, toolboxFeature.UsedLicenses);
            Assert.NotNull(toolboxFeature.ExpirationDate);
            Assert.Equal(1, toolboxFeature.BorrowedUsers); // charlie with borrowed license

            // Check server messages
            Assert.NotEmpty(result.ServerMessages);
        }

        #endregion

        #region Feature-Specific Parsing Tests

        [Fact]
        public void ParseFeatureOutput_WithSpecificFeature_ReturnsFeatureOnly()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var featureName = "solidworks";
            var featureOutput = @"Users of solidworks:  (Total of 100 licenses issued;  Total of 25 licenses in use)

""johndoe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45
""janedoe"" workstation2 (v2023.1) (license-server/27000 456), start Mon 9/25 22:30";

            // Act
            var result = _parser.ParseFeatureOutput(featureOutput, serverAddress, featureName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(featureName, result.Name);
            Assert.Equal(100, result.TotalLicenses);
            Assert.Equal(25, result.UsedLicenses);
            Assert.Equal(75, result.AvailableLicenses);
            Assert.Equal(2, result.ActiveUsers);
            Assert.Equal(0, result.IdleUsers);
            Assert.Equal(0, result.BorrowedUsers);
        }

        [Fact]
        public void ParseFeatureOutput_WithNonexistentFeature_ThrowsException()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var featureName = "nonexistent";
            var featureOutput = @"Users of solidworks:  (Total of 100 licenses issued;  Total of 25 licenses in use)

""johndoe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45";

            // Act & Assert
            var exception = Assert.Throws<OutputParsingException>(() =>
                _parser.ParseFeatureOutput(featureOutput, serverAddress, featureName));

            Assert.Contains("Feature 'nonexistent' not found", exception.Message);
        }

        [Fact]
        public void ParseFeatureOutput_WithEmptyFeatureName_ThrowsException()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var featureName = "";
            var featureOutput = @"Users of solidworks:  (Total of 100 licenses issued;  Total of 25 licenses in use)";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                _parser.ParseFeatureOutput(featureOutput, serverAddress, featureName));

            Assert.Contains("Feature name cannot be null or whitespace", exception.Message);
        }

        #endregion

        #region Borrowed Licenses Tests

        [Fact]
        public void ParseLmstatOutput_WithBorrowedLicenses_IdentifiesBorrowedStatus()
        {
            // Arrange
            var serverAddress = "27000@license-server";

            // Act
            var result = _parser.ParseLmstatOutput(BorrowedLicensesOutput, serverAddress);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.FeatureDetails.ContainsKey("solidworks"));

            var solidworksFeature = result.FeatureDetails["solidworks"];
            Assert.Equal(100, solidworksFeature.TotalLicenses);
            Assert.Equal(20, solidworksFeature.UsedLicenses);
            Assert.Equal(1, solidworksFeature.ActiveUsers);      // bobsmith
            Assert.Equal(2, solidworksFeature.BorrowedUsers);   // johndoe and janedoe

            // Check borrowed user details
            var borrowedUsers = solidworksFeature.BorrowedUsersList;
            Assert.Equal(2, borrowedUsers.Count);

            var johndoe = borrowedUsers.FirstOrDefault(u => u.UserHost == "johndoe");
            Assert.NotNull(johndoe);
            Assert.Equal(LicenseStatus.Borrowed, johndoe.Status);
            Assert.True(johndoe.IsBorrowed);

            var janedoe = borrowedUsers.FirstOrDefault(u => u.UserHost == "janedoe");
            Assert.NotNull(janedoe);
            Assert.Equal(LicenseStatus.Borrowed, janedoe.Status);
            Assert.True(janedoe.IsBorrowed);
        }

        #endregion

        #region Incremental Parsing Tests

        [Fact]
        public void ParseOutputIncrementally_WithMultipleLines_ReturnsValidStatus()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var outputLines = StandardLmstatOutput.Split('\n');

            // Act
            var result = _parser.ParseOutputIncrementally(outputLines, serverAddress);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(serverAddress, result.Server);
            Assert.Equal(2, result.FeatureDetails.Count);
            Assert.Equal(150, result.TotalLicenses);
            Assert.Equal(35, result.LicensesInUse);
        }

        [Fact]
        public void ParseOutputIncrementally_WithVerboseOutput_ReturnsEnhancedStatus()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var outputLines = VerboseLmstatOutput.Split('\n');

            // Act
            var result = _parser.ParseOutputIncrementally(outputLines, serverAddress, isVerbose: true);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsServerUp);
            Assert.Contains("Windows x64", result.Platform);
            Assert.Equal(2, result.FeatureDetails.Count);
        }

        #endregion

        #region User Information Parsing Tests

        [Fact]
        public void ParseLmstatOutput_WithIdleUsers_IdentifiesIdleStatus()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var outputWithIdleUsers = @"License server status: 27000@license-server
Feature usage info:
Users of solidworks:  (Total of 100 licenses issued;  Total of 3 licenses in use)

""johndoe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45
""janedoe"" workstation2 (v2023.1) (license-server/27000 456), start Mon 9/25 22:30 (idle 15 minutes)
""bobsmith"" workstation3 (v2023.1) (license-server/27000 789), start Mon 9/25 21:15 (idle 1 hour)";

            // Act
            var result = _parser.ParseLmstatOutput(outputWithIdleUsers, serverAddress);

            // Assert
            Assert.NotNull(result);
            var solidworksFeature = result.FeatureDetails["solidworks"];

            Assert.Equal(3, solidworksFeature.UsedLicenses);
            Assert.Equal(1, solidworksFeature.ActiveUsers);   // johndoe
            Assert.Equal(2, solidworksFeature.IdleUsers);     // janedoe and bobsmith

            // Check idle user details
            var idleUsers = solidworksFeature.IdleUsersList;
            Assert.Equal(2, idleUsers.Count);

            var janedoe = idleUsers.FirstOrDefault(u => u.UserHost == "janedoe");
            Assert.NotNull(janedoe);
            Assert.Equal(LicenseStatus.Idle, janedoe.Status);
            Assert.True(janedoe.IsIdle);
            Assert.Contains("15 minutes", janedoe.IdleReason);

            var bobsmith = idleUsers.FirstOrDefault(u => u.UserHost == "bobsmith");
            Assert.NotNull(bobsmith);
            Assert.Equal(LicenseStatus.Idle, bobsmith.Status);
            Assert.True(bobsmith.IsIdle);
            Assert.Contains("1 hour", bobsmith.IdleReason);
        }

        [Fact]
        public void ParseLmstatOutput_WithVersionInformation_ParsesVersions()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var outputWithVersions = @"License server status: 27000@license-server
Feature usage info:
Users of solidworks:  (Total of 100 licenses issued;  Total of 2 licenses in use)

""johndoe"" workstation1 (v2023.1.5) (license-server/27000 123), start Mon 9/25 23:45
""janedoe"" workstation2 (v2022.3.1) (license-server/27000 456), start Mon 9/25 22:30";

            // Act
            var result = _parser.ParseLmstatOutput(outputWithVersions, serverAddress);

            // Assert
            Assert.NotNull(result);
            var solidworksFeature = result.FeatureDetails["solidworks"];

            var users = solidworksFeature.GetAllUsers();
            var johndoe = users.FirstOrDefault(u => u.UserHost == "johndoe");
            var janedoe = users.FirstOrDefault(u => u.UserHost == "janedoe");

            Assert.NotNull(johndoe);
            Assert.Equal("v2023.1.5", johndoe.LicenseVersion);

            Assert.NotNull(janedoe);
            Assert.Equal("v2022.3.1", janedoe.LicenseVersion);
        }

        [Fact]
        public void ParseLmstatOutput_WithCheckoutTimes_ParsesTimestamps()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var outputWithTimestamps = @"License server status: 27000@license-server
Feature usage info:
Users of solidworks:  (Total of 100 licenses issued;  Total of 2 licenses in use)

""johndoe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45
""janedoe"" workstation2 (v2023.1) (license-server/27000 456), start 9/25 14:30";

            // Act
            var result = _parser.ParseLmstatOutput(outputWithTimestamps, serverAddress);

            // Assert
            Assert.NotNull(result);
            var solidworksFeature = result.FeatureDetails["solidworks"];

            var users = solidworksFeature.GetAllUsers();
            var johndoe = users.FirstOrDefault(u => u.UserHost == "johndoe");
            var janedoe = users.FirstOrDefault(u => u.UserHost == "janedoe");

            Assert.NotNull(johndoe);
            Assert.True(johndoe.BorrowTime < DateTime.Now);
            Assert.True(johndoe.UsageDuration > TimeSpan.Zero);

            Assert.NotNull(janedoe);
            Assert.True(janedoe.BorrowTime < DateTime.Now);
            Assert.True(janedoe.UsageDuration > TimeSpan.Zero);
        }

        #endregion

        #region Server Information Extraction Tests

        [Fact]
        public void ExtractServerInformation_WithCompleteOutput_ReturnsServerDetails()
        {
            // Arrange
            var completeOutput = @"License server status: 27000@license-server
    License file(s) on license-server:
    SOLIDWORKS: license server UP (MASTER) v11.16.2
    Server start: Mon 9/25 08:00
    Server uptime: 15 hours 30 minutes
    Platform: Windows x64

Vendor daemon status (on license-server):
    solidworks: UP v11.16.2";

            // Act
            var serverInfo = _parser.ExtractServerInformation(completeOutput);

            // Assert
            Assert.NotNull(serverInfo);
            Assert.Equal("27000@license-server", serverInfo["Address"]);
            Assert.Equal("SOLIDWORKS", serverInfo["Vendor"]);
            Assert.Equal("UP", serverInfo["Status"]);
            Assert.Equal("Mon 9/25 08:00", serverInfo["StartTime"]);
            Assert.Equal("Windows x64", serverInfo["Platform"]);
            Assert.Equal("15 hours", serverInfo["Uptime"]);
        }

        [Fact]
        public void ExtractServerInformation_WithMinimalOutput_ReturnsBasicInfo()
        {
            // Arrange
            var minimalOutput = @"License server status: 27000@license-server
    License file(s) on license-server:
    SOLIDWORKS: license server UP (MASTER) v11.16.2";

            // Act
            var serverInfo = _parser.ExtractServerInformation(minimalOutput);

            // Assert
            Assert.NotNull(serverInfo);
            Assert.Equal("27000@license-server", serverInfo["Address"]);
            Assert.Equal("SOLIDWORKS", serverInfo["Vendor"]);
            Assert.Equal("UP", serverInfo["Status"]);
            Assert.False(serverInfo.ContainsKey("StartTime"));
        }

        [Fact]
        public void ExtractServerInformation_WithEmptyOutput_ReturnsEmptyDictionary()
        {
            // Arrange
            var emptyOutput = "";

            // Act
            var serverInfo = _parser.ExtractServerInformation(emptyOutput);

            // Assert
            Assert.NotNull(serverInfo);
            Assert.Empty(serverInfo);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void ValidateParsedData_WithValidData_ReturnsTrue()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var result = _parser.ParseLmstatOutput(StandardLmstatOutput, serverAddress);

            // Act
            var isValid = _parser.ValidateParsedData(result);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void ValidateParsedData_WithNullStatus_ReturnsFalse()
        {
            // Arrange
            LicenseServerStatus nullStatus = null;

            // Act
            var isValid = _parser.ValidateParsedData(nullStatus);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateParsedData_WithInvalidData_DetectsIssues()
        {
            // Arrange
            var invalidStatus = new LicenseServerStatus
            {
                Server = "",
                FeatureDetails = new Dictionary<string, LicenseFeature>
                {
                    ["test"] = new LicenseFeature
                    {
                        Name = "test",
                        TotalLicenses = -1,
                        UsedLicenses = 100,
                        AvailableLicenses = 0
                    }
                }
            };

            // Act
            var isValid = _parser.ValidateParsedData(invalidStatus);

            // Assert
            Assert.False(isValid); // Should detect negative total licenses
        }

        #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public void ParseLmstatOutput_WithNullServerAddress_UsesExtractedAddress()
        {
            // Arrange
            var nullServerAddress = "";

            // Act
            var result = _parser.ParseLmstatOutput(StandardLmstatOutput, nullServerAddress);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Server); // Should extract from output
            Assert.Equal("27000@license-server", result.Server);
        }

        [Fact]
        public void ParseLmstatOutput_WithVeryLargeOutput_HandlesGracefully()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var largeOutput = GenerateLargeLmstatOutput(20000); // 20k lines

            // Act
            var result = _parser.ParseLmstatOutput(largeOutput, serverAddress);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.FeatureDetails.Count > 0);
        }

        [Fact]
        public void ParseOutputIncrementally_WithVeryLargeOutput_TruncatesAppropriately()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var largeOutputLines = GenerateLargeLmstatOutput(15000).Split('\n');

            // Act
            var result = _parser.ParseOutputIncrementally(largeOutputLines, serverAddress);

            // Assert
            Assert.NotNull(result);
            // Should not fail or throw exception due to size
        }

        [Fact]
        public void ParseLmstatOutput_WithSpecialCharactersInUsernames_HandlesCorrectly()
        {
            // Arrange
            var serverAddress = "27000@license-server";
            var outputWithSpecialChars = @"License server status: 27000@license-server
Feature usage info:
Users of solidworks:  (Total of 100 licenses issued;  Total of 3 licenses in use)

""john.doe"" workstation1 (v2023.1) (license-server/27000 123), start Mon 9/25 23:45
""jane-doe"" workstation2 (v2023.1) (license-server/27000 456), start Mon 9/25 22:30
""bob_smith"" workstation3 (v2023.1) (license-server/27000 789), start Mon 9/25 21:15";

            // Act
            var result = _parser.ParseLmstatOutput(outputWithSpecialChars, serverAddress);

            // Assert
            Assert.NotNull(result);
            var solidworksFeature = result.FeatureDetails["solidworks"];
            Assert.Equal(3, solidworksFeature.ActiveUsers);

            var users = solidworksFeature.GetAllUsers();
            Assert.Contains(users, u => u.UserHost == "john.doe");
            Assert.Contains(users, u => u.UserHost == "jane-doe");
            Assert.Contains(users, u => u.UserHost == "bob_smith");
        }

        #endregion

        #region Helper Methods

        private string GenerateLargeLmstatOutput(int lineCount)
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine("License server status: 27000@license-server");
            output.AppendLine("Feature usage info:");
            output.AppendLine("Users of solidworks:  (Total of 100 licenses issued;  Total of 50 licenses in use)");

            for (int i = 1; i <= lineCount - 4; i++) // -4 for header lines
            {
                output.AppendLine($"\"user{i}\" workstation{i % 100} (v2023.1) (license-server/27000 {i % 1000}), start Mon 9/25 {i % 24}:{i % 60}");
            }

            return output.ToString();
        }

        #endregion
    }
}