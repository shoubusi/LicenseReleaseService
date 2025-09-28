using System;

namespace LicenseReleaseService.LicenseManagement.Models
{
    public class LicenseFeatureUsage
    {
        public string Feature { get; set; }
        public string LicenseServer { get; set; }
        public int TotalLicenses { get; set; }
        public int UsedLicenses { get; set; }
        public int AvailableLicenses { get; set; }
        public double UsagePercentage { get; set; }
        public DateTime LastUpdated { get; set; }

        public LicenseFeatureUsage()
        {
            Feature = string.Empty;
            LicenseServer = string.Empty;
            TotalLicenses = 0;
            UsedLicenses = 0;
            AvailableLicenses = 0;
            UsagePercentage = 0.0;
            LastUpdated = DateTime.UtcNow;
        }

        public LicenseFeatureUsage(string feature, string licenseServer, int total, int used)
        {
            Feature = feature;
            LicenseServer = licenseServer;
            TotalLicenses = total;
            UsedLicenses = used;
            AvailableLicenses = total - used;
            UsagePercentage = total > 0 ? (double)used / total * 100 : 0.0;
            LastUpdated = DateTime.UtcNow;
        }
    }
}