using System;
using System.Threading.Tasks;

namespace LicenseReleaseService.Tests.RateLimiting
{
    /// <summary>
    /// Simple test runner for rate limiting system
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                await RateLimitingStandaloneTest.RunAllTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test runner failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}