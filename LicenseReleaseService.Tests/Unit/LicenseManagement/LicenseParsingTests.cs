using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.Unit.LicenseManagement
{
    [TestClass]
    public class LicenseParsingTests
    {
        private Mock<ILogger<LmutilOutputParser>> _mockLogger;
        private LmutilOutputParser _parser;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<LmutilOutputParser>>();
            _parser = new LmutilOutputParser(_mockLogger.Object);
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new LmutilOutputParser(null));
        }

        [TestMethod]
        public void Constructor_ValidLogger_CreatesInstance()
        {
            // Arrange
            var logger = new Mock<ILogger<LmutilOutputParser>>().Object;

            // Act
            var parser = new LmutilOutputParser(logger);

            // Assert
            Assert.IsNotNull(parser);
        }

        #endregion

        #region ParseLmstatOutput Tests

        [TestMethod]
        public void ParseLmstatOutput_ValidServerStatus_ReturnsServerStatus()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.ValidServerStatus;
            var serverAddress = "test-server:27000";

            // Act
            var result = _parser.ParseLmstatOutput(output, serverAddress);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(serverAddress, result.ServerAddress);
            Assert.IsTrue(result.IsServerUp);
            Assert.AreEqual(output, result.RawOutput);
            _mockLogger.VerifyLog(LogLevel.Information, "Successfully parsed lmstat output", Times.Once);
        }

        [TestMethod]
        public void ParseLmstatOutput_MultipleFeatures_ParsesAllFeatures()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.MultipleFeatures;
            var serverAddress = "test-server:27000";

            // Act
            var result = _parser.ParseLmstatOutput(output, serverAddress);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Features.Count > 1);
            Assert.IsTrue(result.Features.ContainsKey("SolidWorks"));
            Assert.IsTrue(result.Features.ContainsKey("SolidWorks_Explorer"));

            var solidWorksFeature = result.Features["SolidWorks"];
            Assert.IsTrue(solidWorksFeature.TotalLicenses > 0);
        }

        [TestMethod]
        public void ParseLmstatOutput_ExhaustedLicenses_ParsesCorrectStatus()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.ExhaustedLicenses;
            var serverAddress = "test-server:27000";

            // Act
            var result = _parser.ParseLmstatOutput(output, serverAddress);

            // Assert
            Assert.IsNotNull(result);
            var feature = result.Features.Values.First();
            Assert.AreEqual(feature.TotalLicenses, feature.LicensesInUse);
            Assert.AreEqual(0, feature.AvailableLicenses);
        }

        [TestMethod]
        public void ParseLmstatOutput_WithUserInformation_ParsesUserDetails()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.WithUserDetails;
            var serverAddress = "test-server:27000";

            // Act
            var result = _parser.ParseLmstatOutput(output, serverAddress);

            // Assert
            Assert.IsNotNull(result);
            var feature = result.Features.Values.First();
            Assert.IsTrue(feature.Users.Count > 0);

            var user = feature.Users.Values.First();
            Assert.IsFalse(string.IsNullOrWhiteSpace(user.UserName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(user.HostName));
        }

        [TestMethod]
        public void ParseLmstatOutput_EmptyOutput_ThrowsOutputParsingException()
        {
            // Arrange
            var output = "";
            var serverAddress = "test-server:27000";

            // Act & Assert
            var exception = Assert.ThrowsException<OutputParsingException>(
                () => _parser.ParseLmstatOutput(output, serverAddress));

            Assert.AreEqual("Empty lmstat output", exception.Message);
        }

        [TestMethod]
        public void ParseLmstatOutput_NullOutput_ThrowsOutputParsingException()
        {
            // Arrange
            string output = null;
            var serverAddress = "test-server:27000";

            // Act & Assert
            var exception = Assert.ThrowsException<OutputParsingException>(
                () => _parser.ParseLmstatOutput(output, serverAddress));

            Assert.AreEqual("Empty lmstat output", exception.Message);
        }

        [TestMethod]
        public void ParseLmstatOutput_ErrorOutput_ThrowsOutputParsingException()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.ErrorOutput;
            var serverAddress = "test-server:27000";

            // Act & Assert
            var exception = Assert.ThrowsException<OutputParsingException>(
                () => _parser.ParseLmstatOutput(output, serverAddress));

            Assert.IsTrue(exception.Message.Contains("lmstat command failed"));
            Assert.IsTrue(exception.Message.Contains("Cannot connect to license server"));
        }

        [TestMethod]
        public void ParseLmstatOutput_MalformedOutput_HandlesGracefully()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.MalformedOutput;
            var serverAddress = "test-server:27000";

            // Act
            var result = _parser.ParseLmstatOutput(output, serverAddress);

            // Assert
            Assert.IsNotNull(result);
            // Should parse what it can, even with malformed sections
            _mockLogger.VerifyLog(LogLevel.Information, "Successfully parsed lmstat output", Times.Once);
        }

        [TestMethod]
        public void ParseLmstatOutput_NoServerAddress_ExtractsFromOutput()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.ValidServerStatus;
            var serverAddress = "";

            // Act
            var result = _parser.ParseLmstatOutput(output, serverAddress);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ServerAddress));
        }

        [TestMethod]
        public void ParseLmstatOutput_LargeOutput_HandlesEfficiently()
        {
            // Arrange
            var output = GenerateLargeLicenseOutput(1000); // 1000 lines
            var serverAddress = "test-server:27000";

            // Act
            var startTime = DateTime.UtcNow;
            var result = _parser.ParseLmstatOutput(output, serverAddress);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(duration.TotalMilliseconds < 5000, "Parsing should complete within 5 seconds");
        }

        #endregion

        #region ParseLmremoveOutput Tests

        [TestMethod]
        public void ParseLmremoveOutput_SuccessfulRemoval_ReturnsSuccessResult()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.LmremoveSuccess;
            var feature = "SolidWorks";
            var user = "testuser";

            // Act
            var result = _parser.ParseLmremoveOutput(output, feature, user);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(feature, result.ReleasedFeature);
            Assert.AreEqual(user, result.ReleasedUser);
            Assert.IsNull(result.ErrorMessage);
            _mockLogger.VerifyLog(LogLevel.Information, "Successfully parsed lmremove output", Times.Once);
        }

        [TestMethod]
        public void ParseLmremoveOutput_FailedRemoval_ReturnsFailureResult()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.LmremoveFailed;
            var feature = "SolidWorks";
            var user = "testuser";

            // Act
            var result = _parser.ParseLmremoveOutput(output, feature, user);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(feature, result.ReleasedFeature);
            Assert.AreEqual(user, result.ReleasedUser);
            Assert.IsNotNull(result.ErrorMessage);
            _mockLogger.VerifyLog(LogLevel.Warning, "lmremove command encountered errors", Times.Once);
        }

        [TestMethod]
        public void ParseLmremoveOutput_AmbiguousOutput_DetectsSuccess()
        {
            // Arrange
            var output = "License release operation completed";
            var feature = "SolidWorks";
            var user = "testuser";

            // Act
            var result = _parser.ParseLmremoveOutput(output, feature, user);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(feature, result.ReleasedFeature);
            Assert.AreEqual(user, result.ReleasedUser);
        }

        [TestMethod]
        public void ParseLmremoveOutput_AmbiguousOutputWithNegativeKeywords_DetectsFailure()
        {
            // Arrange
            var output = "License release Failed with Error";
            var feature = "SolidWorks";
            var user = "testuser";

            // Act
            var result = _parser.ParseLmremoveOutput(output, feature, user);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("Unable to determine operation success"));
        }

        [TestMethod]
        public void ParseLmremoveOutput_EmptyOutput_ThrowsOutputParsingException()
        {
            // Arrange
            var output = "";
            var feature = "SolidWorks";
            var user = "testuser";

            // Act & Assert
            var exception = Assert.ThrowsException<OutputParsingException>(
                () => _parser.ParseLmremoveOutput(output, feature, user));

            Assert.AreEqual("Empty lmremove output", exception.Message);
        }

        [TestMethod]
        public void ParseLmremoveOutput_NullOutput_ThrowsOutputParsingException()
        {
            // Arrange
            string output = null;
            var feature = "SolidWorks";
            var user = "testuser";

            // Act & Assert
            var exception = Assert.ThrowsException<OutputParsingException>(
                () => _parser.ParseLmremoveOutput(output, feature, user));

            Assert.AreEqual("Empty lmremove output", exception.Message);
        }

        #endregion

        #region ParseOutputIncrementallyAsync Tests

        [TestMethod]
        public async Task ParseOutputIncrementallyAsync_LmstatCommand_ReturnsServerStatus()
        {
            // Arrange
            var lines = TestData.LicenseServerResponses.ValidServerStatus.Split('\n');
            var command = "lmstat";

            // Act
            var result = await _parser.ParseOutputIncrementallyAsync(lines, command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(LicenseServerStatus));

            var status = (LicenseServerStatus)result;
            Assert.IsTrue(status.IsServerUp);
        }

        [TestMethod]
        public async Task ParseOutputIncrementallyAsync_LmremoveCommand_ReturnsReleaseResult()
        {
            // Arrange
            var lines = TestData.LicenseServerResponses.LmremoveSuccess.Split('\n');
            var command = "lmremove";

            // Act
            var result = await _parser.ParseOutputIncrementallyAsync(lines, command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(LicenseReleaseResult));

            var releaseResult = (LicenseReleaseResult)result;
            Assert.IsTrue(releaseResult.Success);
        }

        [TestMethod]
        public async Task ParseOutputIncrementallyAsync_UnsupportedCommand_ThrowsOutputParsingException()
        {
            // Arrange
            var lines = new[] { "test output" };
            var command = "unsupported";

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<OutputParsingException>(
                () => _parser.ParseOutputIncrementallyAsync(lines, command));

            Assert.IsTrue(exception.Message.Contains("Unsupported command for incremental parsing"));
        }

        [TestMethod]
        public async Task ParseOutputIncrementallyAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            // Arrange
            var lines = GenerateLargeLicenseOutput(1000).Split('\n');
            var command = "lmstat";
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => _parser.ParseOutputIncrementallyAsync(lines, command, cancellationTokenSource.Token));
        }

        [TestMethod]
        public async Task ParseOutputIncrementallyAsync_LargeOutput_TruncatesAtMaxLines()
        {
            // Arrange
            var lines = GenerateLargeLicenseOutput(15000).Split('\n'); // More than max 10000 lines
            var command = "lmstat";

            // Act
            var result = await _parser.ParseOutputIncrementallyAsync(lines, command);

            // Assert
            Assert.IsNotNull(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "Output truncated after", Times.Once);
        }

        [TestMethod]
        public async Task ParseOutputIncrementallyAsync_EmptyLines_HandlesGracefully()
        {
            // Arrange
            var lines = new string[0];
            var command = "lmstat";

            // Act
            var result = await _parser.ParseOutputIncrementallyAsync(lines, command);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(LicenseServerStatus));
        }

        [TestMethod]
        public async Task ParseOutputIncrementallyAsync_Performance_LargeNumberOfLines_CompletesQuickly()
        {
            // Arrange
            var lines = GenerateLargeLicenseOutput(5000).Split('\n');
            var command = "lmstat";

            // Act
            var startTime = DateTime.UtcNow;
            var result = await _parser.ParseOutputIncrementallyAsync(lines, command);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(duration.TotalMilliseconds < 3000, "Incremental parsing should complete within 3 seconds");
        }

        #endregion

        #region ValidateParsedData Tests

        [TestMethod]
        public void ValidateParsedData_ValidStatus_ReturnsTrue()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.VerifyLog(LogLevel.Debug, "Validation completed successfully", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_NullStatus_ReturnsFalse()
        {
            // Arrange
            LicenseServerStatus status = null;

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Error, "Validation failed: Status object is null", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_EmptyServerAddress_ReturnsTrueWithWarning()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            status.ServerAddress = "";

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "Validation warning: Server address is empty", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_NegativeTotalLicenses_ReturnsFalse()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            var feature = status.Features.Values.First();
            feature.TotalLicenses = -1;

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "has negative total licenses", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_NegativeLicensesInUse_ReturnsFalse()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            var feature = status.Features.Values.First();
            feature.LicensesInUse = -1;

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "has negative licenses in use", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_MoreLicensesInUseThanTotal_ReturnsFalse()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            var feature = status.Features.Values.First();
            feature.LicensesInUse = feature.TotalLicenses + 1;

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "has more licenses in use", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_EmptyUsername_ReturnsTrueWithWarning()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            var feature = status.Features.Values.First();
            var user = feature.Users.Values.First();
            user.UserName = "";

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "has user with empty username", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_EmptyHostname_ReturnsTrueWithWarning()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            var feature = status.Features.Values.First();
            var user = feature.Users.Values.First();
            user.HostName = "";

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "user has empty hostname", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_MultipleValidationErrors_ReturnsFalseOnFirstError()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            var feature = status.Features.Values.First();
            feature.TotalLicenses = -1; // First error
            feature.LicensesInUse = -2; // Second error

            // Act
            var result = _parser.ValidateParsedData(status);

            // Assert
            Assert.IsFalse(result);
            _mockLogger.VerifyLog(LogLevel.Warning, "has negative total licenses", Times.Once);
        }

        [TestMethod]
        public void ValidateParsedData_Performance_LargeNumberOfFeatures_CompletesQuickly()
        {
            // Arrange
            var status = CreateLargeLicenseServerStatus(100); // 100 features
            var startTime = DateTime.UtcNow;

            // Act
            var result = _parser.ValidateParsedData(status);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(duration.TotalMilliseconds < 1000, "Validation should complete within 1 second");
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public void ParseLmstatOutput_Performance_MultipleSequentialParses_CompletesQuickly()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.MultipleFeatures;
            var serverAddress = "test-server:27000";

            // Act
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                var result = _parser.ParseLmstatOutput(output, serverAddress);
                Assert.IsNotNull(result);
            }
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsTrue(duration.TotalMilliseconds < 10000, "100 parses should complete within 10 seconds");
        }

        [TestMethod]
        public void ParseLmstatOutput_ConcurrentParsing_ThreadSafe()
        {
            // Arrange
            var output = TestData.LicenseServerResponses.MultipleFeatures;
            var serverAddress = "test-server:27000";
            var tasks = new Task<LicenseServerStatus>[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => _parser.ParseLmstatOutput(output, serverAddress));
            }

            Task.WaitAll(tasks);

            // Assert
            foreach (var task in tasks)
            {
                Assert.IsNotNull(task.Result);
                Assert.AreEqual(serverAddress, task.Result.ServerAddress);
            }
        }

        [TestMethod]
        public void ValidateParsedData_ConcurrentValidation_ThreadSafe()
        {
            // Arrange
            var status = CreateValidLicenseServerStatus();
            var tasks = new Task<bool>[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => _parser.ValidateParsedData(status));
            }

            Task.WaitAll(tasks);

            // Assert
            foreach (var task in tasks)
            {
                Assert.IsTrue(task.Result);
            }
        }

        #endregion

        #region Helper Methods

        private LicenseServerStatus CreateValidLicenseServerStatus()
        {
            var status = new LicenseServerStatus
            {
                ServerAddress = "test-server:27000",
                IsServerUp = true,
                StatusMessage = "License server is UP",
                RawOutput = "Test output"
            };

            var feature = new LicenseFeatureStatus
            {
                FeatureName = "SolidWorks",
                TotalLicenses = 10,
                LicensesInUse = 5,
                AvailableLicenses = 5,
                Status = "Available",
                Users = new Dictionary<string, LicenseUserInfo>
                {
                    ["user1"] = new LicenseUserInfo("user1", "workstation1", "User One"),
                    ["user2"] = new LicenseUserInfo("user2", "workstation2", "User Two")
                }
            };

            status.Features["SolidWorks"] = feature;
            return status;
        }

        private LicenseServerStatus CreateLargeLicenseServerStatus(int featureCount)
        {
            var status = new LicenseServerStatus
            {
                ServerAddress = "test-server:27000",
                IsServerUp = true,
                StatusMessage = "License server is UP",
                RawOutput = "Large test output"
            };

            for (int i = 0; i < featureCount; i++)
            {
                var feature = new LicenseFeatureStatus
                {
                    FeatureName = $"Feature{i}",
                    TotalLicenses = 10,
                    LicensesInUse = 5,
                    AvailableLicenses = 5,
                    Status = "Available",
                    Users = new Dictionary<string, LicenseUserInfo>()
                };

                for (int j = 0; j < 5; j++)
                {
                    feature.Users[$"user{i}_{j}"] = new LicenseUserInfo($"user{i}_{j}", $"workstation{i}_{j}", $"User {i}_{j}");
                }

                status.Features[feature.FeatureName] = feature;
            }

            return status;
        }

        private string GenerateLargeLicenseOutput(int lineCount)
        {
            var lines = new List<string>();
            lines.Add("License server status: UP");
            lines.Add("test-server:27000");

            for (int i = 0; i < lineCount / 10; i++)
            {
                lines.Add($"Feature{i}: 10 total, 5 in use");
                for (int j = 0; j < 5; j++)
                {
                    lines.Add($"  user{i}_{j} workstation{i}_{j} (User {i}_{j}) start 1/1/2024 09:00");
                }
                lines.Add(""); // Empty line
            }

            return string.Join("\n", lines);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for verifying ILogger calls
    /// </summary>
    public static class LoggerParsingExtensions
    {
        public static void VerifyLog(this Mock<ILogger<LmutilOutputParser>> logger, LogLevel expectedLevel, string expectedMessage, Times times)
        {
            logger.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                times);
        }
    }
}