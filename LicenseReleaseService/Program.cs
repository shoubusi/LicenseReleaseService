using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration.Install;
using System.IO;
using LicenseReleaseService.Configuration;

namespace LicenseReleaseService
{
	internal static class Program
	{
		private const string ServiceName = "LicenseReleaseService";
		private const string ServiceDisplayName = "License Release Service";
		private const string ServiceDescription = "Manages software license releases and monitoring";
		private static readonly ILogger _logger = new EventLogLogger();

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main(string[] args)
		{
			try
			{
				// Log application startup
				_logger.LogInformation($"{ServiceDisplayName} starting with arguments: {string.Join(", ", args)}");

				// Initialize configuration management early
				InitializeConfigurationManagement();

				// Parse command line arguments
				var parsedArgs = ParseCommandLineArguments(args);
				_logger.LogInformation($"Parsed command: {parsedArgs.Command}");

				// Execute based on command
				switch (parsedArgs.Command)
				{
					case ServiceCommand.Console:
						_logger.LogInformation("Starting in console mode");
						RunInConsoleMode(parsedArgs.Arguments);
						break;
					case ServiceCommand.Install:
						_logger.LogInformation("Starting service installation");
						InstallService();
						break;
					case ServiceCommand.Uninstall:
						_logger.LogInformation("Starting service uninstallation");
						UninstallService();
						break;
					case ServiceCommand.Interactive:
						_logger.LogInformation("Starting in interactive mode");
						RunInInteractiveMode();
						break;
					case ServiceCommand.Help:
						_logger.LogInformation("Displaying help information");
						ShowHelp();
						break;
					case ServiceCommand.Config:
						_logger.LogInformation("Displaying configuration information");
						ShowConfiguration();
						break;
					case ServiceCommand.Validate:
						_logger.LogInformation("Validating configuration");
						ValidateConfiguration();
						break;
					case ServiceCommand.Service:
					default:
						_logger.LogInformation("Starting as Windows Service");
						RunAsService();
						break;
				}

				_logger.LogInformation($"{ServiceDisplayName} exiting normally");
			}
			catch (Exception ex)
			{
				_logger.LogError($"Fatal error in {ServiceDisplayName}: {ex.Message}", ex);
				Console.WriteLine($"Fatal error: {ex.Message}");
				Console.WriteLine(ex.StackTrace);
				Environment.Exit(1);
			}
		}

		/// <summary>
		/// Initialize configuration management for the application
		/// </summary>
		private static void InitializeConfigurationManagement()
		{
			try
			{
				_logger.LogInformation("Initializing configuration management");

				// Get the ConfigurationManager instance to initialize it
				var configManager = ConfigurationManager.Instance;

				// Validate configuration
				var validationErrors = configManager.ValidateConfiguration();
				if (validationErrors.Count > 0)
				{
					var errorString = string.Join("; ", validationErrors);
					_logger.LogWarning($"Configuration validation warnings: {errorString}");
				}
				else
				{
					_logger.LogInformation("Configuration validation passed");
				}

				// Enable advanced features if configured
				var enableAdvancedFeatures = configManager.Settings.EnableAdvancedFeatures;
				if (enableAdvancedFeatures)
				{
					configManager.EnableAdvancedFeatures();
					_logger.LogInformation("Advanced configuration features enabled");
				}

				_logger.LogInformation("Configuration management initialized successfully");
			}
			catch (Exception ex)
			{
				_logger.LogError($"Failed to initialize configuration management: {ex.Message}", ex);
				throw new ConfigurationInitializationException($"Configuration initialization failed: {ex.Message}", ex);
			}
		}

