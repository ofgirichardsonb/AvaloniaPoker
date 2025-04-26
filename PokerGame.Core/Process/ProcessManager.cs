using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                
            return StartProcess(
                "dotnet", 
                $"run --project PokerGame.Services/PokerGame.Services.csproj -- {arguments}",
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
                
            return StartProcess(
                "dotnet", 
                $"run --project PokerGame.Console/PokerGame.Console.csproj -- {arguments}",
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