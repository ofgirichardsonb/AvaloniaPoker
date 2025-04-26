using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PokerGame.Core.Process
{
    /// <summary>
    /// Manages external processes for the poker game services
    /// Provides reliable launching, monitoring, and cleanup of processes
    /// </summary>
    public class ProcessManager : IDisposable
    {
        private static ProcessManager? _instance;
        private static readonly object _lockObject = new object();
        private readonly List<ProcessInfo> _managedProcesses = new List<ProcessInfo>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _monitoringTask;
        private bool _isDisposed = false;

        /// <summary>
        /// Gets the singleton instance of the ProcessManager
        /// </summary>
        public static ProcessManager Instance
        {
            get
            {
                lock (_lockObject)
                {
                    if (_instance == null || _instance._isDisposed)
                    {
                        _instance = new ProcessManager();
                    }
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private ProcessManager()
        {
            // Load configuration from appsettings.json if it exists
            LoadConfiguration();
            
            // Register for process exit to ensure cleanup happens
            AppDomain.CurrentDomain.ProcessExit += (s, e) => 
            {
                Console.WriteLine("Process exit detected - cleaning up managed processes");
                Dispose();
            };

            // Start the monitoring task
            _monitoringTask = Task.Run(MonitorProcessesAsync);
        }
        
        /// <summary>
        /// Loads configuration from appsettings.json
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                // Look for appsettings.json in the current directory and parent directories
                string? configPath = FindConfigFile("appsettings.json");
                
                if (configPath != null)
                {
                    Console.WriteLine($"Loading configuration from: {configPath}");
                    
                    // Build configuration
                    var config = new ConfigurationBuilder()
                        .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                        .Build();
                    
                    // Get ProcessManager settings
                    var processManagerSection = config.GetSection("ProcessManager");
                    
                    // Get DotNetPath setting from config
                    string? configuredDotNetPath = processManagerSection.GetValue<string>("DotNetPath");
                    if (!string.IsNullOrEmpty(configuredDotNetPath))
                    {
                        _dotnetPath = configuredDotNetPath;
                        Console.WriteLine($"Using dotnet path from configuration: {_dotnetPath}");
                    }
                    else
                    {
                        Console.WriteLine("DotNetPath not found in configuration.");
                    }
                    
                    // Get project paths
                    string? configuredBasePath = processManagerSection.GetValue<string>("ProjectBasePath");
                    if (!string.IsNullOrEmpty(configuredBasePath))
                    {
                        _projectBasePath = configuredBasePath;
                        Console.WriteLine($"Using project base path from configuration: {_projectBasePath}");
                    }
                    
                    string? configuredConsoleProjectPath = processManagerSection.GetValue<string>("ConsoleProjectPath");
                    if (!string.IsNullOrEmpty(configuredConsoleProjectPath))
                    {
                        _consoleProjectPath = configuredConsoleProjectPath;
                        Console.WriteLine($"Using console project path from configuration: {_consoleProjectPath}");
                    }
                    
                    string? configuredServicesProjectPath = processManagerSection.GetValue<string>("ServicesProjectPath");
                    if (!string.IsNullOrEmpty(configuredServicesProjectPath))
                    {
                        _servicesProjectPath = configuredServicesProjectPath;
                        Console.WriteLine($"Using services project path from configuration: {_servicesProjectPath}");
                    }
                    
                    // Check if we should auto-detect the dotnet path
                    bool autoDetect = processManagerSection.GetValue<bool>("AutoDetectDotNetPath");
                    if (autoDetect)
                    {
                        Console.WriteLine("Auto-detection of dotnet path is enabled.");
                        InitializeDotNetPath();
                    }
                    
                    // If no project base path is set, try to determine it based on the executing assembly
                    if (string.IsNullOrEmpty(_projectBasePath))
                    {
                        DetermineProjectBasePath();
                    }
                }
                else
                {
                    Console.WriteLine("appsettings.json not found, using default configuration.");
                    InitializeDotNetPath();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                
                // Fall back to auto-detection
                InitializeDotNetPath();
            }
        }
        
        /// <summary>
        /// Finds the appsettings.json file by searching in the current and parent directories
        /// </summary>
        private string? FindConfigFile(string fileName)
        {
            // Start with the current directory
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            
            // Check if the file exists in this directory
            string configPath = Path.Combine(directory, fileName);
            if (File.Exists(configPath))
                return configPath;
                
            // Keep going up directories
            while (!string.IsNullOrEmpty(directory))
            {
                // Check in the current directory
                configPath = Path.Combine(directory, fileName);
                if (File.Exists(configPath))
                    return configPath;
                    
                // Check in a nested Config folder
                configPath = Path.Combine(directory, "Config", fileName);
                if (File.Exists(configPath))
                    return configPath;
                
                // Also check in PokerGame.Launcher subdirectory
                configPath = Path.Combine(directory, "PokerGame.Launcher", fileName);
                if (File.Exists(configPath))
                    return configPath;
                
                // Go up one directory
                var parent = Directory.GetParent(directory);
                if (parent == null)
                    break;
                    
                directory = parent.FullName;
            }
            
            // Not found
            return null;
        }
        
        /// <summary>
        /// Tries to determine the project base path based on the executing assembly location
        /// </summary>
        private void DetermineProjectBasePath()
        {
            try
            {
                // Get the directory of the executing assembly
                string assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"Assembly location: {assemblyLocation}");
                
                // Try to find the solution directory by looking for the sln file
                string? solutionDir = FindSolutionDirectory(assemblyLocation);
                if (!string.IsNullOrEmpty(solutionDir))
                {
                    _projectBasePath = solutionDir;
                    Console.WriteLine($"Project base path determined as: {_projectBasePath}");
                    return;
                }
                
                // If we couldn't find the solution directory, try a few common patterns
                // First, check if we're in a bin/Release/net8.0 or similar directory
                var dir = new DirectoryInfo(assemblyLocation);
                if (dir.Name.StartsWith("net") && dir.Parent != null && 
                    (dir.Parent.Name == "Release" || dir.Parent.Name == "Debug") && 
                    dir.Parent.Parent != null && dir.Parent.Parent.Name == "bin" && 
                    dir.Parent.Parent.Parent != null)
                {
                    // We're in a project's output directory, go up to the project directory
                    _projectBasePath = dir.Parent.Parent.Parent.FullName;
                    
                    // Now check if this is a project directory by looking for the project file
                    string? projectDir = FindProjectDirectory(_projectBasePath);
                    if (projectDir != null && !string.IsNullOrEmpty(projectDir))
                    {
                        _projectBasePath = projectDir;
                        Console.WriteLine($"Project base path determined from output directory: {_projectBasePath}");
                        return;
                    }
                }
                
                // Last resort: check for the existence of the project directories to navigate
                if (Directory.Exists(Path.Combine(assemblyLocation, "PokerGame.Core")))
                {
                    _projectBasePath = assemblyLocation;
                    Console.WriteLine($"Project base path set to assembly directory: {_projectBasePath}");
                    return;
                }
                
                // If all else fails, try going up one directory at a time and check for project folders
                dir = new DirectoryInfo(assemblyLocation);
                while (dir != null && dir.Parent != null)
                {
                    dir = dir.Parent;
                    if (Directory.Exists(Path.Combine(dir.FullName, "PokerGame.Core")) || 
                        Directory.Exists(Path.Combine(dir.FullName, "PokerGame.Console")))
                    {
                        _projectBasePath = dir.FullName;
                        Console.WriteLine($"Project base path determined by finding project directories: {_projectBasePath}");
                        return;
                    }
                }
                
                // If we can't determine the base path, use the current directory
                _projectBasePath = Directory.GetCurrentDirectory();
                Console.WriteLine($"Couldn't determine project base path, using current directory: {_projectBasePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error determining project base path: {ex.Message}");
                _projectBasePath = Directory.GetCurrentDirectory();
                Console.WriteLine($"Using current directory as fallback: {_projectBasePath}");
            }
        }
        
        /// <summary>
        /// Finds the solution directory by looking for a .sln file
        /// </summary>
        private string? FindSolutionDirectory(string startDir)
        {
            try
            {
                // Start with the given directory
                string directory = startDir;
                
                // Keep going up directories looking for a .sln file
                while (!string.IsNullOrEmpty(directory))
                {
                    // Look for .sln files in this directory
                    string[] slnFiles = Directory.GetFiles(directory, "*.sln");
                    if (slnFiles.Length > 0)
                    {
                        return directory;
                    }
                    
                    // Go up one directory
                    var parent = Directory.GetParent(directory);
                    if (parent == null)
                        break;
                        
                    directory = parent.FullName;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding solution directory: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Finds the project directory by looking for a .csproj file or certain project directories
        /// </summary>
        private string? FindProjectDirectory(string startDir)
        {
            try
            {
                // Check if this is a project directory
                string[] csprojFiles = Directory.GetFiles(startDir, "*.csproj");
                if (csprojFiles.Length > 0)
                {
                    // This is a project directory, get its parent (which would be the solution directory)
                    var parent = Directory.GetParent(startDir);
                    return parent?.FullName;
                }
                
                // Check if the solution directory contains our project directories
                if (Directory.Exists(Path.Combine(startDir, "PokerGame.Core")) || 
                    Directory.Exists(Path.Combine(startDir, "PokerGame.Console")))
                {
                    return startDir;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding project directory: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Starts a new process with the specified parameters
        /// </summary>
        /// <param name="fileName">The executable file path</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="serviceName">A friendly name for the service</param>
        /// <param name="workingDirectory">Optional working directory</param>
        /// <param name="captureOutput">Whether to capture stdout/stderr</param>
        /// <param name="killOnDispose">Whether to kill the process when the ProcessManager is disposed</param>
        /// <returns>The Process ID of the launched process, or -1 if failed</returns>
        public int StartProcess(
            string fileName, 
            string arguments, 
            string serviceName,
            string? workingDirectory = null,
            bool captureOutput = true,
            bool killOnDispose = true)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ProcessManager));

            try
            {
                // Create the process start info
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = captureOutput,
                    RedirectStandardError = captureOutput
                };

                // Create and start the process
                var process = new System.Diagnostics.Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                // Set up output handlers if capturing output
                if (captureOutput)
                {
                    process.OutputDataReceived += (sender, e) => 
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Console.WriteLine($"[{serviceName}] {e.Data}");
                    };
                    
                    process.ErrorDataReceived += (sender, e) => 
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Console.WriteLine($"[{serviceName} ERROR] {e.Data}");
                    };
                }

                // Start the process
                process.Start();

                // Begin capturing output if requested
                if (captureOutput)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }

                // Create a process info record
                var processInfo = new ProcessInfo
                {
                    Process = process,
                    ServiceName = serviceName,
                    StartTime = DateTime.UtcNow,
                    KillOnDispose = killOnDispose
                };

                // Register the process
                lock (_managedProcesses)
                {
                    _managedProcesses.Add(processInfo);
                }

                Console.WriteLine($"Started {serviceName} process with PID {process.Id}");
                return process.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start {serviceName} process: {ex.Message}");
                return -1;
            }
        }

        // Configurable paths
        private string _dotnetPath = "dotnet";
        private string _projectBasePath = "";
        private string _consoleProjectPath = "PokerGame.Console/PokerGame.Console.csproj";
        private string _servicesProjectPath = "PokerGame.Services/PokerGame.Services.csproj";
        
        /// <summary>
        /// Gets or sets the path to the dotnet executable
        /// </summary>
        public string DotNetPath 
        {
            get => _dotnetPath;
            set => _dotnetPath = !string.IsNullOrEmpty(value) ? value : "dotnet";
        }
        
        /// <summary>
        /// Gets or sets the base path for project files
        /// </summary>
        public string ProjectBasePath
        {
            get => _projectBasePath;
            set => _projectBasePath = value ?? "";
        }
        
        /// <summary>
        /// Gets or sets the path to the console project
        /// </summary>
        public string ConsoleProjectPath
        {
            get => _consoleProjectPath;
            set => _consoleProjectPath = !string.IsNullOrEmpty(value) ? value : "PokerGame.Console/PokerGame.Console.csproj";
        }
        
        /// <summary>
        /// Gets or sets the path to the services project
        /// </summary>
        public string ServicesProjectPath
        {
            get => _servicesProjectPath;
            set => _servicesProjectPath = !string.IsNullOrEmpty(value) ? value : "PokerGame.Services/PokerGame.Services.csproj";
        }
        
        /// <summary>
        /// Gets the full path to the console project
        /// </summary>
        public string GetFullConsoleProjectPath() => 
            Path.Combine(ProjectBasePath, ConsoleProjectPath);
            
        /// <summary>
        /// Gets the full path to the services project
        /// </summary>
        public string GetFullServicesProjectPath() => 
            Path.Combine(ProjectBasePath, ServicesProjectPath);
        
        /// <summary>
        /// Initializes the dotnet path by detecting it from the environment
        /// </summary>
        public void InitializeDotNetPath()
        {
            try
            {
                // Try to find dotnet executable using the 'which' command (Linux/macOS)
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = "dotnet",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string path = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                
                // If we found a path, use it
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    _dotnetPath = path;
                    Console.WriteLine($"Located dotnet at: {_dotnetPath}");
                    return;
                }
                
                // If 'which' failed, try 'where' command (Windows)
                process = new System.Diagnostics.Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "dotnet",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                path = process.StandardOutput.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                process.WaitForExit();
                
                // If we found a path, use it
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    _dotnetPath = path;
                    Console.WriteLine($"Located dotnet at: {_dotnetPath}");
                    return;
                }
                
                // If we get here, we couldn't find dotnet - use the default
                Console.WriteLine("Could not locate dotnet executable. Using default 'dotnet' command, which requires it to be in PATH.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting dotnet path: {ex.Message}");
                Console.WriteLine("Using default 'dotnet' command, which requires it to be in PATH.");
            }
        }
        
        /// <summary>
        /// Starts the services host process
        /// </summary>
        /// <param name="portOffset">Port offset for the services</param>
        /// <param name="verbose">Enable verbose logging</param>
        /// <returns>The process ID if successful, -1 otherwise</returns>
        public int StartServicesHost(int portOffset, bool verbose = false)
        {
            string arguments = $"--all-services --port-offset={portOffset}";
            if (verbose)
                arguments += " --verbose";
                
            string projectPath = GetFullServicesProjectPath();
            Console.WriteLine($"Starting services host with project path: {projectPath}");
                
            return StartProcess(
                _dotnetPath, 
                $"run --project {projectPath} -- {arguments}",
                "ServicesHost");
        }

        /// <summary>
        /// Starts the console UI client process
        /// </summary>
        /// <param name="useCurses">Whether to use the curses UI</param>
        /// <param name="portOffset">Port offset matching the services</param>
        /// <param name="verbose">Enable verbose logging</param>
        /// <returns>The process ID if successful, -1 otherwise</returns>
        public int StartConsoleClient(bool useCurses, int portOffset, bool verbose = false)
        {
            string arguments = $"--port-offset={portOffset}";
            
            if (useCurses)
                arguments += " --curses";
            else
                arguments += " --enhanced-ui";
                
            if (verbose)
                arguments += " --verbose";
                
            string projectPath = GetFullConsoleProjectPath();
            Console.WriteLine($"Starting console client with project path: {projectPath}");
                
            return StartProcess(
                _dotnetPath, 
                $"run --project {projectPath} -- {arguments}",
                useCurses ? "CursesUI" : "ConsoleUI");
        }

        /// <summary>
        /// Stops a specific process
        /// </summary>
        /// <param name="processId">The process ID to stop</param>
        /// <returns>True if the process was found and stopped, false otherwise</returns>
        public bool StopProcess(int processId)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ProcessManager));

            lock (_managedProcesses)
            {
                var processInfo = _managedProcesses.FirstOrDefault(p => p.Process.Id == processId);
                if (processInfo != null)
                {
                    KillProcess(processInfo);
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Stops all processes started with a given service name
        /// </summary>
        /// <param name="serviceName">The name of the service to stop</param>
        /// <returns>The number of processes stopped</returns>
        public int StopProcessesByName(string serviceName)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ProcessManager));

            int count = 0;
            
            lock (_managedProcesses)
            {
                var processes = _managedProcesses.Where(p => p.ServiceName == serviceName).ToList();
                foreach (var process in processes)
                {
                    KillProcess(process);
                    count++;
                }
            }
            
            return count;
        }

        /// <summary>
        /// Stops all managed processes
        /// </summary>
        public void StopAllProcesses()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ProcessManager));

            lock (_managedProcesses)
            {
                foreach (var process in _managedProcesses.ToList())
                {
                    KillProcess(process);
                }
            }
        }

        /// <summary>
        /// Helper method to kill a process
        /// </summary>
        private void KillProcess(ProcessInfo processInfo)
        {
            try
            {
                if (!processInfo.Process.HasExited)
                {
                    Console.WriteLine($"Stopping {processInfo.ServiceName} (PID {processInfo.Process.Id})...");
                    processInfo.Process.Kill(true); // Kill the entire process tree
                    processInfo.Process.WaitForExit(2000);
                }
                else
                {
                    Console.WriteLine($"{processInfo.ServiceName} (PID {processInfo.Process.Id}) has already exited.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping {processInfo.ServiceName} (PID {processInfo.Process.Id}): {ex.Message}");
            }
            finally
            {
                lock (_managedProcesses)
                {
                    _managedProcesses.Remove(processInfo);
                }

                try { processInfo.Process.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Periodically monitors processes to detect if they've exited unexpectedly
        /// </summary>
        private async Task MonitorProcessesAsync()
        {
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    // Check processes every second
                    await Task.Delay(1000, _cancellationTokenSource.Token);

                    lock (_managedProcesses)
                    {
                        var exited = _managedProcesses.Where(p => p.Process.HasExited).ToList();
                        foreach (var processInfo in exited)
                        {
                            Console.WriteLine($"{processInfo.ServiceName} (PID {processInfo.Process.Id}) has exited unexpectedly with code {processInfo.Process.ExitCode}");
                            _managedProcesses.Remove(processInfo);
                            
                            try { processInfo.Process.Dispose(); } catch { }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in process monitoring task: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a specific service is running
        /// </summary>
        /// <param name="serviceName">The name of the service to check</param>
        /// <returns>True if at least one instance of the service is running</returns>
        public bool IsServiceRunning(string serviceName)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ProcessManager));

            lock (_managedProcesses)
            {
                return _managedProcesses.Any(p => p.ServiceName == serviceName && !p.Process.HasExited);
            }
        }

        /// <summary>
        /// Returns a list of all running services
        /// </summary>
        /// <returns>A collection of service names that are currently running</returns>
        public IEnumerable<string> GetRunningServices()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ProcessManager));

            lock (_managedProcesses)
            {
                return _managedProcesses
                    .Where(p => !p.Process.HasExited)
                    .Select(p => p.ServiceName)
                    .Distinct()
                    .ToList();
            }
        }

        /// <summary>
        /// Disposes the managed resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            // Stop the monitoring task
            _cancellationTokenSource.Cancel();
            
            try
            {
                _monitoringTask?.Wait(1000);
            }
            catch {}

            // Stop all processes that should be killed on dispose
            lock (_managedProcesses)
            {
                foreach (var process in _managedProcesses.Where(p => p.KillOnDispose).ToList())
                {
                    KillProcess(process);
                }
            }

            // Clean up resources
            _cancellationTokenSource.Dispose();
            _isDisposed = true;
        }

        /// <summary>
        /// Internal class to track process information
        /// </summary>
        private class ProcessInfo
        {
            /// <summary>
            /// The managed process
            /// </summary>
            public required System.Diagnostics.Process Process { get; set; }
            
            /// <summary>
            /// A friendly name for the service
            /// </summary>
            public required string ServiceName { get; set; }
            
            /// <summary>
            /// When the process was started
            /// </summary>
            public DateTime StartTime { get; set; }
            
            /// <summary>
            /// Whether to kill the process when the manager is disposed
            /// </summary>
            public bool KillOnDispose { get; set; } = true;
        }
    }
}