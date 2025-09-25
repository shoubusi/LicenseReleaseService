using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LicenseReleaseService.LicenseManagement
{
    /// <summary>
    /// Enumeration of supported lmutil.exe commands
    /// </summary>
    public enum LmutilCommands
    {
        /// <summary>
        /// Display license server status
        /// </summary>
        [Description("lmstat")]
        Lmstat,

        /// <summary>
        /// Remove license from user
        /// </summary>
        [Description("lmremove")]
        Lmremove,

        /// <summary>
        /// Check license availability
        /// </summary>
        [Description("lmcksum")]
        Lmcksum,

        /// <summary>
        /// Display license feature information
        /// </summary>
        [Description("lmstat -f")]
        LmstatFeature,

        /// <summary>
        /// Display license server status with verbose output
        /// </summary>
        [Description("lmstat -v")]
        LmstatVerbose,

        /// <summary>
        /// Display license server status for specific user
        /// </summary>
        [Description("lmstat -u")]
        LmstatUser,

        /// <summary>
        /// Display license server status for specific feature
        /// </summary>
        [Description("lmstat -S")]
        LmstatServer,

        /// <summary>
        /// Display license server status with long format
        /// </summary>
        [Description("lmstat -l")]
        LmstatLong,

        /// <summary>
        /// Display license server status with category format
        /// </summary>
        [Description("lmstat -c")]
        LmstatCategory,

        /// <summary>
        /// Display license server status with usage format
        /// </summary>
        [Description("lmstat -a")]
        LmstatUsage,

        /// <summary>
        /// Display license file path
        /// </summary>
        [Description("lmstat -p")]
        LmstatPath,

        /// <summary>
        /// Display license server daemon status
        /// </summary>
        [Description("lmstat -d")]
        LmstatDaemon,

        /// <summary>
        /// Display license server host status
        /// </summary>
        [Description("lmstat -i")]
        LmstatHost,

        /// <summary>
        /// Display license server vendor status
        /// </summary>
        [Description("lmstat -s")]
        LmstatVendor,

        /// <summary>
        /// Display license server version
        /// </summary>
        [Description("lmstat -V")]
        LmstatVersion,

        /// <summary>
        /// Display license server help
        /// </summary>
        [Description("lmstat -h")]
        LmstatHelp
    }

    /// <summary>
    /// Extension methods for LmutilCommands enum
    /// </summary>
    public static class LmutilCommandsExtensions
    {
        /// <summary>
        /// Gets the command line argument for the specified lmutil command
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <returns>Command line argument string</returns>
        public static string GetCommandLineArgument(this LmutilCommands command)
        {
            var fieldInfo = command.GetType().GetField(command.ToString());
            var attribute = (DescriptionAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute));
            return attribute?.Description ?? command.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Gets all available lmutil commands
        /// </summary>
        /// <returns>Array of all lmutil commands</returns>
        public static LmutilCommands[] GetAllCommands()
        {
            return (LmutilCommands[])Enum.GetValues(typeof(LmutilCommands));
        }

        /// <summary>
        /// Gets lmutil commands that require server connection
        /// </summary>
        /// <returns>Array of commands requiring server connection</returns>
        public static LmutilCommands[] GetServerCommands()
        {
            return new[]
            {
                LmutilCommands.Lmstat,
                LmutilCommands.Lmremove,
                LmutilCommands.LmstatFeature,
                LmutilCommands.LmstatVerbose,
                LmutilCommands.LmstatUser,
                LmutilCommands.LmstatServer,
                LmutilCommands.LmstatLong,
                LmutilCommands.LmstatCategory,
                LmutilCommands.LmstatUsage,
                LmutilCommands.LmstatDaemon,
                LmutilCommands.LmstatHost,
                LmutilCommands.LmstatVendor
            };
        }

        /// <summary>
        /// Gets lmutil commands that can be executed offline
        /// </summary>
        /// <returns>Array of commands that can be executed offline</returns>
        public static LmutilCommands[] GetOfflineCommands()
        {
            return new[]
            {
                LmutilCommands.Lmcksum,
                LmutilCommands.LmstatPath,
                LmutilCommands.LmstatVersion,
                LmutilCommands.LmstatHelp
            };
        }

        /// <summary>
        /// Checks if the command requires a server connection
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <returns>True if server connection is required</returns>
        public static bool RequiresServerConnection(this LmutilCommands command)
        {
            var serverCommands = GetServerCommands();
            return Array.Exists(serverCommands, c => c == command);
        }

        /// <summary>
        /// Gets the command category
        /// </summary>
        /// <param name="command">The lmutil command</param>
        /// <returns>Command category</returns>
        public static string GetCategory(this LmutilCommands command)
        {
            switch (command)
            {
                case LmutilCommands.Lmstat:
                case LmutilCommands.LmstatVerbose:
                case LmutilCommands.LmstatLong:
                case LmutilCommands.LmstatCategory:
                case LmutilCommands.LmstatUsage:
                case LmutilCommands.LmstatDaemon:
                case LmutilCommands.LmstatHost:
                case LmutilCommands.LmstatVendor:
                    return "Status";

                case LmutilCommands.Lmremove:
                    return "Management";

                case LmutilCommands.Lmcksum:
                case LmutilCommands.LmstatPath:
                case LmutilCommands.LmstatVersion:
                case LmutilCommands.LmstatHelp:
                    return "Utility";

                case LmutilCommands.LmstatFeature:
                case LmutilCommands.LmstatUser:
                case LmutilCommands.LmstatServer:
                    return "Query";

                default:
                    return "Unknown";
            }
        }
    }
}