		/// <summary>
		/// Parse command line arguments
		/// </summary>
		private static ParsedArguments ParseCommandLineArguments(string[] args)
		{
			if (args.Length == 0)
			{
				_logger.LogInformation("No arguments provided, running as Windows Service");
				return new ParsedArguments { Command = ServiceCommand.Service, Arguments = new string[0] };
			}

			var command = args[0].ToLowerInvariant();
			var remainingArgs = args.Skip(1).ToArray();

			_logger.LogInformation($"Parsing command: {command} with {remainingArgs.Length} additional arguments");

			switch (command)
			{
				case "/console":
				case "-console":
				case "--console":
					_logger.LogInformation("Console mode detected");
					return new ParsedArguments { Command = ServiceCommand.Console, Arguments = remainingArgs };

				case "/install":
				case "-install":
				case "--install":
					_logger.LogInformation("Install command detected");
					return new ParsedArguments { Command = ServiceCommand.Install, Arguments = remainingArgs };

				case "/uninstall":
				case "-uninstall":
				case "--uninstall":
					_logger.LogInformation("Uninstall command detected");
					return new ParsedArguments { Command = ServiceCommand.Uninstall, Arguments = remainingArgs };

				case "/interactive":
				case "-interactive":
				case "--interactive":
					_logger.LogInformation("Interactive mode detected");
					return new ParsedArguments { Command = ServiceCommand.Interactive, Arguments = remainingArgs };

				case "/help":
				case "-help":
				case "--help":
				case "/?":
				case "-?":
					_logger.LogInformation("Help command detected");
					return new ParsedArguments { Command = ServiceCommand.Help, Arguments = remainingArgs };

				case "/config":
				case "-config":
				case "--config":
					_logger.LogInformation("Config command detected");
					return new ParsedArguments { Command = ServiceCommand.Config, Arguments = remainingArgs };

				case "/validate":
				case "-validate":
				case "--validate":
					_logger.LogInformation("Validate command detected");
					return new ParsedArguments { Command = ServiceCommand.Validate, Arguments = remainingArgs };

				default:
					_logger.LogWarning($"Unknown command detected: {command}");
					Console.WriteLine($"Unknown command: {command}");
					ShowHelp();
					Environment.Exit(1);
					return new ParsedArguments(); // This line won't be reached due to Environment.Exit
			}
		}

		/// <summary>
		/// Run the service as a Windows Service
		/// </summary>
		private static void RunAsService()
		{
			try
			{
				_logger.LogInformation("Creating Windows Service instances");
				ServiceBase[] ServicesToRun =
				{
					new LicenseReleaseService()
				};
				_logger.LogInformation("Starting Windows Service runtime");
				ServiceBase.Run(ServicesToRun);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error running as Windows Service: {ex.Message}", ex);
				throw;
			}
		}
		/// <summary>
		/// Run the service in console mode for debugging
		/// </summary>
		private static void RunInConsoleMode(string[] args)
		{
			Console.WriteLine($"{ServiceDisplayName} running in console mode");
			Console.WriteLine("Press Ctrl+C to stop the service");
			Console.WriteLine();

			var service = new LicenseReleaseService();
			var cancellationTokenSource = new System.Threading.CancellationTokenSource();

			// Set up console cancellation
			Console.CancelKeyPress += (sender, e) =>
			{
				Console.WriteLine("\nShutting down service...");
				_logger.LogInformation("Console mode shutdown requested via Ctrl+C");
				cancellationTokenSource.Cancel();
				e.Cancel = true;
			};

			try
			{
				// Start the service
				_logger.LogInformation("Starting service in console mode");
				service.StartConsoleMode(args);
				Console.WriteLine("Service started successfully");

				// Keep running until cancelled
				while (!cancellationTokenSource.IsCancellationRequested)
				{
					System.Threading.Thread.Sleep(1000);
				}

				// Stop the service gracefully
				_logger.LogInformation("Stopping service in console mode");
				service.StopConsoleMode();
				Console.WriteLine("Service stopped successfully");
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error in console mode: {ex.Message}", ex);
				Console.WriteLine($"Error in console mode: {ex.Message}");
				Console.WriteLine(ex.StackTrace);
			}
			finally
			{
				_logger.LogInformation("Console mode cleanup completed");
				cancellationTokenSource.Dispose();
			}
		}

