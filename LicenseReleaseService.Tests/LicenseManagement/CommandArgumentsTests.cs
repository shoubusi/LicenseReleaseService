using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.LicenseManagement;

namespace LicenseReleaseService.Tests.LicenseManagement
{
    [TestClass]
    public class CommandArgumentsTests
    {
        [TestMethod]
        public void CommandArguments_Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var arguments = new CommandArguments();

            // Assert
            Assert.AreEqual(30, arguments.Timeout, "Default timeout should be 30 seconds");
            Assert.AreEqual(0, arguments.Verbosity, "Default verbosity should be 0");
            Assert.IsFalse(arguments.Debug, "Debug should be false by default");
            Assert.IsFalse(arguments.Force, "Force should be false by default");
            Assert.IsNotNull(arguments.Options, "Options dictionary should be initialized");
            Assert.AreEqual(0, arguments.Options.Count, "Options should be empty by default");
            Assert.IsNotNull(arguments.Flags, "Flags list should be initialized");
            Assert.AreEqual(0, arguments.Flags.Count, "Flags should be empty by default");
        }

        [TestMethod]
        public void CommandArguments_AddArgument_String_ShouldAddArgument()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.AddArgument("test", "value");

            // Assert
            Assert.IsTrue(arguments.HasArgument("test"), "Should have the added argument");
            Assert.AreEqual("value", arguments.GetArgument("test"), "Argument value should be correct");
            Assert.AreEqual(1, arguments.Options.Count, "Options count should be 1");
        }

        [TestMethod]
        public void CommandArguments_AddArgument_Int_ShouldAddArgument()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.AddArgument("port", 27000);

            // Assert
            Assert.IsTrue(arguments.HasArgument("port"), "Should have the added argument");
            Assert.AreEqual("27000", arguments.GetArgument("port"), "Argument value should be correct");
            Assert.AreEqual(27000, arguments.GetArgument("port", 0), "Argument value as int should be correct");
        }

        [TestMethod]
        public void CommandArguments_AddArgument_Bool_ShouldAddArgument()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.AddArgument("enabled", true);

            // Assert
            Assert.IsTrue(arguments.HasArgument("enabled"), "Should have the added argument");
            Assert.AreEqual("true", arguments.GetArgument("enabled"), "Argument value should be correct");
            Assert.AreEqual(true, arguments.GetArgument("enabled", false), "Argument value as bool should be correct");
        }

        [TestMethod]
        public void CommandArguments_AddArgument_WithNullKey_ShouldThrowException()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => arguments.AddArgument(null, "value"),
                "Should throw exception for null key");
        }

        [TestMethod]
        public void CommandArguments_AddArgument_WithEmptyKey_ShouldThrowException()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => arguments.AddArgument("", "value"),
                "Should throw exception for empty key");
        }

        [TestMethod]
        public void CommandArguments_AddArgument_WithWhitespaceKey_ShouldThrowException()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => arguments.AddArgument("   ", "value"),
                "Should throw exception for whitespace key");
        }

        [TestMethod]
        public void CommandArguments_AddFlag_ShouldAddFlag()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.AddFlag("verbose");

            // Assert
            Assert.IsTrue(arguments.HasFlag("verbose"), "Should have the added flag");
            Assert.AreEqual(1, arguments.Flags.Count, "Flags count should be 1");
            Assert.AreEqual("verbose", arguments.Flags[0], "Flag value should be correct");
        }

        [TestMethod]
        public void CommandArguments_AddFlag_DuplicateFlag_ShouldNotAddDuplicate()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.AddFlag("verbose");
            arguments.AddFlag("verbose");

            // Assert
            Assert.IsTrue(arguments.HasFlag("verbose"), "Should have the flag");
            Assert.AreEqual(1, arguments.Flags.Count, "Should not add duplicate flag");
        }

        [TestMethod]
        public void CommandArguments_AddFlag_WithNullFlag_ShouldThrowException()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => arguments.AddFlag(null),
                "Should throw exception for null flag");
        }

        [TestMethod]
        public void CommandArguments_RemoveArgument_ShouldRemoveArgument()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.AddArgument("test", "value");

            // Act
            arguments.RemoveArgument("test");

            // Assert
            Assert.IsFalse(arguments.HasArgument("test"), "Should not have the removed argument");
            Assert.IsNull(arguments.GetArgument("test"), "Should return null for removed argument");
        }

        [TestMethod]
        public void CommandArguments_RemoveFlag_ShouldRemoveFlag()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.AddFlag("verbose");

            // Act
            arguments.RemoveFlag("verbose");

            // Assert
            Assert.IsFalse(arguments.HasFlag("verbose"), "Should not have the removed flag");
            Assert.AreEqual(0, arguments.Flags.Count, "Flags count should be 0");
        }

        [TestMethod]
        public void CommandArguments_GetArgument_WithDefault_ShouldReturnDefaultWhenNotFound()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act & Assert
            Assert.AreEqual("default", arguments.GetArgument("nonexistent", "default"), "Should return default value");
            Assert.AreEqual(42, arguments.GetArgument("nonexistent", 42), "Should return default int value");
            Assert.AreEqual(true, arguments.GetArgument("nonexistent", true), "Should return default bool value");
        }

        [TestMethod]
        public void CommandArguments_SetServer_ShouldSetServerAndPort()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.SetServer("localhost", 27000);

            // Assert
            Assert.AreEqual("localhost", arguments.Server, "Server should be set correctly");
            Assert.AreEqual(27000, arguments.Port, "Port should be set correctly");
        }

        [TestMethod]
        public void CommandArguments_SetFeature_ShouldSetFeature()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.SetFeature("test_feature");

            // Assert
            Assert.AreEqual("test_feature", arguments.Feature, "Feature should be set correctly");
        }

        [TestMethod]
        public void CommandArguments_SetUser_ShouldSetUserInformation()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.SetUser("testuser", "testhost", "testdisplay");

            // Assert
            Assert.AreEqual("testuser", arguments.Username, "Username should be set correctly");
            Assert.AreEqual("testhost", arguments.Hostname, "Hostname should be set correctly");
            Assert.AreEqual("testdisplay", arguments.Display, "Display should be set correctly");
        }

        [TestMethod]
        public void CommandArguments_SetTimeout_ShouldSetTimeout()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.SetTimeout(60);

            // Assert
            Assert.AreEqual(60, arguments.Timeout, "Timeout should be set correctly");
        }

        [TestMethod]
        public void CommandArguments_EnableDebug_ShouldEnableDebug()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.EnableDebug();

            // Assert
            Assert.IsTrue(arguments.Debug, "Debug should be enabled");
        }

        [TestMethod]
        public void CommandArguments_EnableForce_ShouldEnableForce()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.EnableForce();

            // Assert
            Assert.IsTrue(arguments.Force, "Force should be enabled");
        }

        [TestMethod]
        public void CommandArguments_SetVerbosity_ShouldSetVerbosity()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.SetVerbosity(2);

            // Assert
            Assert.AreEqual(2, arguments.Verbosity, "Verbosity should be set correctly");
        }

        [TestMethod]
        public void CommandArguments_SetFormat_ShouldSetFormat()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            arguments.SetFormat("json");

            // Assert
            Assert.AreEqual("json", arguments.Format, "Format should be set correctly");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithServer_ShouldIncludeServer()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 27000);

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("-c 27000@localhost"), "Should include server specification");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithLicensePath_ShouldIncludeLicensePath()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.LicensePath = @"C:\licenses\license.dat";

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("-c \"C:\\licenses\\license.dat\""), "Should include license path with quotes");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithFeature_ShouldIncludeFeature()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetFeature("test_feature");

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("test_feature"), "Should include feature name");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithUser_ShouldIncludeUser()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetUser("testuser", "testhost", "testdisplay");

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("testuser"), "Should include username");
            Assert.IsTrue(result.Contains("testhost"), "Should include hostname");
            Assert.IsTrue(result.Contains("testdisplay"), "Should include display");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithFlags_ShouldIncludeFlags()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.AddFlag("verbose");
            arguments.AddFlag("debug");

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("-verbose"), "Should include verbose flag");
            Assert.IsTrue(result.Contains("-debug"), "Should include debug flag");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithArguments_ShouldIncludeArguments()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.AddArgument("timeout", "30");
            arguments.AddArgument("format", "json");

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("-timeout 30"), "Should include timeout argument");
            Assert.IsTrue(result.Contains("-format json"), "Should include format argument");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithSpacesInValues_ShouldEscapeWithQuotes()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetFeature("test feature");
            arguments.AddArgument("description", "test description with spaces");

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("\"test feature\""), "Should escape feature with spaces");
            Assert.IsTrue(result.Contains("\"test description with spaces\""), "Should escape argument with spaces");
        }

        [TestMethod]
        public void CommandArguments_BuildArguments_WithQuotesInValues_ShouldEscapeQuotes()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetFeature("test\"feature");

            // Act
            var result = arguments.BuildArguments();

            // Assert
            Assert.IsTrue(result.Contains("\"test\"\"feature\""), "Should escape quotes properly");
        }

        [TestMethod]
        public void CommandArguments_Validate_WithValidArguments_ShouldReturnEmptyErrors()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 27000);
            arguments.SetTimeout(30);

            // Act
            var errors = arguments.Validate();

            // Assert
            Assert.AreEqual(0, errors.Count, "Should have no validation errors");
        }

        [TestMethod]
        public void CommandArguments_Validate_WithInvalidPort_ShouldReturnError()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 0);

            // Act
            var errors = arguments.Validate();

            // Assert
            Assert.AreEqual(1, errors.Count, "Should have one validation error");
            Assert.IsTrue(errors[0].Contains("Port must be between 1 and 65535"), "Should mention port range");
        }

        [TestMethod]
        public void CommandArguments_Validate_WithNegativeTimeout_ShouldReturnError()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetTimeout(-1);

            // Act
            var errors = arguments.Validate();

            // Assert
            Assert.AreEqual(1, errors.Count, "Should have one validation error");
            Assert.IsTrue(errors[0].Contains("Timeout must be non-negative"), "Should mention non-negative timeout");
        }

        [TestMethod]
        public void CommandArguments_Validate_WithNegativeVerbosity_ShouldReturnError()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetVerbosity(-1);

            // Act
            var errors = arguments.Validate();

            // Assert
            Assert.AreEqual(1, errors.Count, "Should have one validation error");
            Assert.IsTrue(errors[0].Contains("Verbosity level must be non-negative"), "Should mention non-negative verbosity");
        }

        [TestMethod]
        public void CommandArguments_Validate_WithFeatureFlagButNoFeature_ShouldReturnError()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.AddFlag("f");

            // Act
            var errors = arguments.Validate();

            // Assert
            Assert.AreEqual(1, errors.Count, "Should have one validation error");
            Assert.IsTrue(errors[0].Contains("Feature name is required"), "Should mention required feature name");
        }

        [TestMethod]
        public void CommandArguments_Validate_WithUserFlagButNoUser_ShouldReturnError()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.AddFlag("u");

            // Act
            var errors = arguments.Validate();

            // Assert
            Assert.AreEqual(1, errors.Count, "Should have one validation error");
            Assert.IsTrue(errors[0].Contains("Username is required"), "Should mention required username");
        }

        [TestMethod]
        public void CommandArguments_Copy_ShouldCreateExactCopy()
        {
            // Arrange
            var original = new CommandArguments();
            original.SetServer("localhost", 27000);
            original.SetFeature("test_feature");
            original.SetUser("testuser", "testhost", "testdisplay");
            original.SetTimeout(60);
            original.EnableDebug();
            original.EnableForce();
            original.AddFlag("verbose");
            original.AddArgument("custom", "value");

            // Act
            var copy = original.Copy();

            // Assert
            Assert.AreEqual(original.Server, copy.Server, "Server should be copied");
            Assert.AreEqual(original.Port, copy.Port, "Port should be copied");
            Assert.AreEqual(original.Feature, copy.Feature, "Feature should be copied");
            Assert.AreEqual(original.Username, copy.Username, "Username should be copied");
            Assert.AreEqual(original.Hostname, copy.Hostname, "Hostname should be copied");
            Assert.AreEqual(original.Display, copy.Display, "Display should be copied");
            Assert.AreEqual(original.Timeout, copy.Timeout, "Timeout should be copied");
            Assert.AreEqual(original.Debug, copy.Debug, "Debug should be copied");
            Assert.AreEqual(original.Force, copy.Force, "Force should be copied");
            Assert.AreEqual(original.Flags.Count, copy.Flags.Count, "Flags count should be copied");
            Assert.AreEqual(original.Options.Count, copy.Options.Count, "Options count should be copied");
        }

        [TestMethod]
        public void CommandArguments_ToString_ShouldReturnValidStringRepresentation()
        {
            // Arrange
            var arguments = new CommandArguments();
            arguments.SetServer("localhost", 27000);
            arguments.SetFeature("test_feature");
            arguments.SetUser("testuser");
            arguments.SetTimeout(60);
            arguments.EnableDebug();
            arguments.AddFlag("verbose");
            arguments.AddArgument("custom", "value");

            // Act
            var result = arguments.ToString();

            // Assert
            Assert.IsNotNull(result, "ToString should not return null");
            Assert.IsTrue(result.Contains("CommandArguments:"), "Should include class name");
            Assert.IsTrue(result.Contains("localhost:27000"), "Should include server info");
            Assert.IsTrue(result.Contains("test_feature"), "Should include feature");
            Assert.IsTrue(result.Contains("testuser"), "Should include user");
            Assert.IsTrue(result.Contains("verbose"), "Should include flags");
            Assert.IsTrue(result.Contains("custom=value"), "Should include arguments");
        }

        [TestMethod]
        public void CommandArguments_MethodChaining_ShouldWorkCorrectly()
        {
            // Arrange
            var arguments = new CommandArguments();

            // Act
            var result = arguments.SetServer("localhost", 27000)
                                 .SetFeature("test_feature")
                                 .SetUser("testuser")
                                 .SetTimeout(60)
                                 .EnableDebug()
                                 .AddFlag("verbose")
                                 .AddArgument("custom", "value");

            // Assert
            Assert.AreSame(arguments, result, "Method chaining should return same instance");
            Assert.AreEqual("localhost", arguments.Server, "Server should be set");
            Assert.AreEqual("test_feature", arguments.Feature, "Feature should be set");
            Assert.AreEqual("testuser", arguments.Username, "User should be set");
            Assert.AreEqual(60, arguments.Timeout, "Timeout should be set");
            Assert.IsTrue(arguments.Debug, "Debug should be enabled");
            Assert.IsTrue(arguments.HasFlag("verbose"), "Flag should be added");
            Assert.IsTrue(arguments.HasArgument("custom"), "Argument should be added");
        }
    }
}