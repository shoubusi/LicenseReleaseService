using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace LicenseReleaseService.LicenseManagement.Models
{
    /// <summary>
    /// Represents a license feature with detailed usage information
    /// </summary>
    public class LicenseFeature
    {
        private string _name = string.Empty;
        private Dictionary<string, LicenseInfo> _activeUsers = new Dictionary<string, LicenseInfo>();
        private Dictionary<string, LicenseInfo> _idleUsers = new Dictionary<string, LicenseInfo>();
        private Dictionary<string, LicenseInfo> _borrowedUsers = new Dictionary<string, LicenseInfo>();

        /// <summary>
        /// Gets or sets the name of the license feature
        /// </summary>
        [Required(ErrorMessage = "Feature name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Feature name must be between 1 and 100 characters")]
        [Description("Name of the license feature")]
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Feature name cannot be null or whitespace", nameof(value));
                if (value.Length > 100)
                    throw new ArgumentException("Feature name cannot exceed 100 characters", nameof(value));
                _name = value.Trim();
            }
        }

        /// <summary>
        /// Gets or sets the total number of licenses available for this feature
        /// </summary>
        [Required(ErrorMessage = "Total licenses is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Total licenses must be a non-negative number")]
        [Description("Total number of licenses available for this feature")]
        public int TotalLicenses { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses currently in use
        /// </summary>
        [Required(ErrorMessage = "Used licenses is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Used licenses must be a non-negative number")]
        [Description("Number of licenses currently in use")]
        public int UsedLicenses { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses currently available
        /// </summary>
        [Required(ErrorMessage = "Available licenses is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Available licenses must be a non-negative number")]
        [Description("Number of licenses currently available")]
        public int AvailableLicenses { get; set; }

        /// <summary>
        /// Gets the number of active users (thread-safe)
        /// </summary>
        [Description("Number of active users")]
        public int ActiveUsers
        {
            get
            {
                lock (_activeUsers)
                {
                    return _activeUsers.Count;
                }
            }
        }

        /// <summary>
        /// Gets the number of idle users (thread-safe)
        /// </summary>
        [Description("Number of idle users")]
        public int IdleUsers
        {
            get
            {
                lock (_idleUsers)
                {
                    return _idleUsers.Count;
                }
            }
        }

        /// <summary>
        /// Gets the number of borrowed users (thread-safe)
        /// </summary>
        [Description("Number of borrowed users")]
        public int BorrowedUsers
        {
            get
            {
                lock (_borrowedUsers)
                {
                    return _borrowedUsers.Count;
                }
            }
        }

        /// <summary>
        /// Gets or sets the last time this feature information was updated
        /// </summary>
        [Required(ErrorMessage = "Last updated time is required")]
        [Description("Last time this feature information was updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the version of this feature
        /// </summary>
        [StringLength(50, ErrorMessage = "Feature version cannot exceed 50 characters")]
        [Description("Version of this feature")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of this feature
        /// </summary>
        [StringLength(500, ErrorMessage = "Feature description cannot exceed 500 characters")]
        [Description("Description of this feature")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expiration date of this feature
        /// </summary>
        [Description("Expiration date of this feature")]
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the vendor of this feature
        /// </summary>
        [StringLength(100, ErrorMessage = "Vendor name cannot exceed 100 characters")]
        [Description("Vendor of this feature")]
        public string Vendor { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this feature is available
        /// </summary>
        [Description("Indicates whether this feature is available")]
        public bool IsAvailable => AvailableLicenses > 0 &&
                                  (!ExpirationDate.HasValue || ExpirationDate.Value > DateTime.Now);

        /// <summary>
        /// Gets a value indicating whether this feature has expired
        /// </summary>
        [Description("Indicates whether this feature has expired")]
        public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value <= DateTime.Now;

        /// <summary>
        /// Gets the utilization percentage for this feature
        /// </summary>
        [Description("Utilization percentage for this feature")]
        public double UtilizationPercentage => TotalLicenses > 0 ?
            (UsedLicenses * 100.0 / TotalLicenses) : 0;

        /// <summary>
        /// Gets the availability percentage for this feature
        /// </summary>
        [Description("Availability percentage for this feature")]
        public double AvailabilityPercentage => TotalLicenses > 0 ?
            (AvailableLicenses * 100.0 / TotalLicenses) : 0;

        /// <summary>
        /// Gets the idle users percentage for this feature
        /// </summary>
        [Description("Idle users percentage for this feature")]
        public double IdleUsersPercentage => TotalUsers > 0 ?
            (IdleUsers * 100.0 / TotalUsers) : 0;

        /// <summary>
        /// Gets the total number of users (active + idle + borrowed)
        /// </summary>
        [Description("Total number of users")]
        public int TotalUsers => ActiveUsers + IdleUsers + BorrowedUsers;

        /// <summary>
        /// Gets a read-only collection of active users (thread-safe)
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyCollection<LicenseInfo> ActiveUsersList
        {
            get
            {
                lock (_activeUsers)
                {
                    return new System.Collections.ObjectModel.ReadOnlyCollection<LicenseInfo>(_activeUsers.Values.ToList());
                }
            }
        }

        /// <summary>
        /// Gets a read-only collection of idle users (thread-safe)
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyCollection<LicenseInfo> IdleUsersList
        {
            get
            {
                lock (_idleUsers)
                {
                    return new System.Collections.ObjectModel.ReadOnlyCollection<LicenseInfo>(_idleUsers.Values.ToList());
                }
            }
        }

        /// <summary>
        /// Gets a read-only collection of borrowed users (thread-safe)
        /// </summary>
        public System.Collections.ObjectModel.ReadOnlyCollection<LicenseInfo> BorrowedUsersList
        {
            get
            {
                lock (_borrowedUsers)
                {
                    return new System.Collections.ObjectModel.ReadOnlyCollection<LicenseInfo>(_borrowedUsers.Values.ToList());
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the LicenseFeature class
        /// </summary>
        public LicenseFeature()
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseFeature class with required parameters
        /// </summary>
        /// <param name="name">Feature name</param>
        /// <param name="totalLicenses">Total licenses</param>
        /// <param name="usedLicenses">Used licenses</param>
        /// <param name="availableLicenses">Available licenses</param>
        public LicenseFeature(string name, int totalLicenses, int usedLicenses, int availableLicenses)
        {
            Name = name;
            TotalLicenses = totalLicenses;
            UsedLicenses = usedLicenses;
            AvailableLicenses = availableLicenses;
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Adds an active user to the feature (thread-safe)
        /// </summary>
        /// <param name="licenseInfo">License information for the user</param>
        /// <returns>True if the user was added, false if already exists</returns>
        public bool AddActiveUser(LicenseInfo licenseInfo)
        {
            if (licenseInfo == null)
                throw new ArgumentNullException(nameof(licenseInfo));

            var userKey = GetUserKey(licenseInfo);

            lock (_activeUsers)
            {
                if (_activeUsers.ContainsKey(userKey))
                    return false;

                licenseInfo.MarkAsActive();
                _activeUsers[userKey] = licenseInfo;
            }

            UpdateLastUpdated();
            return true;
        }

        /// <summary>
        /// Adds an idle user to the feature (thread-safe)
        /// </summary>
        /// <param name="licenseInfo">License information for the user</param>
        /// <returns>True if the user was added, false if already exists</returns>
        public bool AddIdleUser(LicenseInfo licenseInfo)
        {
            if (licenseInfo == null)
                throw new ArgumentNullException(nameof(licenseInfo));

            var userKey = GetUserKey(licenseInfo);

            lock (_idleUsers)
            {
                if (_idleUsers.ContainsKey(userKey))
                    return false;

                licenseInfo.MarkAsIdle("Idle license");
                _idleUsers[userKey] = licenseInfo;
            }

            UpdateLastUpdated();
            return true;
        }

        /// <summary>
        /// Adds a borrowed user to the feature (thread-safe)
        /// </summary>
        /// <param name="licenseInfo">License information for the user</param>
        /// <returns>True if the user was added, false if already exists</returns>
        public bool AddBorrowedUser(LicenseInfo licenseInfo)
        {
            if (licenseInfo == null)
                throw new ArgumentNullException(nameof(licenseInfo));

            var userKey = GetUserKey(licenseInfo);

            lock (_borrowedUsers)
            {
                if (_borrowedUsers.ContainsKey(userKey))
                    return false;

                licenseInfo.Status = LicenseStatus.Borrowed;
                _borrowedUsers[userKey] = licenseInfo;
            }

            UpdateLastUpdated();
            return true;
        }

        /// <summary>
        /// Removes a user from the feature (thread-safe)
        /// </summary>
        /// <param name="userHost">User host name</param>
        /// <param name="clientHost">Client host name</param>
        /// <returns>True if the user was removed, false if not found</returns>
        public bool RemoveUser(string userHost, string clientHost = "")
        {
            var userKey = GetUserKey(userHost, clientHost);
            bool removed = false;

            lock (_activeUsers)
            {
                removed |= _activeUsers.Remove(userKey);
            }

            lock (_idleUsers)
            {
                removed |= _idleUsers.Remove(userKey);
            }

            lock (_borrowedUsers)
            {
                removed |= _borrowedUsers.Remove(userKey);
            }

            if (removed)
            {
                UpdateLastUpdated();
            }

            return removed;
        }

        /// <summary>
        /// Moves a user from active to idle (thread-safe)
        /// </summary>
        /// <param name="userHost">User host name</param>
        /// <param name="clientHost">Client host name</param>
        /// <param name="reason">Reason for idle state</param>
        /// <returns>True if the user was moved, false if not found</returns>
        public bool MoveUserToIdle(string userHost, string clientHost = "", string reason = "Idle license")
        {
            var userKey = GetUserKey(userHost, clientHost);
            LicenseInfo? licenseInfo = null;

            lock (_activeUsers)
            {
                if (_activeUsers.TryGetValue(userKey, out licenseInfo))
                {
                    _activeUsers.Remove(userKey);
                }
            }

            if (licenseInfo != null)
            {
                licenseInfo.MarkAsIdle(reason);

                lock (_idleUsers)
                {
                    _idleUsers[userKey] = licenseInfo;
                }

                UpdateLastUpdated();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Moves a user from idle to active (thread-safe)
        /// </summary>
        /// <param name="userHost">User host name</param>
        /// <param name="clientHost">Client host name</param>
        /// <returns>True if the user was moved, false if not found</returns>
        public bool MoveUserToActive(string userHost, string clientHost = "")
        {
            var userKey = GetUserKey(userHost, clientHost);
            LicenseInfo? licenseInfo = null;

            lock (_idleUsers)
            {
                if (_idleUsers.TryGetValue(userKey, out licenseInfo))
                {
                    _idleUsers.Remove(userKey);
                }
            }

            if (licenseInfo != null)
            {
                licenseInfo.MarkAsActive();

                lock (_activeUsers)
                {
                    _activeUsers[userKey] = licenseInfo;
                }

                UpdateLastUpdated();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets all users (active, idle, and borrowed) in a thread-safe manner
        /// </summary>
        /// <returns>List of all users</returns>
        public List<LicenseInfo> GetAllUsers()
        {
            var allUsers = new List<LicenseInfo>();

            lock (_activeUsers)
            {
                allUsers.AddRange(_activeUsers.Values);
            }

            lock (_idleUsers)
            {
                allUsers.AddRange(_idleUsers.Values);
            }

            lock (_borrowedUsers)
            {
                allUsers.AddRange(_borrowedUsers.Values);
            }

            return allUsers;
        }

        /// <summary>
        /// Updates usage durations for all users
        /// </summary>
        public void UpdateUsageDurations()
        {
            var allUsers = GetAllUsers();
            foreach (var user in allUsers)
            {
                user.UpdateUsageDuration();
            }
            UpdateLastUpdated();
        }

        /// <summary>
        /// Clears all users from this feature (thread-safe)
        /// </summary>
        public void ClearAllUsers()
        {
            lock (_activeUsers)
            {
                _activeUsers.Clear();
            }

            lock (_idleUsers)
            {
                _idleUsers.Clear();
            }

            lock (_borrowedUsers)
            {
                _borrowedUsers.Clear();
            }

            UpdateLastUpdated();
        }

        /// <summary>
        /// Validates the current license feature
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Feature name is required");

            if (TotalLicenses < 0)
                errors.Add("Total licenses cannot be negative");

            if (UsedLicenses < 0)
                errors.Add("Used licenses cannot be negative");

            if (AvailableLicenses < 0)
                errors.Add("Available licenses cannot be negative");

            if (UsedLicenses + AvailableLicenses > TotalLicenses)
                errors.Add("Used licenses plus available licenses cannot exceed total licenses");

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the license feature
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            return $"LicenseFeature[Name={Name}, Total={TotalLicenses}, Used={UsedLicenses}, " +
                   $"Available={AvailableLicenses}, Active={ActiveUsers}, Idle={IdleUsers}, " +
                   $"Borrowed={BorrowedUsers}, Utilization={UtilizationPercentage:F1}%, " +
                   $"Updated={LastUpdated:yyyy-MM-dd HH:mm:ss}]";
        }

        /// <summary>
        /// Creates a unique key for a user based on host names
        /// </summary>
        /// <param name="userHost">User host name</param>
        /// <param name="clientHost">Client host name</param>
        /// <returns>Unique user key</returns>
        private static string GetUserKey(string userHost, string clientHost)
        {
            return $"{userHost}|{clientHost}";
        }

        /// <summary>
        /// Creates a unique key for a user from license info
        /// </summary>
        /// <param name="licenseInfo">License information</param>
        /// <returns>Unique user key</returns>
        private static string GetUserKey(LicenseInfo licenseInfo)
        {
            return GetUserKey(licenseInfo.UserHost, licenseInfo.ClientHost);
        }

        /// <summary>
        /// Updates the last updated timestamp
        /// </summary>
        private void UpdateLastUpdated()
        {
            LastUpdated = DateTime.Now;
        }
    }
}