using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    [TestClass]
    public class LmutilCommandBuilderTests
    {
        private Mock<ILogger<LmutilCommandBuilder>> _mockLogger;
        private LmutilCommandBuilder _commandBuilder;

        [TestInitialize]
        public void TestInitialize()
        {
            _mockLogger = new Mock<ILogger<LmutilCommandBuilder>>();
            _commandBuilder = new LmutilCommandBuilder(_mockLogger.Object);
        }

        [TestMethod]
        public void Constructor_WithValidLogger_ShouldInitialize()
        {
            // Arrange & Act
            var builder = new LmutilCommandBuilder(_mockLogger.Object);

            // Assert
            Assert.IsNotNull(builder, "Command builder should be created");
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new LmutilCommandBuilder(null),
                "Should throw exception for null logger");
        }

        [TestMethod]
        public void AddServerAlias_WithValidParameters_ShouldAddAlias()
        {
            // Arrange
            var alias = "testserver";
            var server = "localhost";
            var port = 27000;

            // Act
            _commandBuilder.AddServerAlias(alias, server, port);

            // Assert
            var aliases = _commandBuilder.GetServerAliases();
            Assert.IsTrue(aliases.ContainsKey(alias), "Alias should be added");
            Assert.AreEqual("27000@localhost", aliases[alias], "Alias should point to correct server");
        }

        [TestMethod]
        public void AddServerAlias_WithNullAlias_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.AddServerAlias(null, "localhost", 27000),
                "Should throw exception for null alias");
        }

        [TestMethod]
        public void AddServerAlias_WithEmptyAlias_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.AddServerAlias("", "localhost", 27000),
                "Should throw exception for empty alias");
        }

        [TestMethod]
        public void AddServerAlias_WithNullServer_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.AddServerAlias("test", null, 27000),
                "Should throw exception for null server");
        }

        [TestMethod]
        public void AddServerAlias_WithInvalidPort_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.AddServerAlias("test", "localhost", 0),
                "Should throw exception for invalid port");
        }

        [TestMethod]
        public void RemoveServerAlias_WithExistingAlias_ShouldRemoveAlias()
        {
            // Arrange
            _commandBuilder.AddServerAlias("test", "localhost", 27000);

            // Act
            _commandBuilder.RemoveServerAlias("test");

            // Assert
            var aliases = _commandBuilder.GetServerAliases();
            Assert.IsFalse(aliases.ContainsKey("test"), "Alias should be removed");
        }

        [TestMethod]
        public void BuildStatusCommand_WithValidParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;
            var verbose = true;

            // Act
            var result = _commandBuilder.BuildStatusCommand(server, port, verbose);

            // Assert
            Assert.IsTrue(result.Contains("lmstat"), "Should include lmstat command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsTrue(result.Contains("-v"), "Should include verbose flag");
        }

        [TestMethod]
        public void BuildStatusCommand_WithoutVerbose_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;
            var verbose = false;

            // Act
            var result = _commandBuilder.BuildStatusCommand(server, port, verbose);

            // Assert
            Assert.IsTrue(result.Contains("lmstat"), "Should include lmstat command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsFalse(result.Contains("-v"), "Should not include verbose flag");
        }

        [TestMethod]
        public void BuildFeatureStatusCommand_WithValidParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;
            var feature = "test_feature";

            // Act
            var result = _commandBuilder.BuildFeatureStatusCommand(server, port, feature);

            // Assert
            Assert.IsTrue(result.Contains("lmstat -f"), "Should include lmstat -f command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsTrue(result.Contains("test_feature"), "Should include feature name");
        }

        [TestMethod]
        public void BuildFeatureStatusCommand_WithNullFeature_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.BuildFeatureStatusCommand("localhost", 27000, null),
                "Should throw exception for null feature");
        }

        [TestMethod]
        public void BuildUserStatusCommand_WithValidParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;
            var username = "testuser";

            // Act
            var result = _commandBuilder.BuildUserStatusCommand(server, port, username);

            // Assert
            Assert.IsTrue(result.Contains("lmstat -u"), "Should include lmstat -u command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsTrue(result.Contains("testuser"), "Should include username");
        }

        [TestMethod]
        public void BuildUserStatusCommand_WithNullUsername_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.BuildUserStatusCommand("localhost", 27000, null),
                "Should throw exception for null username");
        }

        [TestMethod]
        public void BuildRemoveCommand_WithMinimumParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;
            var feature = "test_feature";
            var username = "testuser";

            // Act
            var result = _commandBuilder.BuildRemoveCommand(server, port, feature, username);

            // Assert
            Assert.IsTrue(result.Contains("lmremove"), "Should include lmremove command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsTrue(result.Contains("test_feature"), "Should include feature name");
            Assert.IsTrue(result.Contains("testuser"), "Should include username");
        }

        [TestMethod]
        public void BuildRemoveCommand_WithAllParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;
            var feature = "test_feature";
            var username = "testuser";
            var hostname = "testhost";
            var display = "testdisplay";
            var force = true;

            // Act
            var result = _commandBuilder.BuildRemoveCommand(server, port, feature, username, hostname, display, force);

            // Assert
            Assert.IsTrue(result.Contains("lmremove"), "Should include lmremove command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsTrue(result.Contains("test_feature"), "Should include feature name");
            Assert.IsTrue(result.Contains("testuser"), "Should include username");
            Assert.IsTrue(result.Contains("testhost"), "Should include hostname");
            Assert.IsTrue(result.Contains("testdisplay"), "Should include display");
            Assert.IsTrue(result.Contains("-force"), "Should include force flag");
        }

        [TestMethod]
        public void BuildRemoveCommand_WithNullFeature_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.BuildRemoveCommand("localhost", 27000, null, "testuser"),
                "Should throw exception for null feature");
        }

        [TestMethod]
        public void BuildRemoveCommand_WithNullUsername_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.BuildRemoveCommand("localhost", 27000, "test_feature", null),
                "Should throw exception for null username");
        }

        [TestMethod]
        public void BuildChecksumCommand_WithValidParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var licensePath = @"C:\licenses\license.dat";

            // Act
            var result = _commandBuilder.BuildChecksumCommand(licensePath);

            // Assert
            Assert.IsTrue(result.Contains("lmcksum"), "Should include lmcksum command");
            Assert.IsTrue(result.Contains("\"C:\\licenses\\license.dat\""), "Should include license path");
        }

        [TestMethod]
        public void BuildChecksumCommand_WithNullLicensePath_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.BuildChecksumCommand(null),
                "Should throw exception for null license path");
        }

        [TestMethod]
        public void BuildLongStatusCommand_WithValidParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;
            var includeUsage = true;

            // Act
            var result = _commandBuilder.BuildLongStatusCommand(server, port, includeUsage);

            // Assert
            Assert.IsTrue(result.Contains("lmstat -l"), "Should include lmstat -l command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsTrue(result.Contains("-a"), "Should include usage flag");
        }

        [TestMethod]
        public void BuildDaemonStatusCommand_WithValidParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;

            // Act
            var result = _commandBuilder.BuildDaemonStatusCommand(server, port);

            // Assert
            Assert.IsTrue(result.Contains("lmstat -d"), "Should include lmstat -d command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
        }

        [TestMethod]
        public void BuildVendorStatusCommand_WithValidParameters_ShouldBuildCorrectCommand()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;

            // Act
            var result = _commandBuilder.BuildVendorStatusCommand(server, port);

            // Assert
            Assert.IsTrue(result.Contains("lmstat -s"), "Should include lmstat -s command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
        }

        [TestMethod]
        public void BuildVersionCommand_ShouldBuildCorrectCommand()
        {
            // Act
            var result = _commandBuilder.BuildVersionCommand();

            // Assert
            Assert.IsTrue(result.Contains("lmstat -V"), "Should include lmstat -V command");
        }

        [TestMethod]
        public void BuildHelpCommand_ShouldBuildCorrectCommand()
        {
            // Act
            var result = _commandBuilder.BuildHelpCommand();

            // Assert
            Assert.IsTrue(result.Contains("lmstat -h"), "Should include lmstat -h command");
        }

        [TestMethod]
        public void BuildCommand_WithValidArguments_ShouldBuildCorrectCommand()
        {
            // Arrange
            var command = LmutilCommands.Lmstat;
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 27000);
            arguments.AddFlag("v");

            // Act
            var result = _commandBuilder.BuildCommand(command, arguments);

            // Assert
            Assert.IsTrue(result.Contains("lmstat"), "Should include command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
            Assert.IsTrue(result.Contains("-v"), "Should include flag");
        }

        [TestMethod]
        public void BuildCommand_WithNullArguments_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => _commandBuilder.BuildCommand(LmutilCommands.Lmstat, null),
                "Should throw exception for null arguments");
        }

        [TestMethod]
        public void BuildCommand_WithInvalidArguments_ShouldThrowException()
        {
            // Arrange
            var command = LmutilCommands.Lmstat;
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 0); // Invalid port

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.BuildCommand(command, arguments),
                "Should throw exception for invalid arguments");
        }

        [TestMethod]
        public void BuildCommandWithAlias_WithValidAlias_ShouldBuildCorrectCommand()
        {
            // Arrange
            _commandBuilder.AddServerAlias("testalias", "localhost", 27000);
            var command = LmutilCommands.Lmstat;
            var arguments = new CommandArguments();

            // Act
            var result = _commandBuilder.BuildCommandWithAlias(command, "testalias", arguments);

            // Assert
            Assert.IsTrue(result.Contains("lmstat"), "Should include command");
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification from alias");
        }

        [TestMethod]
        public void BuildCommandWithAlias_WithNonexistentAlias_ShouldThrowException()
        {
            // Arrange
            var command = LmutilCommands.Lmstat;
            var arguments = new CommandArguments();

            // Act & Assert
            Assert.ThrowsException<KeyNotFoundException>(() => _commandBuilder.BuildCommandWithAlias(command, "nonexistent", arguments),
                "Should throw exception for nonexistent alias");
        }

        [TestMethod]
        public void BuildCommandWithAlias_WithNullAlias_ShouldThrowException()
        {
            // Arrange
            var command = LmutilCommands.Lmstat;
            var arguments = new CommandArguments();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _commandBuilder.BuildCommandWithAlias(command, null, arguments),
                "Should throw exception for null alias");
        }

        [TestMethod]
        public void BuildMultiServerCommands_WithValidServers_ShouldBuildCommands()
        {
            // Arrange
            var servers = new List<ServerConfiguration>
            {
                new ServerConfiguration("localhost", 27000),
                new ServerConfiguration("server2", 28000, 60, true)
            };
            var command = LmutilCommands.Lmstat;

            // Act
            var results = _commandBuilder.BuildMultiServerCommands(command, servers);

            // Assert
            Assert.AreEqual(2, results.Count, "Should build commands for all servers");
            Assert.IsTrue(results[0].Contains("localhost:27000"), "First command should include first server");
            Assert.IsTrue(results[1].Contains("server2:28000"), "Second command should include second server");
            Assert.IsTrue(results[1].Contains("-timeout 60"), "Second command should include timeout");
            Assert.IsTrue(results[1].Contains("-debug"), "Second command should include debug flag");
        }

        [TestMethod]
        public void BuildMultiServerCommands_WithNullServers_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => _commandBuilder.BuildMultiServerCommands(LmutilCommands.Lmstat, null),
                "Should throw exception for null servers");
        }

        [TestMethod]
        public void ValidateServerPrerequisites_WithValidServer_ShouldReturnTrue()
        {
            // Arrange
            var server = "localhost";
            var port = 27000;

            // Act
            var result = _commandBuilder.ValidateServerPrerequisites(server, port);

            // Assert
            Assert.IsTrue(result, "Should validate valid server");
        }

        [TestMethod]
        public void ValidateServerPrerequisites_WithNullServer_ShouldReturnFalse()
        {
            // Arrange
            var port = 27000;

            // Act
            var result = _commandBuilder.ValidateServerPrerequisites(null, port);

            // Assert
            Assert.IsFalse(result, "Should not validate null server");
        }

        [TestMethod]
        public void ValidateServerPrerequisites_WithInvalidPort_ShouldReturnFalse()
        {
            // Arrange
            var server = "localhost";
            var port = 0;

            // Act
            var result = _commandBuilder.ValidateServerPrerequisites(server, port);

            // Assert
            Assert.IsFalse(result, "Should not validate invalid port");
        }

        [TestMethod]
        public void GetExecutionHints_WithLmstatCommand_ShouldReturnCorrectHints()
        {
            // Arrange
            var command = LmutilCommands.Lmstat;

            // Act
            var hints = _commandBuilder.GetExecutionHints(command);

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(5), hints.ExpectedExecutionTime, "Should have correct execution time");
            Assert.IsTrue(hints.CanBeCached, "Should be cacheable");
            Assert.AreEqual(TimeSpan.FromMinutes(5), hints.CacheDuration, "Should have correct cache duration");
            Assert.IsTrue(hints.IsIdempotent, "Should be idempotent");
            Assert.IsFalse(hints.RequiresConfirmation, "Should not require confirmation");
            Assert.IsFalse(hints.HasSideEffects, "Should not have side effects");
        }

        [TestMethod]
        public void GetExecutionHints_WithLmremoveCommand_ShouldReturnCorrectHints()
        {
            // Arrange
            var command = LmutilCommands.Lmremove;

            // Act
            var hints = _commandBuilder.GetExecutionHints(command);

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(10), hints.ExpectedExecutionTime, "Should have correct execution time");
            Assert.IsFalse(hints.CanBeCached, "Should not be cacheable");
            Assert.AreEqual(TimeSpan.Zero, hints.CacheDuration, "Should have zero cache duration");
            Assert.IsTrue(hints.IsIdempotent, "Should be idempotent");
            Assert.IsTrue(hints.RequiresConfirmation, "Should require confirmation");
            Assert.IsTrue(hints.HasSideEffects, "Should have side effects");
        }

        [TestMethod]
        public void GetExecutionHints_WithLmcksumCommand_ShouldReturnCorrectHints()
        {
            // Arrange
            var command = LmutilCommands.Lmcksum;

            // Act
            var hints = _commandBuilder.GetExecutionHints(command);

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(2), hints.ExpectedExecutionTime, "Should have correct execution time");
            Assert.IsTrue(hints.CanBeCached, "Should be cacheable");
            Assert.AreEqual(TimeSpan.FromHours(1), hints.CacheDuration, "Should have correct cache duration");
            Assert.IsTrue(hints.IsIdempotent, "Should be idempotent");
            Assert.IsFalse(hints.RequiresConfirmation, "Should not require confirmation");
            Assert.IsFalse(hints.HasSideEffects, "Should not have side effects");
        }

        [TestMethod]
        public void GetExecutionHints_WithUnknownCommand_ShouldReturnDefaultHints()
        {
            // Arrange
            var command = (LmutilCommands)99; // Unknown command

            // Act
            var hints = _commandBuilder.GetExecutionHints(command);

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(5), hints.ExpectedExecutionTime, "Should have default execution time");
            Assert.IsFalse(hints.CanBeCached, "Should not be cacheable");
            Assert.IsTrue(hints.IsIdempotent, "Should be idempotent");
        }

        [TestMethod]
        public void BuildCommand_ShouldLogCommand()
        {
            // Arrange
            var command = LmutilCommands.Lmstat;
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 27000);

            // Act
            var result = _commandBuilder.BuildCommand(command, arguments);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Built lmutil command")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce, "Should log command building");
        }

        [TestMethod]
        public void BuildCommand_WithSensitiveInformation_ShouldSanitizeForLogging()
        {
            // Arrange
            var command = LmutilCommands.Lmstat;
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 27000);
            arguments.AddArgument("password", "secret123");
            arguments.AddArgument("token", "abc123def456");

            // Act
            var result = _commandBuilder.BuildCommand(command, arguments);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                    {
                        var logMessage = v.ToString();
                        return logMessage.Contains("Built lmutil command") &&
                               !logMessage.Contains("secret123") &&
                               !logMessage.Contains("abc123def456") &&
                               logMessage.Contains("[REDACTED]");
                    }),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce, "Should sanitize sensitive information for logging");
        }

        [TestMethod]
        public void ServerConfiguration_Constructor_WithDefaultValues_ShouldInitialize()
        {
            // Arrange & Act
            var config = new ServerConfiguration();

            // Assert
            Assert.AreEqual(30, config.Timeout, "Default timeout should be 30");
            Assert.IsFalse(config.Debug, "Debug should be false by default");
        }

        [TestMethod]
        public void ServerConfiguration_Constructor_WithParameters_ShouldInitialize()
        {
            // Arrange & Act
            var config = new ServerConfiguration("localhost", 27000, 60, true);

            // Assert
            Assert.AreEqual("localhost", config.Address, "Address should be set");
            Assert.AreEqual(27000, config.Port, "Port should be set");
            Assert.AreEqual(60, config.Timeout, "Timeout should be set");
            Assert.IsTrue(config.Debug, "Debug should be enabled");
        }

        [TestMethod]
        public void ServerConfiguration_Constructor_WithNullAddress_ShouldThrowException()
        {
            // Arrange, Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new ServerConfiguration(null, 27000),
                "Should throw exception for null address");
        }

        [TestMethod]
        public void CommandExecutionHints_Constructor_WithDefaultValues_ShouldInitialize()
        {
            // Arrange & Act
            var hints = new CommandExecutionHints();

            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(5), hints.ExpectedExecutionTime, "Default execution time should be 5 seconds");
            Assert.IsFalse(hints.CanBeCached, "Should not be cacheable by default");
            Assert.AreEqual(TimeSpan.Zero, hints.CacheDuration, "Cache duration should be zero by default");
            Assert.IsTrue(hints.IsIdempotent, "Should be idempotent by default");
            Assert.IsFalse(hints.RequiresConfirmation, "Should not require confirmation by default");
            Assert.IsFalse(hints.HasSideEffects, "Should not have side effects by default");
        }
    }
}