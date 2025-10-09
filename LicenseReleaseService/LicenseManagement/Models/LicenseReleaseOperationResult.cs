using System;

namespace LicenseReleaseService.LicenseManagement.Models
{
    public class ReleasedLicenseInfo
    {
        public int SessionId { get; set; }
        public string UserName { get; set; }
        public string Feature { get; set; }
        public string Server { get; set; }
        public int Port { get; set; }
        public DateTime ReleasedAt { get; set; }
        public TimeSpan IdleTime { get; set; }
        public string Reason { get; set; }
    }

    public class LicenseReleaseOperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorCode { get; set; }
        public DateTime OperationTime { get; set; }
        public string LicenseServer { get; set; }
        public string Feature { get; set; }
        public string UserName { get; set; }
        public string ComputerName { get; set; }
        public Exception Exception { get; set; }
        public ReleasedLicenseInfo ReleasedLicense { get; set; }
        public string ErrorMessage { get; set; }

        public LicenseReleaseOperationResult()
        {
            OperationTime = DateTime.UtcNow;
            Success = false;
            Message = string.Empty;
            ErrorCode = string.Empty;
        }

        public static LicenseReleaseOperationResult Successful(string message = "License released successfully")
        {
            return new LicenseReleaseOperationResult
            {
                Success = true,
                Message = message
            };
        }

        public static LicenseReleaseOperationResult Failed(string message, string errorCode = null)
        {
            return new LicenseReleaseOperationResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode ?? "UNKNOWN_ERROR"
            };
        }

        public static LicenseReleaseOperationResult Failed(Exception ex, string errorCode = "EXCEPTION")
        {
            return new LicenseReleaseOperationResult
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = errorCode,
                Exception = ex
            };
        }
    }
}