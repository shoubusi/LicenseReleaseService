using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    public class ParsingPatternsTests : IDisposable
    {
        private readonly ITestOutputHelper _output;

        public ParsingPatternsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ServerStatusPattern_MatchesValidStatus()
        {
            // Arrange
            var validOutputs = new[]
            {
                "license server UP",
                "license server DOWN",
                "license server DOWN (MASTER)",
                "License Server UP",
                "LICENSE SERVER DOWN"
            };

            // Act & Assert
            foreach (var output in validOutputs)
            {
                var match = ParsingPatterns.ServerStatusPattern.Match(output);
                Assert.True(match.Success, $"Pattern should match: {output}");
                Assert.Contains(match.Groups[1].Value.ToUpper(), new[] { "UP", "DOWN" });
            }
        }

        [Fact]
        public void ServerStatusPattern_DoesNotMatchInvalidStatus()
        {
            // Arrange
            var invalidOutputs = new[]
            {
                "server status UP",
                "license up",
                "server DOWN",
                "random text",
                ""
            };

            // Act & Assert
            foreach (var output in invalidOutputs)
            {
                var match = ParsingPatterns.ServerStatusPattern.Match(output);
                Assert.False(match.Success, $"Pattern should not match: {output}");
            }
        }

        [Fact]
        public void FeatureHeaderPattern_MatchesValidFeatureHeaders()
        {
            // Arrange
            var validHeaders = new[]
            {
                "Users of feature_name:",
                "Users of FEATURE123:",
                "Users of complex-feature.name_123:",
                "Users of maya: (Total of 5 licenses issued; Total of 2 licenses in use)"
            };

            // Act & Assert
            foreach (var header in validHeaders)
            {
                var match = ParsingPatterns.FeatureHeaderPattern.Match(header);
                Assert.True(match.Success, $"Pattern should match: {header}");
                Assert.False(string.IsNullOrWhiteSpace(match.Groups[1].Value));
            }
        }

        [Fact]
        public void FeatureStatusPattern_MatchesValidStatus()
        {
            // Arrange
            var validStatus = new[]
            {
                "Total of 10 licenses issued; Total of 5 licenses in use",
                "Total of 1 licenses issued; Total of 0 licenses in use",
                "total of 100 licenses issued; total of 50 licenses in use",
                "Total of 25 licenses issued; Total of 25 licenses in use"
            };

            // Act & Assert
            foreach (var status in validStatus)
            {
                var match = ParsingPatterns.FeatureStatusPattern.Match(status);
                Assert.True(match.Success, $"Pattern should match: {status}");

                Assert.True(int.TryParse(match.Groups[1].Value, out var total));
                Assert.True(int.TryParse(match.Groups[2].Value, out var inUse));

                Assert.True(total >= 0);
                Assert.True(inUse >= 0);
            }
        }

        [Fact]
        public void ErrorPattern_MatchesVariousErrors()
        {
            // Arrange
            var errorLines = new[]
            {
                "ERROR: Cannot connect to license server",
                "FATAL: License file not found",
                "WARNING: License server not responding",
                "Error: Invalid feature name",
                "error: connection refused"
            };

            // Act & Assert
            foreach (var error in errorLines)
            {
                var match = ParsingPatterns.ErrorPattern.Match(error);
                Assert.True(match.Success, $"Pattern should match error: {error}");
                Assert.False(string.IsNullOrWhiteSpace(match.Groups[2].Value));
            }
        }

        [Fact]
        public void ContainsErrorPatterns_DetectsErrorsInText()
        {
            // Arrange
            var errorText = @"
License server status
Users of maya:
ERROR: Cannot connect to license server
Total of 10 licenses issued
";

            var cleanText = @"
License server UP
Users of maya:
Total of 10 licenses issued; Total of 2 licenses in use
";

            // Act & Assert
            Assert.True(ParsingPatterns.ContainsErrorPatterns(errorText));
            Assert.False(ParsingPatterns.ContainsErrorPatterns(cleanText));
        }

        [Fact]
        public void ExtractErrorMessages_ExtractsAllErrors()
        {
            // Arrange
            var textWithErrors = @"
License server status
ERROR: Cannot connect to license server
WARNING: License server not responding
Users of maya:
FATAL: License file not found
";

            // Act
            var errors = ParsingPatterns.ExtractErrorMessages(textWithErrors);

            // Assert
            Assert.Equal(3, errors.Length);
            Assert.Contains("Cannot connect to license server", errors);
            Assert.Contains("License server not responding", errors);
            Assert.Contains("License file not found", errors);
        }

        [Fact]
        public void GetCommandPattern_ReturnsCorrectPattern()
        {
            // Act & Assert
            Assert.Equal(ParsingPatterns.ServerStatusPattern, ParsingPatterns.GetCommandPattern("lmstat"));
            Assert.Equal(ParsingPatterns.RemoveSuccessPattern, ParsingPatterns.GetCommandPattern("lmremove"));
            Assert.Equal(ParsingPatterns.ChecksumPattern, ParsingPatterns.GetCommandPattern("lmcksum"));
            Assert.Equal(ParsingPatterns.DiagFeaturePattern, ParsingPatterns.GetCommandPattern("lmdiag"));
            Assert.Equal(ParsingPatterns.HostIdPattern, ParsingPatterns.GetCommandPattern("lmhostid"));
            Assert.Equal(ParsingPatterns.ErrorPattern, ParsingPatterns.GetCommandPattern("unknown"));
        }

        [Fact]
        public void IndicatesSuccess_CorrectlyIdentifiesSuccess()
        {
            // Arrange
            var successOutputs = new[]
            {
                "license server UP",
                "Removed user test from feature maya",
                "License server status: good",
                "Total of 10 licenses issued"
            };

            var failureOutputs = new[]
            {
                "ERROR: Cannot connect to license server",
                "FATAL: License file not found",
                "Failed to remove user",
                "Invalid feature name"
            };

            // Act & Assert
            foreach (var output in successOutputs)
            {
                Assert.True(ParsingPatterns.IndicatesSuccess(output), $"Should indicate success: {output}");
            }

            foreach (var output in failureOutputs)
            {
                Assert.False(ParsingPatterns.IndicatesSuccess(output), $"Should not indicate success: {output}");
            }
        }

        [Fact]
        public void RemoveSuccessPattern_MatchesSuccessMessages()
        {
            // Arrange
            var successMessages = new[]
            {
                "Removed user test from feature maya",
                "Released license for user john",
                "Removed license successfully",
                "RELEASED user jane"
            };

            // Act & Assert
            foreach (var message in successMessages)
            {
                var match = ParsingPatterns.RemoveSuccessPattern.Match(message);
                Assert.True(match.Success, $"Pattern should match: {message}");
            }
        }

        [Fact]
        public void ConnectionErrorPattern_MatchesConnectionErrors()
        {
            // Arrange
            var connectionErrors = new[]
            {
                "Cannot connect to license server",
                "Connection refused",
                "No route to host",
                "License server not responding",
                "cannot connect to license server"
            };

            // Act & Assert
            foreach (var error in connectionErrors)
            {
                var match = ParsingPatterns.ConnectionErrorPattern.Match(error);
                Assert.True(match.Success, $"Pattern should match: {error}");
            }
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}