		/// <summary>
		/// Install the Windows Service
		/// </summary>
		private static void InstallService()
		{
			Console.WriteLine($"Installing {ServiceDisplayName}...");

			try
			{
				var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
				var installUtilPath = Path.Combine(
					System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
					"installutil.exe");

				if (!File.Exists(installUtilPath))
				{
					Console.WriteLine("Error: installutil.exe not found");
					Console.WriteLine("Please run this command as Administrator");
					Environment.Exit(1);
				}

				var process = new System.Diagnostics.Process
				{
					StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = installUtilPath,
						Arguments = $"\"{assemblyPath}\"",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};

				process.Start();
				var output = process.StandardOutput.ReadToEnd();
				var error = process.StandardError.ReadToEnd();
				process.WaitForExit();

				if (process.ExitCode == 0)
				{
					Console.WriteLine("Service installation completed successfully");
					if (!string.IsNullOrEmpty(output))
					{
						Console.WriteLine(output);
					}
				}
				else
				{
					Console.WriteLine($"Service installation failed with exit code: {process.ExitCode}");
					if (!string.IsNullOrEmpty(error))
					{
						Console.WriteLine("Error output:");
						Console.WriteLine(error);
					}
					Environment.Exit(1);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error installing service: {ex.Message}");
				Console.WriteLine(ex.StackTrace);
				Environment.Exit(1);
			}
		}

		/// <summary>
		/// Uninstall the Windows Service
		/// </summary>
		private static void UninstallService()
		{
			Console.WriteLine($"Uninstalling {ServiceDisplayName}...");

			try
			{
				var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
				var installUtilPath = Path.Combine(
					System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
					"installutil.exe");

				if (!File.Exists(installUtilPath))
				{
					Console.WriteLine("Error: installutil.exe not found");
					Console.WriteLine("Please run this command as Administrator");
					Environment.Exit(1);
				}

				var process = new System.Diagnostics.Process
				{
					StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = installUtilPath,
						Arguments = $"/u \"{assemblyPath}\"",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};

				process.Start();
				var output = process.StandardOutput.ReadToEnd();
				var error = process.StandardError.ReadToEnd();
				process.WaitForExit();

				if (process.ExitCode == 0)
				{
					Console.WriteLine("Service uninstallation completed successfully");
					if (!string.IsNullOrEmpty(output))
					{
						Console.WriteLine(output);
					}
				}
				else
				{
					Console.WriteLine($"Service uninstallation failed with exit code: {process.ExitCode}");
					if (!string.IsNullOrEmpty(error))
					{
						Console.WriteLine("Error output:");
						Console.WriteLine(error);
					}
					Environment.Exit(1);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error uninstalling service: {ex.Message}");
				Console.WriteLine(ex.StackTrace);
				Environment.Exit(1);
			}
		}

		/// <summary>
		/// Run the service in interactive mode for testing and debugging
		/// </summary>
		private static void RunInInteractiveMode()
		{
			Console.WriteLine($"{ServiceDisplayName} - Interactive Mode");
			Console.WriteLine("Available commands: start, stop, pause, continue, status, health, exit");
			Console.WriteLine();

			var service = new LicenseReleaseService();
			bool isRunning = false;

			while (true)
			{
				Console.Write("> ");
				var input = Console.ReadLine()?.Trim().ToLowerInvariant();

				if (string.IsNullOrEmpty(input))
					continue;

				switch (input)
				{
					case "start":
						if (!isRunning)
						{
							_logger.LogInformation("Interactive mode: starting service");
							service.StartConsoleMode(new string[0]);
							isRunning = true;
							Console.WriteLine("Service started");
						}
						else
						{
							Console.WriteLine("Service is already running");
						}
						break;

					case "stop":
						if (isRunning)
						{
							_logger.LogInformation("Interactive mode: stopping service");
							service.StopConsoleMode();
							isRunning = false;
							Console.WriteLine("Service stopped");
						}
						else
						{
							Console.WriteLine("Service is not running");
						}
						break;

					case "status":
						_logger.LogInformation("Interactive mode: checking service status");
						Console.WriteLine($"Service state: {service.CurrentState}");
						Console.WriteLine($"Is healthy: {service.IsHealthy}");
						break;

					case "health":
						_logger.LogInformation("Interactive mode: checking service health");
						Console.WriteLine($"Service health: {(service.IsHealthy ? "Healthy" : "Unhealthy")}");
						Console.WriteLine($"Current state: {service.CurrentState}");
						break;

					case "exit":
					case "quit":
						_logger.LogInformation("Interactive mode: exiting");
						if (isRunning)
						{
							service.StopConsoleMode();
						}
						Console.WriteLine("Exiting interactive mode");
						return;

					default:
						_logger.LogWarning($"Interactive mode: unknown command '{input}'");
						Console.WriteLine("Unknown command. Available commands: start, stop, pause, continue, status, health, exit");
						break;
				}
			}
		}

		/// <summary>
		/// Show help information
		/// </summary>
		private static void ShowHelp()
		{
			Console.WriteLine($"{ServiceDisplayName} - {ServiceDescription}");
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.WriteLine("  LicenseReleaseService.exe           Run as Windows Service (default)");
			Console.WriteLine("  LicenseReleaseService.exe /console  Run in console mode for debugging");
			Console.WriteLine("  LicenseReleaseService.exe /interactive Run in interactive mode for testing");
			Console.WriteLine("  LicenseReleaseService.exe /install  Install the Windows Service (requires admin)");
			Console.WriteLine("  LicenseReleaseService.exe /uninstall Uninstall the Windows Service (requires admin)");
			Console.WriteLine("  LicenseReleaseService.exe /help     Show this help information");
			Console.WriteLine("  LicenseReleaseService.exe /config   Show configuration information");
			Console.WriteLine("  LicenseReleaseService.exe /validate  Validate configuration and exit");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine("  LicenseReleaseService.exe /console");
			Console.WriteLine("  LicenseReleaseService.exe /install");
			Console.WriteLine("  LicenseReleaseService.exe /interactive");
			Console.WriteLine("  LicenseReleaseService.exe /config");
			Console.WriteLine("  LicenseReleaseService.exe /validate");
		}

		/// <summary>
		/// Show configuration information
		/// </summary>
		private static void ShowConfiguration()
		{
			try
			{
				var configManager = ConfigurationManager.Instance;
				Console.WriteLine("Configuration Information:");
				Console.WriteLine(configManager.GetConfigurationSummary());
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error showing configuration: {ex.Message}", ex);
				Console.WriteLine($"Error showing configuration: {ex.Message}");
				Environment.Exit(1);
			}
		}

		/// <summary>
		/// Validate configuration and exit
		/// </summary>
		private static void ValidateConfiguration()
		{
			try
			{
				var configManager = ConfigurationManager.Instance;
				var validationErrors = configManager.ValidateConfiguration();

				Console.WriteLine("Configuration Validation:");
				Console.WriteLine($"Configuration file: {configManager.ConfigFilePath}");
				Console.WriteLine($"Last change: {configManager.LastConfigChange:yyyy-MM-dd HH:mm:ss}");
				Console.WriteLine($"Last reload: {configManager.LastSuccessfulReload:yyyy-MM-dd HH:mm:ss}");
				Console.WriteLine($"Advanced features enabled: {configManager.AdvancedFeaturesEnabled}");
				Console.WriteLine($"Configuration health: {configManager.HealthStatus}");

				if (validationErrors.Count == 0)
				{
					Console.WriteLine("✓ Configuration is valid");
					Environment.Exit(0);
				}
				else
				{
					Console.WriteLine("✗ Configuration validation failed:");
					foreach (var error in validationErrors)
					{
						Console.WriteLine($"  - {error}");
					}
					Environment.Exit(1);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"Error validating configuration: {ex.Message}", ex);
				Console.WriteLine($"Error validating configuration: {ex.Message}");
				Environment.Exit(1);
			}
		}
	}

	/// <summary>
	/// Service command enumeration
	/// </summary>
	public enum ServiceCommand
	{
		Service,
		Console,
		Install,
		Uninstall,
		Interactive,
		Help,
		Config,
		Validate
	}

	/// <summary>
	/// Parsed command line arguments
	/// </summary>
	public class ParsedArguments
	{
		public ServiceCommand Command { get; set; }
		public string[] Arguments { get; set; } = new string[0];
	}
}