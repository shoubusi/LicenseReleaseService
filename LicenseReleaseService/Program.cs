using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace LicenseReleaseService
{
	internal static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main(string[] args)
		{
			// Check for console mode argument
			if (args.Length > 0 && args[0].Equals("/console", StringComparison.OrdinalIgnoreCase))
			{
				RunInConsoleMode();
				return;
			}

			// Check for installation arguments
			if (args.Length > 0)
			{
				if (args[0].Equals("/install", StringComparison.OrdinalIgnoreCase))
				{
					InstallService();
					return;
				}
				else if (args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
				{
					UninstallService();
					return;
				}
			}

			// Normal service mode
			ServiceBase[] ServicesToRun;
			ServicesToRun = new ServiceBase[]
			{
				new LicenseReleaseService()
			};
			ServiceBase.Run(ServicesToRun);
		}

		/// <summary>
		/// Run the service in console mode for debugging
		/// </summary>
		private static void RunInConsoleMode()
		{
			Console.WriteLine("License Release Service running in console mode");
			Console.WriteLine("Press Ctrl+C to stop the service");

			var service = new LicenseReleaseService();

			// Start the service
			service.OnStart(new string[0]);

			// Wait for Ctrl+C
			Console.CancelKeyPress += (sender, e) =>
			{
				Console.WriteLine("Shutting down service...");
				service.OnStop();
				e.Cancel = true;
			};

			// Keep running until Ctrl+C
			while (true)
			{
				Task.Delay(1000).Wait();
			}
		}

		/// <summary>
		/// Install the Windows Service
		/// </summary>
		private static void InstallService()
		{
			Console.WriteLine("Installing License Release Service...");
			// This would use installutil or sc.exe to install the service
			// Implementation would depend on deployment strategy
			Console.WriteLine("Service installation completed");
		}

		/// <summary>
		/// Uninstall the Windows Service
		/// </summary>
		private static void UninstallService()
		{
			Console.WriteLine("Uninstalling License Release Service...");
			// This would use installutil or sc.exe to uninstall the service
			// Implementation would depend on deployment strategy
			Console.WriteLine("Service uninstallation completed");
		}
	}
}