using System;
using System.ComponentModel;
using Xunit;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.Tests.LicenseManagement.Models
{
    /// <summary>
    /// Unit tests for LicenseStatus enum
    /// </summary>
    public class LicenseStatusTests
    {
        [Fact]
        public void LicenseStatus_ShouldHaveCorrectValues()
        {
            // Arrange & Act
            var activeStatus = LicenseStatus.Active;
            var idleStatus = LicenseStatus.Idle;
            var borrowedStatus = LicenseStatus.Borrowed;
            var expiredStatus = LicenseStatus.Expired;
            var unknownStatus = LicenseStatus.Unknown;

            // Assert
            Assert.Equal(0, (int)activeStatus);
            Assert.Equal(1, (int)idleStatus);
            Assert.Equal(2, (int)borrowedStatus);
            Assert.Equal(3, (int)expiredStatus);
            Assert.Equal(4, (int)unknownStatus);
        }

        [Fact]
        public void LicenseStatus_ShouldHaveDescriptionAttributes()
        {
            // Arrange
            var activeType = typeof(LicenseStatus);
            var activeField = activeType.GetField("Active");
            var activeDescription = activeField?.GetCustomAttributes(typeof(DescriptionAttribute), false)[0] as DescriptionAttribute;

            var idleType = typeof(LicenseStatus);
            var idleField = idleType.GetField("Idle");
            var idleDescription = idleField?.GetCustomAttributes(typeof(DescriptionAttribute), false)[0] as DescriptionAttribute;

            var borrowedType = typeof(LicenseStatus);
            var borrowedField = borrowedType.GetField("Borrowed");
            var borrowedDescription = borrowedField?.GetCustomAttributes(typeof(DescriptionAttribute), false)[0] as DescriptionAttribute;

            var expiredType = typeof(LicenseStatus);
            var expiredField = expiredType.GetField("Expired");
            var expiredDescription = expiredField?.GetCustomAttributes(typeof(DescriptionAttribute), false)[0] as DescriptionAttribute;

            var unknownType = typeof(LicenseStatus);
            var unknownField = unknownType.GetField("Unknown");
            var unknownDescription = unknownField?.GetCustomAttributes(typeof(DescriptionAttribute), false)[0] as DescriptionAttribute;

            // Assert
            Assert.NotNull(activeDescription);
            Assert.Equal("Active", activeDescription.Description);

            Assert.NotNull(idleDescription);
            Assert.Equal("Idle", idleDescription.Description);

            Assert.NotNull(borrowedDescription);
            Assert.Equal("Borrowed", borrowedDescription.Description);

            Assert.NotNull(expiredDescription);
            Assert.Equal("Expired", expiredDescription.Description);

            Assert.NotNull(unknownDescription);
            Assert.Equal("Unknown", unknownDescription.Description);
        }

        [Fact]
        public void LicenseStatus_ToString_ShouldReturnCorrectValues()
        {
            // Arrange & Act
            var activeString = LicenseStatus.Active.ToString();
            var idleString = LicenseStatus.Idle.ToString();
            var borrowedString = LicenseStatus.Borrowed.ToString();
            var expiredString = LicenseStatus.Expired.ToString();
            var unknownString = LicenseStatus.Unknown.ToString();

            // Assert
            Assert.Equal("Active", activeString);
            Assert.Equal("Idle", idleString);
            Assert.Equal("Borrowed", borrowedString);
            Assert.Equal("Expired", expiredString);
            Assert.Equal("Unknown", unknownString);
        }

        [Theory]
        [InlineData(LicenseStatus.Active, true)]
        [InlineData(LicenseStatus.Idle, false)]
        [InlineData(LicenseStatus.Borrowed, false)]
        [InlineData(LicenseStatus.Expired, false)]
        [InlineData(LicenseStatus.Unknown, false)]
        public void LicenseStatus_Comparison_ShouldWorkCorrectly(LicenseStatus status, bool expectedActive)
        {
            // Arrange & Act
            var isActive = status == LicenseStatus.Active;
            var isIdle = status == LicenseStatus.Idle;
            var isBorrowed = status == LicenseStatus.Borrowed;
            var isExpired = status == LicenseStatus.Expired;
            var isUnknown = status == LicenseStatus.Unknown;

            // Assert
            Assert.Equal(expectedActive, isActive);
            Assert.Equal(status == LicenseStatus.Idle, isIdle);
            Assert.Equal(status == LicenseStatus.Borrowed, isBorrowed);
            Assert.Equal(status == LicenseStatus.Expired, isExpired);
            Assert.Equal(status == LicenseStatus.Unknown, isUnknown);
        }

        [Fact]
        public void LicenseStatus_ParseFromString_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var activeParsed = Enum.Parse<LicenseStatus>("Active");
            var idleParsed = Enum.Parse<LicenseStatus>("Idle");
            var borrowedParsed = Enum.Parse<LicenseStatus>("Borrowed");
            var expiredParsed = Enum.Parse<LicenseStatus>("Expired");
            var unknownParsed = Enum.Parse<LicenseStatus>("Unknown");

            // Assert
            Assert.Equal(LicenseStatus.Active, activeParsed);
            Assert.Equal(LicenseStatus.Idle, idleParsed);
            Assert.Equal(LicenseStatus.Borrowed, borrowedParsed);
            Assert.Equal(LicenseStatus.Expired, expiredParsed);
            Assert.Equal(LicenseStatus.Unknown, unknownParsed);
        }

        [Fact]
        public void LicenseStatus_TryParseFromString_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var successActive = Enum.TryParse("Active", out LicenseStatus activeResult);
            var successIdle = Enum.TryParse("Idle", out LicenseStatus idleResult);
            var successBorrowed = Enum.TryParse("Borrowed", out LicenseStatus borrowedResult);
            var successExpired = Enum.TryParse("Expired", out LicenseStatus expiredResult);
            var successUnknown = Enum.TryParse("Unknown", out LicenseStatus unknownResult);
            var successInvalid = Enum.TryParse("Invalid", out LicenseStatus invalidResult);

            // Assert
            Assert.True(successActive);
            Assert.Equal(LicenseStatus.Active, activeResult);

            Assert.True(successIdle);
            Assert.Equal(LicenseStatus.Idle, idleResult);

            Assert.True(successBorrowed);
            Assert.Equal(LicenseStatus.Borrowed, borrowedResult);

            Assert.True(successExpired);
            Assert.Equal(LicenseStatus.Expired, expiredResult);

            Assert.True(successUnknown);
            Assert.Equal(LicenseStatus.Unknown, unknownResult);

            Assert.False(successInvalid);
            Assert.Equal(default, invalidResult);
        }
    }
}