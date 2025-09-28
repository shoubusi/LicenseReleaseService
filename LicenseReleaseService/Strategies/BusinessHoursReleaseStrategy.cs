using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.Interfaces;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.Models;
using Microsoft.Extensions.Logging;

namespace LicenseReleaseService.Strategies
{
    /// <summary>
    /// License release strategy that releases licenses during specific business hours
    /// </summary>
    public class BusinessHoursReleaseStrategy : ILicenseReleaseStrategy
    {
        private readonly ILogger<BusinessHoursReleaseStrategy> _logger;
        private readonly Lazy<ILicenseManager> _licenseManager;
        private readonly BusinessHoursReleaseStrategyConfig _config;

        /// <summary>
        /// Gets the name of the strategy
        /// </summary>
        public string Name => "BusinessHoursReleaseStrategy";

        /// <summary>
        /// Gets the description of the strategy
        /// </summary>
        public string Description => "Releases licenses during configured business hours and days";

        /// <summary>
        /// Gets the priority of the strategy (higher values = higher priority)
        /// </summary>
        public int Priority => 5;

        /// <summary>
        /// Initializes a new instance of the BusinessHoursReleaseStrategy class
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="licenseManager">The license manager (lazy-loaded to prevent circular dependencies)</param>
        /// <param name="config">The strategy configuration</param>
        public BusinessHoursReleaseStrategy(
            ILogger<BusinessHoursReleaseStrategy> logger,
            Lazy<ILicenseManager> licenseManager,
            BusinessHoursReleaseStrategyConfig config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _licenseManager = licenseManager ?? throw new ArgumentNullException(nameof(licenseManager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Determines whether this strategy can handle the given release request
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>True if this strategy can handle the request</returns>
        public bool CanHandle(ReleaseRequest request)
        {
            if (request == null)
                return false;

            // Check if strategy is enabled
            if (!_config.IsEnabled)
                return false;

            // Check if the request specifies this strategy or allows automatic strategy selection
            if (!string.IsNullOrEmpty(request.Strategy) &&
                !request.Strategy.Equals(Name, StringComparison.OrdinalIgnoreCase))
                return false;

            // Check if current time is within business hours
            if (!IsWithinBusinessHours(DateTime.Now))
            {
                _logger.LogDebug("Cannot handle request {RequestId}: Current time is outside business hours", request.RequestId);
                return false;
            }

            _logger.LogDebug("Strategy {StrategyName} can handle request {RequestId} during business hours",
                Name, request.RequestId);

            return true;
        }

        /// <summary>
        /// Executes the license release strategy
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>License release result</returns>
        public async Task<LicenseReleaseResult> ExecuteAsync(ReleaseRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogInformation("Executing {StrategyName} for request {RequestId}, user {User}, feature {Feature}",
                Name, request.RequestId, request.User, request.Feature);

            var startTime = DateTime.UtcNow;
            var currentTime = DateTime.Now;

            try
            {
                // Double-check business hours before execution
                if (!IsWithinBusinessHours(currentTime))
                {
                    _logger.LogWarning("Cannot execute {StrategyName}: Current time {CurrentTime} is outside business hours",
                        Name, currentTime);
                    return LicenseReleaseResult.CreateFailure(
                        request.Server, request.Port, request.Feature, request.User,
                        $"Current time {currentTime:yyyy-MM-dd HH:mm:ss} is outside business hours",
                        LicenseReleaseResultCode.PermissionDenied,
                        DateTime.UtcNow - startTime);
                }

                // Check if we're in a grace period
                if (IsInGracePeriod(currentTime))
                {
                    _logger.LogDebug("Request {RequestId} is within grace period, allowing execution", request.RequestId);
                }
                else
                {
                    // Check if it's too close to business hours end
                    if (IsTooCloseToBusinessHoursEnd(currentTime))
                    {
                        _logger.LogWarning("Cannot execute {StrategyName}: Too close to business hours end", Name);
                        return LicenseReleaseResult.CreateFailure(
                            request.Server, request.Port, request.Feature, request.User,
                            "Too close to business hours end for safe license release",
                            LicenseReleaseResultCode.PermissionDenied,
                            DateTime.UtcNow - startTime);
                    }
                }

                // Perform business hours specific validation
                var businessValidation = await PerformBusinessHoursValidationAsync(request, cancellationToken);
                if (!businessValidation.IsValid)
                {
                    _logger.LogWarning("Business hours validation failed for request {RequestId}: {Reason}",
                        request.RequestId, businessValidation.ErrorMessage);
                    return LicenseReleaseResult.CreateFailure(
                        request.Server, request.Port, request.Feature, request.User,
                        businessValidation.ErrorMessage,
                        LicenseReleaseResultCode.PermissionDenied,
                        DateTime.UtcNow - startTime);
                }

                // Execute license release
                _logger.LogInformation("Releasing license for user {User} during business hours", request.User);

                var releaseResult = await _licenseManager.Value.ReleaseLicenseAsync(
                    request.Server, request.Port, request.Feature, request.User, cancellationToken);

                if (releaseResult.Success)
                {
                    _logger.LogInformation("Successfully released license for user {User} during business hours", request.User);
                }
                else
                {
                    _logger.LogError("Failed to release license for user {User}: {Error}",
                        request.User, releaseResult.ErrorMessage);
                }

                // Add strategy-specific metadata
                releaseResult.Metadata = new
                {
                    Strategy = Name,
                    BusinessHours = GetBusinessHoursInfo(),
                    CurrentTime = currentTime,
                    IsWithinGracePeriod = IsInGracePeriod(currentTime),
                    TimeToBusinessHoursEnd = GetTimeToBusinessHoursEnd(currentTime),
                    DayOfWeek = currentTime.DayOfWeek,
                    IsWeekend = IsWeekend(currentTime)
                };

                return releaseResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing {StrategyName} for request {RequestId}", Name, request.RequestId);
                return LicenseReleaseResult.CreateFailure(
                    request.Server, request.Port, request.Feature, request.User,
                    $"Strategy execution failed: {ex.Message}",
                    LicenseReleaseResultCode.UnknownError,
                    DateTime.UtcNow - startTime);
            }
        }

        /// <summary>
        /// Validates the release request before execution
        /// </summary>
        /// <param name="request">The license release request</param>
        /// <returns>Validation result indicating whether the request is valid</returns>
        public StrategyValidationResult Validate(ReleaseRequest request)
        {
            if (request == null)
                return StrategyValidationResult.Failure("Request cannot be null");

            if (!_config.IsEnabled)
                return StrategyValidationResult.Failure("Business hours release strategy is disabled");

            if (_config.BusinessHours.Count == 0)
                return StrategyValidationResult.Failure("No business hours configured");

            if (_config.WorkingDays.Count == 0)
                return StrategyValidationResult.Failure("No working days configured");

            if (_config.GracePeriodMinutes < 0)
                return StrategyValidationResult.Failure("Grace period cannot be negative");

            if (_config.MinimumTimeBeforeEndMinutes < 0)
                return StrategyValidationResult.Failure("Minimum time before end cannot be negative");

            return StrategyValidationResult.Success();
        }

        /// <summary>
        /// Gets the health status of the strategy
        /// </summary>
        /// <returns>Health status information</returns>
        public StrategyHealthStatus GetHealthStatus()
        {
            try
            {
                // Check configuration
                if (!_config.IsEnabled)
                {
                    return StrategyHealthStatus.Unhealthy("Strategy is disabled");
                }

                // Check if business hours are configured
                if (_config.BusinessHours.Count == 0)
                {
                    return StrategyHealthStatus.Unhealthy("No business hours configured");
                }

                // Check if working days are configured
                if (_config.WorkingDays.Count == 0)
                {
                    return StrategyHealthStatus.Unhealthy("No working days configured");
                }

                // Check if license manager is available
                if (_licenseManager == null)
                {
                    return StrategyHealthStatus.Unhealthy("License manager is not available");
                }

                return StrategyHealthStatus.Healthy("Strategy is healthy and ready to process requests");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health status for {StrategyName}", Name);
                return StrategyHealthStatus.Unhealthy($"Health check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the current time is within business hours
        /// </summary>
        /// <param name="currentTime">The current time</param>
        /// <returns>True if within business hours</returns>
        private bool IsWithinBusinessHours(DateTime currentTime)
        {
            // Check if it's a working day
            if (!_config.WorkingDays.Contains(currentTime.DayOfWeek))
            {
                _logger.LogDebug("Current day {Day} is not a working day", currentTime.DayOfWeek);
                return false;
            }

            // Check if it's within business hours
            var currentTimeOfDay = currentTime.TimeOfDay;
            var isInBusinessHours = _config.BusinessHours.Any(hours =>
                currentTimeOfDay >= hours.Start && currentTimeOfDay <= hours.End);

            _logger.LogDebug("Current time {Time} is {Within} business hours on {Day}",
                currentTimeOfDay, isInBusinessHours ? "within" : "outside", currentTime.DayOfWeek);

            return isInBusinessHours;
        }

        /// <summary>
        /// Checks if the current time is within the grace period
        /// </summary>
        /// <param name="currentTime">The current time</param>
        /// <returns>True if within grace period</returns>
        private bool IsInGracePeriod(DateTime currentTime)
        {
            if (_config.GracePeriodMinutes <= 0)
                return false;

            // Check if we're in a grace period at the start of business hours
            var currentTimeOfDay = currentTime.TimeOfDay;
            return _config.BusinessHours.Any(hours =>
            {
                var graceStart = hours.Start.Subtract(TimeSpan.FromMinutes(_config.GracePeriodMinutes));
                return currentTimeOfDay >= graceStart && currentTimeOfDay <= hours.Start;
            });
        }

        /// <summary>
        /// Checks if it's too close to business hours end
        /// </summary>
        /// <param name="currentTime">The current time</param>
        /// <returns>True if too close to end</returns>
        private bool IsTooCloseToBusinessHoursEnd(DateTime currentTime)
        {
            if (_config.MinimumTimeBeforeEndMinutes <= 0)
                return false;

            var currentTimeOfDay = currentTime.TimeOfDay;
            return _config.BusinessHours.Any(hours =>
            {
                var warningTime = hours.End.Subtract(TimeSpan.FromMinutes(_config.MinimumTimeBeforeEndMinutes));
                return currentTimeOfDay >= warningTime && currentTimeOfDay <= hours.End;
            });
        }

        /// <summary>
        /// Gets the time remaining until business hours end
        /// </summary>
        /// <param name="currentTime">The current time</param>
        /// <returns>Time remaining until business hours end</returns>
        private TimeSpan GetTimeToBusinessHoursEnd(DateTime currentTime)
        {
            var currentTimeOfDay = currentTime.TimeOfDay;
            var timesToEnd = _config.BusinessHours
                .Where(hours => currentTimeOfDay <= hours.End)
                .Select(hours => hours.End - currentTimeOfDay)
                .Where(time => time > TimeSpan.Zero)
                .ToList();

            return timesToEnd.Any() ? timesToEnd.Min() : TimeSpan.Zero;
        }

        /// <summary>
        /// Gets business hours information for logging
        /// </summary>
        /// <returns>Business hours information</returns>
        private object GetBusinessHoursInfo()
        {
            return new
            {
                WorkingDays = _config.WorkingDays,
                BusinessHours = _config.BusinessHours.Select(h => new { h.Start, h.End }),
                GracePeriodMinutes = _config.GracePeriodMinutes,
                MinimumTimeBeforeEndMinutes = _config.MinimumTimeBeforeEndMinutes,
                TimeZone = _config.TimeZone
            };
        }

        /// <summary>
        /// Checks if the given day is a weekend
        /// </summary>
        /// <param name="date">The date to check</param>
        /// <returns>True if it's a weekend</returns>
        private bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        /// <summary>
        /// Performs business hours specific validation
        /// </summary>
        /// <param name="request">The release request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        private async Task<StrategyValidationResult> PerformBusinessHoursValidationAsync(ReleaseRequest request, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Performing business hours validation for request {RequestId}", request.RequestId);

            // Check for holidays
            if (_config.RespectHolidays && IsHoliday(DateTime.Now))
            {
                return StrategyValidationResult.Failure("Today is a holiday, license release is not allowed");
            }

            // Check for company-specific restrictions
            if (_config.CompanySpecificRestrictions.Any())
            {
                var currentHour = DateTime.Now.Hour;
                var hasRestriction = _config.CompanySpecificRestrictions.Any(r =>
                    currentHour >= r.StartHour && currentHour <= r.EndHour);

                if (hasRestriction)
                {
                    return StrategyValidationResult.Failure("Company-specific restrictions prevent license release at this time");
                }
            }

            // Check for department-specific restrictions
            if (!string.IsNullOrEmpty(request.Metadata?.ToString()) && _config.DepartmentRestrictions.Any())
            {
                // In a real implementation, you'd extract department info from the request metadata
                // For now, we'll skip this check
            }

            // Check if it's too close to lunch break
            if (_config.LunchBreakHours.Any())
            {
                var currentTimeOfDay = DateTime.Now.TimeOfDay;
                var isInLunchBreak = _config.LunchBreakHours.Any(lunch =>
                    currentTimeOfDay >= lunch.Start && currentTimeOfDay <= lunch.End);

                if (isInLunchBreak && _config.PreventReleaseDuringLunch)
                {
                    return StrategyValidationResult.Failure("License release is not allowed during lunch break");
                }
            }

            _logger.LogDebug("Business hours validation passed for request {RequestId}", request.RequestId);
            return StrategyValidationResult.Success();
        }

        /// <summary>
        /// Checks if today is a holiday
        /// </summary>
        /// <param name="date">The date to check</param>
        /// <returns>True if it's a holiday</returns>
        private bool IsHoliday(DateTime date)
        {
            // In a real implementation, this would check against a holiday calendar
            // For now, we'll check for some common holidays
            var month = date.Month;
            var day = date.Day;

            // New Year's Day
            if (month == 1 && day == 1) return true;

            // Christmas
            if (month == 12 && day == 25) return true;

            // Add more holidays as needed

            return false;
        }
    }

    /// <summary>
    /// Configuration for the BusinessHoursReleaseStrategy
    /// </summary>
    public class BusinessHoursReleaseStrategyConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether the strategy is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the working days
        /// </summary>
        public List<DayOfWeek> WorkingDays { get; set; } = new List<DayOfWeek>
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };

        /// <summary>
        /// Gets or sets the business hours
        /// </summary>
        public List<BusinessHours> BusinessHours { get; set; } = new List<BusinessHours>
        {
            new BusinessHours(TimeSpan.FromHours(9), TimeSpan.FromHours(17)) // 9 AM to 5 PM
        };

        /// <summary>
        /// Gets or sets the grace period in minutes (allows release before official start time)
        /// </summary>
        public int GracePeriodMinutes { get; set; } = 15;

        /// <summary>
        /// Gets or sets the minimum time before business hours end in minutes
        /// </summary>
        public int MinimumTimeBeforeEndMinutes { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to respect holidays
        /// </summary>
        public bool RespectHolidays { get; set; } = true;

        /// <summary>
        /// Gets or sets the lunch break hours
        /// </summary>
        public List<BusinessHours> LunchBreakHours { get; set; } = new List<BusinessHours>
        {
            new BusinessHours(TimeSpan.FromHours(12), TimeSpan.FromHours(13)) // 12 PM to 1 PM
        };

        /// <summary>
        /// Gets or sets a value indicating whether to prevent release during lunch break
        /// </summary>
        public bool PreventReleaseDuringLunch { get; set; } = true;

        /// <summary>
        /// Gets or sets the company-specific restrictions
        /// </summary>
        public List<CompanyRestriction> CompanySpecificRestrictions { get; set; } = new List<CompanyRestriction>();

        /// <summary>
        /// Gets or sets the department restrictions
        /// </summary>
        public List<DepartmentRestriction> DepartmentRestrictions { get; set; } = new List<DepartmentRestriction>();

        /// <summary>
        /// Gets or sets the time zone for business hours
        /// </summary>
        public string TimeZone { get; set; } = "Local";

        /// <summary>
        /// Gets or sets the timeout for release operations in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets custom configuration parameters
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents business hours with start and end times
    /// </summary>
    public class BusinessHours
    {
        /// <summary>
        /// Gets or sets the start time
        /// </summary>
        public TimeSpan Start { get; set; }

        /// <summary>
        /// Gets or sets the end time
        /// </summary>
        public TimeSpan End { get; set; }

        /// <summary>
        /// Initializes a new instance of the BusinessHours class
        /// </summary>
        /// <param name="start">Start time</param>
        /// <param name="end">End time</param>
        public BusinessHours(TimeSpan start, TimeSpan end)
        {
            Start = start;
            End = end;
        }
    }

    /// <summary>
    /// Represents company-specific time restrictions
    /// </summary>
    public class CompanyRestriction
    {
        /// <summary>
        /// Gets or sets the start hour (0-23)
        /// </summary>
        public int StartHour { get; set; }

        /// <summary>
        /// Gets or sets the end hour (0-23)
        /// </summary>
        public int EndHour { get; set; }

        /// <summary>
        /// Gets or sets the reason for the restriction
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the CompanyRestriction class
        /// </summary>
        /// <param name="startHour">Start hour</param>
        /// <param name="endHour">End hour</param>
        /// <param name="reason">Reason for restriction</param>
        public CompanyRestriction(int startHour, int endHour, string reason)
        {
            StartHour = startHour;
            EndHour = endHour;
            Reason = reason;
        }
    }

    /// <summary>
    /// Represents department-specific restrictions
    /// </summary>
    public class DepartmentRestriction
    {
        /// <summary>
        /// Gets or sets the department name
        /// </summary>
        public string Department { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the allowed hours for the department
        /// </summary>
        public List<BusinessHours> AllowedHours { get; set; } = new List<BusinessHours>();

        /// <summary>
        /// Gets or sets a value indicating whether the department can release licenses on weekends
        /// </summary>
        public bool AllowWeekendReleases { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of concurrent releases for the department
        /// </summary>
        public int MaxConcurrentReleases { get; set; } = 1;
    }
}