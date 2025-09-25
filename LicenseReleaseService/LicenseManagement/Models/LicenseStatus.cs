using System.ComponentModel;

namespace LicenseReleaseService.LicenseManagement.Models
{
    /// <summary>
    /// Represents the current status of a license
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>
        /// License is currently in use by a user
        /// </summary>
        [Description("Active")]
        Active,

        /// <summary>
        /// License is checked out but not currently being used
        /// </summary>
        [Description("Idle")]
        Idle,

        /// <summary>
        /// License has been borrowed by a user for offline use
        /// </summary>
        [Description("Borrowed")]
        Borrowed,

        /// <summary>
        /// License has expired or is no longer valid
        /// </summary>
        [Description("Expired")]
        Expired,

        /// <summary>
        /// License status is unknown or cannot be determined
        /// </summary>
        [Description("Unknown")]
        Unknown
    }
}