using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.Tests.LicenseManagement.Models
{
    /// <summary>
    /// Unit tests for LicenseFeature class
    /// </summary>
    public class LicenseFeatureTests
    {
        [Fact]
        public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var feature = new LicenseFeature();

            // Assert
            Assert.NotEmpty(feature.Name);
            Assert.Equal(0, feature.TotalLicenses);
            Assert.Equal(0, feature.UsedLicenses);
            Assert.Equal(0, feature.AvailableLicenses);
            Assert.Equal(0, feature.ActiveUsers);
            Assert.Equal(0, feature.IdleUsers);
            Assert.Equal(0, feature.BorrowedUsers);
            Assert.Equal(0, feature.TotalUsers);
            Assert.NotEqual(default, feature.LastUpdated);
            Assert.Empty(feature.Version);
            Assert.Empty(feature.Description);
            Assert.Empty(feature.Vendor);
            Assert.False(feature.IsExpired);
            Assert.Equal(0, feature.UtilizationPercentage);
            Assert.Equal(0, feature.AvailabilityPercentage);
            Assert.Equal(0, feature.IdleUsersPercentage);
        }

        [Fact]
        public void Constructor_WithParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var name = "solidworks-premium";
            var totalLicenses = 10;
            var usedLicenses = 3;
            var availableLicenses = 7;

            // Act
            var feature = new LicenseFeature(name, totalLicenses, usedLicenses, availableLicenses);

            // Assert
            Assert.Equal(name, feature.Name);
            Assert.Equal(totalLicenses, feature.TotalLicenses);
            Assert.Equal(usedLicenses, feature.UsedLicenses);
            Assert.Equal(availableLicenses, feature.AvailableLicenses);
            Assert.Equal(usedLicenses + availableLicenses, feature.TotalLicenses);
            Assert.True(feature.IsAvailable);
            Assert.Equal(30.0, feature.UtilizationPercentage);
            Assert.Equal(70.0, feature.AvailabilityPercentage);
        }

        [Fact]
        public void Name_WithValidValue_ShouldSetCorrectly()
        {
            // Arrange
            var feature = new LicenseFeature();
            var validName = "solidworks-standard";

            // Act
            feature.Name = validName;

            // Assert
            Assert.Equal(validName, feature.Name);
        }

        [Fact]
        public void Name_WithNullValue_ShouldThrowException()
        {
            // Arrange
            var feature = new LicenseFeature();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => feature.Name = null);
        }

        [Fact]
        public void Name_WithLongValue_ShouldThrowException()
        {
            // Arrange
            var feature = new LicenseFeature();
            var longName = new string('a', 101);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => feature.Name = longName);
        }

        [Fact]
        public void TotalLicenses_WithNegativeValue_ShouldThrowException()
        {
            // Arrange
            var feature = new LicenseFeature();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => feature.TotalLicenses = -1);
        }

        [Fact]
        public void UsedLicenses_WithNegativeValue_ShouldThrowException()
        {
            // Arrange
            var feature = new LicenseFeature();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => feature.UsedLicenses = -1);
        }

        [Fact]
        public void AvailableLicenses_WithNegativeValue_ShouldThrowException()
        {
            // Arrange
            var feature = new LicenseFeature();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => feature.AvailableLicenses = -1);
        }

        [Fact]
        public void IsAvailable_WithAvailableLicenses_ShouldReturnTrue()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 3, 7);

            // Act & Assert
            Assert.True(feature.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithNoAvailableLicenses_ShouldReturnFalse()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 10, 0);

            // Act & Assert
            Assert.False(feature.IsAvailable);
        }

        [Fact]
        public void IsAvailable_WithExpiredFeature_ShouldReturnFalse()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 3, 7);
            feature.ExpirationDate = DateTime.Now.AddDays(-1);

            // Act & Assert
            Assert.False(feature.IsAvailable);
            Assert.True(feature.IsExpired);
        }

        [Fact]
        public void UtilizationPercentage_WithNoLicenses_ShouldReturnZero()
        {
            // Arrange
            var feature = new LicenseFeature("test", 0, 0, 0);

            // Act & Assert
            Assert.Equal(0, feature.UtilizationPercentage);
        }

        [Fact]
        public void UtilizationPercentage_WithLicensesInUse_ShouldCalculateCorrectly()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 3, 7);

            // Act & Assert
            Assert.Equal(30.0, feature.UtilizationPercentage);
        }

        [Fact]
        public void AvailabilityPercentage_WithNoLicenses_ShouldReturnZero()
        {
            // Arrange
            var feature = new LicenseFeature("test", 0, 0, 0);

            // Act & Assert
            Assert.Equal(0, feature.AvailabilityPercentage);
        }

        [Fact]
        public void AvailabilityPercentage_WithAvailableLicenses_ShouldCalculateCorrectly()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 3, 7);

            // Act & Assert
            Assert.Equal(70.0, feature.AvailabilityPercentage);
        }

        [Fact]
        public void AddActiveUser_WithValidUser_ShouldAddUser()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var licenseInfo = LicenseInfo.CreateActive("user1", "test");

            // Act
            var result = feature.AddActiveUser(licenseInfo);

            // Assert
            Assert.True(result);
            Assert.Equal(1, feature.ActiveUsers);
            Assert.Equal(0, feature.IdleUsers);
            Assert.Equal(0, feature.BorrowedUsers);
            Assert.Equal(1, feature.TotalUsers);
        }

        [Fact]
        public void AddActiveUser_WithNullUser_ShouldThrowException()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => feature.AddActiveUser(null));
        }

        [Fact]
        public void AddActiveUser_WithDuplicateUser_ShouldReturnFalse()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var licenseInfo = LicenseInfo.CreateActive("user1", "test");
            feature.AddActiveUser(licenseInfo);

            // Act
            var result = feature.AddActiveUser(licenseInfo);

            // Assert
            Assert.False(result);
            Assert.Equal(1, feature.ActiveUsers);
        }

        [Fact]
        public void AddIdleUser_WithValidUser_ShouldAddUser()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var licenseInfo = LicenseInfo.CreateIdle("user1", "test", "Idle reason");

            // Act
            var result = feature.AddIdleUser(licenseInfo);

            // Assert
            Assert.True(result);
            Assert.Equal(0, feature.ActiveUsers);
            Assert.Equal(1, feature.IdleUsers);
            Assert.Equal(0, feature.BorrowedUsers);
            Assert.Equal(1, feature.TotalUsers);
        }

        [Fact]
        public void AddBorrowedUser_WithValidUser_ShouldAddUser()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var licenseInfo = LicenseInfo.CreateBorrowed("user1", "test", DateTime.Now.AddHours(-1));

            // Act
            var result = feature.AddBorrowedUser(licenseInfo);

            // Assert
            Assert.True(result);
            Assert.Equal(0, feature.ActiveUsers);
            Assert.Equal(0, feature.IdleUsers);
            Assert.Equal(1, feature.BorrowedUsers);
            Assert.Equal(1, feature.TotalUsers);
        }

        [Fact]
        public void RemoveUser_WithExistingUser_ShouldRemoveUser()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var licenseInfo = LicenseInfo.CreateActive("user1", "test");
            feature.AddActiveUser(licenseInfo);

            // Act
            var result = feature.RemoveUser("user1");

            // Assert
            Assert.True(result);
            Assert.Equal(0, feature.ActiveUsers);
            Assert.Equal(0, feature.TotalUsers);
        }

        [Fact]
        public void RemoveUser_WithNonExistingUser_ShouldReturnFalse()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);

            // Act
            var result = feature.RemoveUser("nonexistent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void MoveUserToIdle_WithActiveUser_ShouldMoveUser()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var licenseInfo = LicenseInfo.CreateActive("user1", "test");
            feature.AddActiveUser(licenseInfo);

            // Act
            var result = feature.MoveUserToIdle("user1", "", "Testing idle");

            // Assert
            Assert.True(result);
            Assert.Equal(0, feature.ActiveUsers);
            Assert.Equal(1, feature.IdleUsers);
            Assert.Equal(0, feature.BorrowedUsers);
        }

        [Fact]
        public void MoveUserToActive_WithIdleUser_ShouldMoveUser()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var licenseInfo = LicenseInfo.CreateIdle("user1", "test", "Testing");
            feature.AddIdleUser(licenseInfo);

            // Act
            var result = feature.MoveUserToActive("user1");

            // Assert
            Assert.True(result);
            Assert.Equal(1, feature.ActiveUsers);
            Assert.Equal(0, feature.IdleUsers);
            Assert.Equal(0, feature.BorrowedUsers);
        }

        [Fact]
        public void GetAllUsers_ShouldReturnAllUsers()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var activeUser = LicenseInfo.CreateActive("user1", "test");
            var idleUser = LicenseInfo.CreateIdle("user2", "test", "Idle");
            var borrowedUser = LicenseInfo.CreateBorrowed("user3", "test", DateTime.Now.AddHours(-1));

            feature.AddActiveUser(activeUser);
            feature.AddIdleUser(idleUser);
            feature.AddBorrowedUser(borrowedUser);

            // Act
            var allUsers = feature.GetAllUsers();

            // Assert
            Assert.Equal(3, allUsers.Count);
            Assert.Contains(allUsers, u => u.UserHost == "user1" && u.Status == LicenseStatus.Active);
            Assert.Contains(allUsers, u => u.UserHost == "user2" && u.Status == LicenseStatus.Idle);
            Assert.Contains(allUsers, u => u.UserHost == "user3" && u.Status == LicenseStatus.Borrowed);
        }

        [Fact]
        public void ActiveUsersList_ShouldReturnReadOnlyCollection()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var activeUser = LicenseInfo.CreateActive("user1", "test");
            feature.AddActiveUser(activeUser);

            // Act
            var activeUsersList = feature.ActiveUsersList;

            // Assert
            Assert.Single(activeUsersList);
            Assert.Equal("user1", activeUsersList[0].UserHost);
            Assert.True(activeUsersList.IsReadOnly);
        }

        [Fact]
        public void IdleUsersList_ShouldReturnReadOnlyCollection()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var idleUser = LicenseInfo.CreateIdle("user1", "test", "Idle");
            feature.AddIdleUser(idleUser);

            // Act
            var idleUsersList = feature.IdleUsersList;

            // Assert
            Assert.Single(idleUsersList);
            Assert.Equal("user1", idleUsersList[0].UserHost);
            Assert.True(idleUsersList.IsReadOnly);
        }

        [Fact]
        public void BorrowedUsersList_ShouldReturnReadOnlyCollection()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var borrowedUser = LicenseInfo.CreateBorrowed("user1", "test", DateTime.Now.AddHours(-1));
            feature.AddBorrowedUser(borrowedUser);

            // Act
            var borrowedUsersList = feature.BorrowedUsersList;

            // Assert
            Assert.Single(borrowedUsersList);
            Assert.Equal("user1", borrowedUsersList[0].UserHost);
            Assert.True(borrowedUsersList.IsReadOnly);
        }

        [Fact]
        public void UpdateUsageDurations_ShouldUpdateAllUsers()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            var borrowTime = DateTime.Now.AddMinutes(-5);
            var activeUser = LicenseInfo.CreateActive("user1", "test");
            var idleUser = LicenseInfo.CreateIdle("user2", "test", "Idle");
            var borrowedUser = LicenseInfo.CreateBorrowed("user3", "test", borrowTime);

            // Set initial duration to zero
            activeUser.UsageDuration = TimeSpan.Zero;
            idleUser.UsageDuration = TimeSpan.Zero;
            borrowedUser.UsageDuration = TimeSpan.Zero;

            feature.AddActiveUser(activeUser);
            feature.AddIdleUser(idleUser);
            feature.AddBorrowedUser(borrowedUser);

            // Act
            feature.UpdateUsageDurations();

            // Assert
            var allUsers = feature.GetAllUsers();
            foreach (var user in allUsers)
            {
                Assert.True(user.UsageDuration.TotalSeconds > 0);
            }
        }

        [Fact]
        public void ClearAllUsers_ShouldRemoveAllUsers()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            feature.AddActiveUser(LicenseInfo.CreateActive("user1", "test"));
            feature.AddIdleUser(LicenseInfo.CreateIdle("user2", "test", "Idle"));
            feature.AddBorrowedUser(LicenseInfo.CreateBorrowed("user3", "test", DateTime.Now.AddHours(-1)));

            // Act
            feature.ClearAllUsers();

            // Assert
            Assert.Equal(0, feature.ActiveUsers);
            Assert.Equal(0, feature.IdleUsers);
            Assert.Equal(0, feature.BorrowedUsers);
            Assert.Equal(0, feature.TotalUsers);
        }

        [Fact]
        public void Validate_WithValidFeature_ShouldReturnEmptyList()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 3, 7);

            // Act
            var errors = feature.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_WithInvalidFeature_ShouldReturnErrors()
        {
            // Arrange
            var feature = new LicenseFeature();
            feature.Name = ""; // Invalid
            feature.TotalLicenses = -1; // Invalid
            feature.UsedLicenses = -1; // Invalid
            feature.AvailableLicenses = -1; // Invalid
            feature.UsedLicenses = 15; // More than total
            feature.AvailableLicenses = 10; // More than total

            // Act
            var errors = feature.Validate();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("Feature name is required", errors);
            Assert.Contains("Total licenses cannot be negative", errors);
            Assert.Contains("Used licenses cannot be negative", errors);
            Assert.Contains("Available licenses cannot be negative", errors);
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulStringRepresentation()
        {
            // Arrange
            var feature = new LicenseFeature("solidworks-premium", 10, 3, 7);
            feature.AddActiveUser(LicenseInfo.CreateActive("user1", "solidworks-premium"));
            feature.AddIdleUser(LicenseInfo.CreateIdle("user2", "solidworks-premium", "Idle"));

            // Act
            var result = feature.ToString();

            // Assert
            Assert.Contains("solidworks-premium", result);
            Assert.Contains("Total=10", result);
            Assert.Contains("Used=3", result);
            Assert.Contains("Available=7", result);
            Assert.Contains("Active=1", result);
            Assert.Contains("Idle=1", result);
            Assert.Contains("Borrowed=0", result);
            Assert.Contains("30.0%", result); // Utilization percentage
        }

        [Fact]
        public void ThreadSafety_AddUsersConcurrently_ShouldHandleCorrectly()
        {
            // Arrange
            var feature = new LicenseFeature("test", 100, 0, 100);
            var tasks = new List<Task>();
            const int numberOfTasks = 10;
            const int usersPerTask = 10;

            // Act
            for (int i = 0; i < numberOfTasks; i++)
            {
                var taskId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < usersPerTask; j++)
                    {
                        var userHost = $"user-{taskId}-{j}";
                        var licenseInfo = LicenseInfo.CreateActive(userHost, "test");
                        feature.AddActiveUser(licenseInfo);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(numberOfTasks * usersPerTask, feature.ActiveUsers);
            Assert.Equal(numberOfTasks * usersPerTask, feature.TotalUsers);
        }

        [Fact]
        public void ThreadSafety_MoveUsersConcurrently_ShouldHandleCorrectly()
        {
            // Arrange
            var feature = new LicenseFeature("test", 100, 0, 100);
            var tasks = new List<Task>();
            const int numberOfUsers = 20;

            // Add users first
            for (int i = 0; i < numberOfUsers; i++)
            {
                var userHost = $"user-{i}";
                var licenseInfo = LicenseInfo.CreateActive(userHost, "test");
                feature.AddActiveUser(licenseInfo);
            }

            // Act - move users to idle concurrently
            for (int i = 0; i < numberOfUsers; i++)
            {
                var userHost = $"user-{i}";
                tasks.Add(Task.Run(() => feature.MoveUserToIdle(userHost)));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(0, feature.ActiveUsers);
            Assert.Equal(numberOfUsers, feature.IdleUsers);
            Assert.Equal(numberOfUsers, feature.TotalUsers);
        }

        [Fact]
        public void IdleUsersPercentage_WithNoUsers_ShouldReturnZero()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);

            // Act & Assert
            Assert.Equal(0, feature.IdleUsersPercentage);
        }

        [Fact]
        public void IdleUsersPercentage_WithMixedUsers_ShouldCalculateCorrectly()
        {
            // Arrange
            var feature = new LicenseFeature("test", 10, 0, 10);
            feature.AddActiveUser(LicenseInfo.CreateActive("user1", "test"));
            feature.AddActiveUser(LicenseInfo.CreateActive("user2", "test"));
            feature.AddIdleUser(LicenseInfo.CreateIdle("user3", "test", "Idle"));
            feature.AddIdleUser(LicenseInfo.CreateIdle("user4", "test", "Idle"));

            // Act & Assert
            Assert.Equal(50.0, feature.IdleUsersPercentage); // 2 idle out of 4 total users
        }
    }
}