using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MSA.Foundation.ServiceManagement;
using NetMQ;
using PokerGame.Avalonia.ViewModels;
using PokerGame.Avalonia.Views;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace PokerGame.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Desktop application lifecycle only - we're focusing on Windows and macOS
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Register for application exit events for proper cleanup
                desktop.ShutdownRequested += Desktop_ShutdownRequested;
                desktop.Exit += Desktop_Exit;
                
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
                
                // Register for window closing event
                desktop.MainWindow.Closing += MainWindow_Closing;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            Console.WriteLine("MainWindow closing - preparing for cleanup...");
            PerformCleanup();
        }

        private void Desktop_Exit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            Console.WriteLine($"Application exiting with code {e.ApplicationExitCode} - performing cleanup...");
            PerformCleanup();
        }

        private void Desktop_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
        {
            Console.WriteLine("Application shutdown requested - performing cleanup...");
            
            // If we need to cancel shutdown, we could set e.Cancel = true;
            PerformCleanup();
        }
        
        // Tracks if cleanup has been completed
        private static bool _cleanupComplete = false;
        
        // Use a completely new approach with no reflection, focusing on reliable cleanup
        private void PerformCleanup()
        {
            // Only run cleanup once
            if (_cleanupComplete)
                return;
            
            _cleanupComplete = true;
            
            try
            {
                Console.WriteLine("Avalonia application cleanup starting...");
                
                // Stop services first to reduce active sockets
                try
                {
                    Console.WriteLine("Terminating all running microservices...");
                    var broker = PokerGame.Core.Messaging.BrokerManager.Instance?.CentralBroker;
                    if (broker != null)
                    {
                        var shutdownMessage = new PokerGame.Core.Messaging.NetworkMessage
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Type = PokerGame.Core.Messaging.MessageType.EndGame,
                            SenderId = "AvaloniaUI",
                            Timestamp = DateTime.UtcNow
                        };
                        broker.Publish(shutdownMessage);
                        Console.WriteLine("Shutdown signal sent to all services");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping services: {ex.Message}");
                }
                
                // Smaller delay to reduce hanging
                System.Threading.Thread.Sleep(200);
                
                // Perform ExecutionContext cleanup
                Console.WriteLine("Cleaning up all execution contexts...");
                MSA.Foundation.ServiceManagement.ExecutionContext.CleanupAll();
                
                // Directly attempt cleanup with forced parameter (immediately terminates sockets)
                try
                {
                    Console.WriteLine("Performing forced NetMQ cleanup...");
                    NetMQ.NetMQConfig.Cleanup(true);
                    Console.WriteLine("NetMQ forced cleanup completed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during NetMQ cleanup: {ex.Message}");
                }
                
                // Terminate any lingering subprocesses
                try
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName("dotnet");
                    foreach (var process in processes)
                    {
                        if (process.Id != System.Diagnostics.Process.GetCurrentProcess().Id)
                        {
                            try
                            {
                                process.Kill(true);
                                Console.WriteLine($"Terminated process ID: {process.Id}");
                            }
                            catch
                            {
                                // Ignore errors killing processes
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors in process termination
                }
                
                Console.WriteLine("Avalonia application cleanup completed");
                
                // Force immediate application exit - the nuclear option
                // This bypasses all normal shutdown procedures and immediately terminates the process
                Console.WriteLine("Forcing immediate process termination");
                
                // Set a timeout to force exit if something is blocking
                var forceExitTimer = new System.Threading.Timer(_ => 
                {
                    Console.WriteLine("Force exit timer triggered - terminating process immediately");
                    Environment.Exit(0);
                }, null, 1000, Timeout.Infinite);
                
                // First try normal shutdown
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    try 
                    {
                        desktop.Shutdown(0);
                    }
                    catch
                    {
                        // Ignore shutdown errors
                    }
                }
                
                // Then try emergency exit on a separate thread
                Task.Run(() => 
                {
                    Thread.Sleep(500);
                    Console.WriteLine("Emergency shutdown triggered");
                    Environment.Exit(0);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during application cleanup: {ex.Message}");
                Debug.WriteLine(ex);
                
                // Force exit in case of error
                Environment.Exit(0);
            }
        }
    }
}
