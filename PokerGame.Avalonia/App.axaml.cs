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
        
        private void PerformCleanup()
        {
            try
            {
                Console.WriteLine("Avalonia application cleanup starting...");
                
                // Kill all running services first
                try
                {
                    Console.WriteLine("Terminating all running microservices...");
                    // Try to use the MicroserviceManager to stop services if available
                    var broker = PokerGame.Core.Messaging.BrokerManager.Instance?.CentralBroker;
                    if (broker != null)
                    {
                        // Create shutdown message using the NetworkMessage from PokerGame.Core.Messaging
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
                
                // Small delay to allow services to process shutdown messages
                System.Threading.Thread.Sleep(500);
                
                // Perform all necessary ExecutionContext cleanup
                Console.WriteLine("Cleaning up all execution contexts...");
                MSA.Foundation.ServiceManagement.ExecutionContext.CleanupAll();
                
                // We need to be more aggressive with NetMQ cleanup to avoid deadlocks
                try
                {
                    Console.WriteLine("Performing direct NetMQ cleanup...");
                    
                    // Force a very quick shutdown without waiting for socket closure
                    NetMQ.NetMQConfig.Cleanup(false);
                    
                    // Close sockets in the helper class directly
                    var closeSockets = typeof(PokerGame.Core.Microservices.NetMQContextHelper)
                        .GetMethod("PerformCleanup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    
                    if (closeSockets != null)
                    {
                        closeSockets.Invoke(null, null);
                    }
                    
                    Console.WriteLine("NetMQ direct cleanup completed");
                } 
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during NetMQ cleanup: {ex.Message}");
                    
                    // As a fallback, try forcing cleanup
                    try
                    {
                        NetMQConfig.Cleanup(true);
                    }
                    catch
                    {
                        // Ignore errors in forced cleanup
                    }
                }
                
                // Forcibly kill any remaining processes as a last resort
                try
                {
                    // Force terminate any remaining child processes
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
                
                Console.WriteLine("Avalonia application cleanup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during application cleanup: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }
    }
}
