using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LicenseReleaseService.LicenseManagement.Models
{
    /// <summary>
    /// Represents detailed information about a specific license usage
    /// </summary>
    public class LicenseInfo
    {
        private string _userHost = string.Empty;
        private string _feature = string.Empty;
        private string _clientHost = string.Empty;
        private string _idleReason = string.Empty;
        private string _licenseVersion = string.Empty;
        private DateTime _borrowTime = DateTime.Now;
        private TimeSpan _usageDuration = TimeSpan.Zero;
        private LicenseStatus _status = LicenseStatus.Unknown;

        /// <summary>
        /// Gets or sets the host name of the user using the license
        /// </summary>
        [Required(ErrorMessage = "User host is required")]
        [StringLength(255, MinimumLength = 1, ErrorMessage = "User host must be between 1 and 255 characters")]
        [Description("Host name of the user using the license")]
        public string UserHost
        {
            get => _userHost;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("User host cannot be null or whitespace", nameof(value));
                if (value.Length > 255)
                    throw new ArgumentException("User host cannot exceed 255 characters", nameof(value));
                _userHost = value.Trim();
            }
        }

        /// <summary>
        /// Gets or sets the feature name associated with this license
        /// </summary>
        [Required(ErrorMessage = "Feature name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Feature name must be between 1 and 100 characters")]
        [Description("Feature name associated with this license")]
        public string Feature
        {
            get => _feature;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Feature name cannot be null or whitespace", nameof(value));
                if (value.Length > 100)
                    throw new ArgumentException("Feature name cannot exceed 100 characters", nameof(value));
                _feature = value.Trim();
            }
        }

        /// <summary>
        /// Gets or sets the time when the license was borrowed
        /// </summary>
        [Required(ErrorMessage = "Borrow time is required")]
        [Description("Time when the license was borrowed")]
        public DateTime BorrowTime
        {
            get => _borrowTime;
            set
            {
                if (value == default)
                    throw new ArgumentException("Borrow time cannot be default value", nameof(value));
                if (value > DateTime.Now.AddMinutes(5)) // Allow for small clock differences
                    throw new ArgumentException("Borrow time cannot be in the future", nameof(value));
                _borrowTime = value;
            }
        }

        /// <summary>
        /// Gets or sets the host name of the client machine
        /// </summary>
        [StringLength(255, ErrorMessage = "Client host cannot exceed 255 characters")]
        [Description("Host name of the client machine")]
        public string ClientHost
        {
            get => _clientHost;
            set
            {
                if (value != null && value.Length > 255)
                    throw new ArgumentException("Client host cannot exceed 255 characters", nameof(value));
                _clientHost = value?.Trim() ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the license is currently idle
        /// </summary>
        [Description("Indicates whether the license is currently idle")]
        public bool IsIdle { get; set; }

        /// <summary>
        /// Gets or sets the reason why the license is idle (if applicable)
        /// </summary>
        [StringLength(500, ErrorMessage = "Idle reason cannot exceed 500 characters")]
        [Description("Reason why the license is idle")]
        public string IdleReason
        {
            get => _idleReason;
            set
            {
                if (value != null && value.Length > 500)
                    throw new ArgumentException("Idle reason cannot exceed 500 characters", nameof(value));
                _idleReason = value?.Trim() ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets or sets the version of the license
        /// </summary>
        [StringLength(50, ErrorMessage = "License version cannot exceed 50 characters")]
        [Description("Version of the license")]
        public string LicenseVersion
        {
            get => _licenseVersion;
            set
            {
                if (value != null && value.Length > 50)
                    throw new ArgumentException("License version cannot exceed 50 characters", nameof(value));
                _licenseVersion = value?.Trim() ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets or sets the duration the license has been in use
        /// </summary>
        [Required(ErrorMessage = "Usage duration is required")]
        [Description("Duration the license has been in use")]
        public TimeSpan UsageDuration
        {
            get => _usageDuration;
            set
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentException("Usage duration cannot be negative", nameof(value));
                _usageDuration = value;
            }
        }

        /// <summary>
        /// Gets or sets the current status of the license
        /// </summary>
        [Required(ErrorMessage = "License status is required")]
        [Description("Current status of the license")]
        public LicenseStatus Status
        {
            get => _status;
            set => _status = value;
        }

        /// <summary>
        /// Gets a value indicating whether the license is currently active
        /// </summary>
        public bool IsActive => Status == LicenseStatus.Active;

        /// <summary>
        /// Gets a value indicating whether the license is currently borrowed
        /// </summary>
        public bool IsBorrowed => Status == LicenseStatus.Borrowed;

        /// <summary>
        /// Gets a value indicating whether the license has expired
        /// </summary>
        public bool IsExpired => Status == LicenseStatus.Expired;

        /// <summary>
        /// Gets the total time the license has been held (from borrow time to now)
        /// </summary>
        public TimeSpan TotalHoldTime => DateTime.Now - BorrowTime;

        /// <summary>
        /// Gets the percentage of time the license has been idle (if applicable)
        /// </summary>
        public double IdlePercentage => TotalHoldTime > TimeSpan.Zero ?
            (UsageDuration.TotalSeconds / TotalHoldTime.TotalSeconds) * 100 : 0;

        /// <summary>
        /// Initializes a new instance of the LicenseInfo class
        /// </summary>
        public LicenseInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the LicenseInfo class with required parameters
        /// </summary>
        /// <param name="userHost">Host name of the user</param>
        /// <param name="feature">Feature name</param>
        /// <param name="borrowTime">Time when borrowed</param>
        /// <param name="status">License status</param>
        public LicenseInfo(string userHost, string feature, DateTime borrowTime, LicenseStatus status)
        {
            UserHost = userHost;
            Feature = feature;
            BorrowTime = borrowTime;
            Status = status;
            UsageDuration = DateTime.Now - borrowTime;
        }

        /// <summary>
        /// Creates a license info for an active license
        /// </summary>
        /// <param name="userHost">Host name of the user</param>
        /// <param name="feature">Feature name</param>
        /// <param name="clientHost">Client host name</param>
        /// <param name="licenseVersion">License version</param>
        /// <returns>Active license info</returns>
        public static LicenseInfo CreateActive(string userHost, string feature, string clientHost = "", string licenseVersion = "")
        {
            return new LicenseInfo
            {
                UserHost = userHost,
                Feature = feature,
                ClientHost = clientHost,
                LicenseVersion = licenseVersion,
                BorrowTime = DateTime.Now,
                Status = LicenseStatus.Active,
                IsIdle = false,
                UsageDuration = TimeSpan.Zero
            };
        }

        /// <summary>
        /// Creates a license info for an idle license
        /// </summary>
        /// <param name="userHost">Host name of the user</param>
        /// <param name="feature">Feature name</param>
        /// <param name="idleReason">Reason for idle state</param>
        /// <param name="clientHost">Client host name</param>
        /// <returns>Idle license info</returns>
        public static LicenseInfo CreateIdle(string userHost, string feature, string idleReason, string clientHost = "")
        {
            return new LicenseInfo
            {
                UserHost = userHost,
                Feature = feature,
                ClientHost = clientHost,
                BorrowTime = DateTime.Now,
                Status = LicenseStatus.Idle,
                IsIdle = true,
                IdleReason = idleReason,
                UsageDuration = TimeSpan.Zero
            };
        }

        /// <summary>
        /// Creates a license info for a borrowed license
        /// </summary>
        /// <param name="userHost">Host name of the user</param>
        /// <param name="feature">Feature name</param>
        /// <param name="borrowTime">Time when borrowed</param>
        /// <param name="clientHost">Client host name</param>
        /// <returns>Borrowed license info</returns>
        public static LicenseInfo CreateBorrowed(string userHost, string feature, DateTime borrowTime, string clientHost = "")
        {
            return new LicenseInfo
            {
                UserHost = userHost,
                Feature = feature,
                ClientHost = clientHost,
                BorrowTime = borrowTime,
                Status = LicenseStatus.Borrowed,
                IsIdle = false,
                UsageDuration = DateTime.Now - borrowTime
            };
        }

        /// <summary>
        /// Updates the usage duration based on current time
        /// </summary>
        public void UpdateUsageDuration()
        {
            UsageDuration = DateTime.Now - BorrowTime;
        }

        /// <summary>
        /// Marks the license as idle with the specified reason
        /// </summary>
        /// <param name="reason">Reason for idle state</param>
        public void MarkAsIdle(string reason)
        {
            IsIdle = true;
            IdleReason = reason;
            Status = LicenseStatus.Idle;
        }

        /// <summary>
        /// Marks the license as active
        /// </summary>
        public void MarkAsActive()
        {
            IsIdle = false;
            IdleReason = string.Empty;
            Status = LicenseStatus.Active;
        }

        /// <summary>
        /// Validates the current license info
        /// </summary>
        /// <returns>List of validation errors</returns>
        public System.Collections.Generic.List<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(UserHost))
                errors.Add("User host is required");

            if (string.IsNullOrWhiteSpace(Feature))
                errors.Add("Feature name is required");

            if (BorrowTime == default)
                errors.Add("Borrow time is required");

            if (BorrowTime > DateTime.Now.AddMinutes(5))
                errors.Add("Borrow time cannot be in the future");

            if (UsageDuration < TimeSpan.Zero)
                errors.Add("Usage duration cannot be negative");

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the license info
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            var statusText = Status.ToString();
            if (IsIdle)
                statusText += " (Idle)";

            return $"LicenseInfo[User={UserHost}, Feature={Feature}, Status={statusText}, " +
                   $"Borrowed={BorrowTime:yyyy-MM-dd HH:mm:ss}, Duration={UsageDuration.TotalMinutes:F1}min, " +
                   $"Client={ClientHost}, Version={LicenseVersion}]";
        }
    }
}