using System;
using System.Collections.Generic;

namespace LicenseReleaseService.LicenseManagement.Models
{
    public class LicenseSessionValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Errors { get; set; }
        public Dictionary<string, object> ValidationData { get; set; }

        public LicenseSessionValidationResult()
        {
            IsValid = true;
            Message = string.Empty;
            Warnings = new List<string>();
            Errors = new List<string>();
            ValidationData = new Dictionary<string, object>();
        }

        public static LicenseSessionValidationResult Valid(string message = "Session is valid")
        {
            return new LicenseSessionValidationResult
            {
                IsValid = true,
                Message = message
            };
        }

        public static LicenseSessionValidationResult Invalid(string message, params string[] errors)
        {
            var result = new LicenseSessionValidationResult
            {
                IsValid = false,
                Message = message
            };

            if (errors != null)
            {
                result.Errors.AddRange(errors);
            }

            return result;
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                Warnings.Add(warning);
            }
        }

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
                IsValid = false;
            }
        }
    }
}