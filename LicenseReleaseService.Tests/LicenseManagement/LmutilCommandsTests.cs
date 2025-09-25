using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    [TestClass]
    public class LmutilCommandsTests
    {
        [TestMethod]
        public void LmutilCommands_GetCommandLineArgument_ShouldReturnCorrectCommand()
        {
            // Arrange & Act
            var lmstatCommand = LmutilCommands.Lmstat.GetCommandLineArgument();
            var lmremoveCommand = LmutilCommands.Lmremove.GetCommandLineArgument();
            var lmcksumCommand = LmutilCommands.Lmcksum.GetCommandLineArgument();

            // Assert
            Assert.AreEqual("lmstat", lmstatCommand, "Lmstat command should return 'lmstat'");
            Assert.AreEqual("lmremove", lmremoveCommand, "Lmremove command should return 'lmremove'");
            Assert.AreEqual("lmcksum", lmcksumCommand, "Lmcksum command should return 'lmcksum'");
        }

        [TestMethod]
        public void LmutilCommands_GetAllCommands_ShouldReturnAllCommands()
        {
            // Arrange & Act
            var allCommands = LmutilCommandsExtensions.GetAllCommands();

            // Assert
            Assert.IsNotNull(allCommands, "All commands should not be null");
            Assert.IsTrue(allCommands.Length > 0, "Should return at least one command");
            Assert.IsTrue(allCommands.Contains(LmutilCommands.Lmstat), "Should include Lmstat command");
            Assert.IsTrue(allCommands.Contains(LmutilCommands.Lmremove), "Should include Lmremove command");
            Assert.IsTrue(allCommands.Contains(LmutilCommands.Lmcksum), "Should include Lmcksum command");
        }

        [TestMethod]
        public void LmutilCommands_GetServerCommands_ShouldReturnOnlyServerCommands()
        {
            // Arrange & Act
            var serverCommands = LmutilCommandsExtensions.GetServerCommands();

            // Assert
            Assert.IsNotNull(serverCommands, "Server commands should not be null");
            Assert.IsTrue(serverCommands.Length > 0, "Should return at least one server command");
            Assert.IsTrue(serverCommands.Contains(LmutilCommands.Lmstat), "Should include Lmstat as server command");
            Assert.IsTrue(serverCommands.Contains(LmutilCommands.Lmremove), "Should include Lmremove as server command");
            Assert.IsFalse(serverCommands.Contains(LmutilCommands.Lmcksum), "Should not include Lmcksum as server command");
            Assert.IsFalse(serverCommands.Contains(LmutilCommands.LmstatVersion), "Should not include LmstatVersion as server command");
        }

        [TestMethod]
        public void LmutilCommands_GetOfflineCommands_ShouldReturnOnlyOfflineCommands()
        {
            // Arrange & Act
            var offlineCommands = LmutilCommandsExtensions.GetOfflineCommands();

            // Assert
            Assert.IsNotNull(offlineCommands, "Offline commands should not be null");
            Assert.IsTrue(offlineCommands.Length > 0, "Should return at least one offline command");
            Assert.IsTrue(offlineCommands.Contains(LmutilCommands.Lmcksum), "Should include Lmcksum as offline command");
            Assert.IsTrue(offlineCommands.Contains(LmutilCommands.LmstatVersion), "Should include LmstatVersion as offline command");
            Assert.IsFalse(offlineCommands.Contains(LmutilCommands.Lmstat), "Should not include Lmstat as offline command");
            Assert.IsFalse(offlineCommands.Contains(LmutilCommands.Lmremove), "Should not include Lmremove as offline command");
        }

        [TestMethod]
        public void LmutilCommands_RequiresServerConnection_ShouldCorrectlyIdentifyServerCommands()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(LmutilCommands.Lmstat.RequiresServerConnection(), "Lmstat should require server connection");
            Assert.IsTrue(LmutilCommands.Lmremove.RequiresServerConnection(), "Lmremove should require server connection");
            Assert.IsTrue(LmutilCommands.LmstatFeature.RequiresServerConnection(), "LmstatFeature should require server connection");
            Assert.IsFalse(LmutilCommands.Lmcksum.RequiresServerConnection(), "Lmcksum should not require server connection");
            Assert.IsFalse(LmutilCommands.LmstatVersion.RequiresServerConnection(), "LmstatVersion should not require server connection");
            Assert.IsFalse(LmutilCommands.LmstatHelp.RequiresServerConnection(), "LmstatHelp should not require server connection");
        }

        [TestMethod]
        public void LmutilCommands_GetCategory_ShouldReturnCorrectCategory()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("Status", LmutilCommands.Lmstat.GetCategory(), "Lmstat should be in Status category");
            Assert.AreEqual("Status", LmutilCommands.LmstatVerbose.GetCategory(), "LmstatVerbose should be in Status category");
            Assert.AreEqual("Management", LmutilCommands.Lmremove.GetCategory(), "Lmremove should be in Management category");
            Assert.AreEqual("Utility", LmutilCommands.Lmcksum.GetCategory(), "Lmcksum should be in Utility category");
            Assert.AreEqual("Utility", LmutilCommands.LmstatVersion.GetCategory(), "LmstatVersion should be in Utility category");
            Assert.AreEqual("Query", LmutilCommands.LmstatFeature.GetCategory(), "LmstatFeature should be in Query category");
            Assert.AreEqual("Query", LmutilCommands.LmstatUser.GetCategory(), "LmstatUser should be in Query category");
            Assert.AreEqual("Unknown", (LmutilCommands)99.GetCategory(), "Unknown command should return Unknown category");
        }

        [TestMethod]
        public void LmutilCommands_EnumValues_ShouldHaveValidDescriptions()
        {
            // Arrange
            var allCommands = LmutilCommandsExtensions.GetAllCommands();

            // Act & Assert
            foreach (var command in allCommands)
            {
                var commandLineArg = command.GetCommandLineArgument();
                Assert.IsFalse(string.IsNullOrWhiteSpace(commandLineArg),
                    $"Command {command} should have a valid command line argument");

                // Should contain letters, numbers, hyphens, or spaces only
                Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(commandLineArg, @"^[a-zA-Z0-9\s\-]+$"),
                    $"Command {command} has invalid command line argument: {commandLineArg}");
            }
        }

        [TestMethod]
        public void LmutilCommands_ServerAndOfflineCommands_ShouldBeMutuallyExclusive()
        {
            // Arrange
            var serverCommands = LmutilCommandsExtensions.GetServerCommands();
            var offlineCommands = LmutilCommandsExtensions.GetOfflineCommands();

            // Act & Assert
            foreach (var serverCommand in serverCommands)
            {
                Assert.IsFalse(offlineCommands.Contains(serverCommand),
                    $"Server command {serverCommand} should not be in offline commands");
            }

            foreach (var offlineCommand in offlineCommands)
            {
                Assert.IsFalse(serverCommands.Contains(offlineCommand),
                    $"Offline command {offlineCommand} should not be in server commands");
            }
        }

        [TestMethod]
        public void LmutilCommands_AllCommands_ShouldBeCoveredByCategories()
        {
            // Arrange
            var allCommands = LmutilCommandsExtensions.GetAllCommands();
            var validCategories = new[] { "Status", "Management", "Utility", "Query" };

            // Act & Assert
            foreach (var command in allCommands)
            {
                var category = command.GetCategory();
                Assert.IsTrue(validCategories.Contains(category) || category == "Unknown",
                    $"Command {command} has invalid category: {category}");
            }
        }
    }
}