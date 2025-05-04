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
        private async void PerformCleanup()
        {
            // Only run cleanup once
            if (_cleanupComplete)
                return;
            
            _cleanupComplete = true;
            
            try
            {
                Console.WriteLine("Avalonia application cleanup starting...");
                
                // Stop services first via our centralized ShutdownCoordinator
                try
                {
                    // Step 1: Send the EndGame message to all services
                    Console.WriteLine("Terminating all running microservices via message broker...");
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
                    
                    // Step 2: Use the global ShutdownCoordinator to handle the rest
                    Console.WriteLine("Initiating coordinated shutdown sequence...");
                    var coordinator = MSA.Foundation.ServiceManagement.ShutdownCoordinator.Instance;
                    
                    // Allow a short time for services to process the EndGame message
                    await System.Threading.Tasks.Task.Delay(300);
                    
                    // Instruct the ShutdownCoordinator to perform the shutdown
                    try
                    {
                        var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await coordinator.TriggerShutdownAsync(cts.Token);
                        Console.WriteLine("Coordinated shutdown completed successfully");
                    }
                    catch (System.Threading.Tasks.TaskCanceledException)
                    {
                        Console.WriteLine("Coordinated shutdown timed out - proceeding with forced cleanup");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during coordinated shutdown: {ex.Message}");
                    }
                    
                    // Execute context cleanup is used by service management
                    Console.WriteLine("Cleaning up all execution contexts...");
                    MSA.Foundation.ServiceManagement.ExecutionContext.CleanupAll();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initiating coordinated shutdown: {ex.Message}");
                    
                    // Attempt direct NetMQ cleanup as a fallback
                    try
                    {
                        var shutdownHandler = PokerGame.Core.Microservices.NetMQShutdownHandler.Instance;
                        Console.WriteLine("Attempting direct NetMQ cleanup via shutdown handler...");
                        
                        // Give the system 2 seconds to complete cleanup
                        var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await shutdownHandler.ShutdownAsync(cts.Token);
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"Error during direct NetMQ cleanup: {innerEx.Message}");
                    }
                }
                
                // After coordinated shutdown completes, exit the application
                Console.WriteLine("Avalonia application cleanup completed");
                
                // Give NetMQ a chance to clean up on its own
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    try 
                    {
                        // Set a task to force exit if normal shutdown doesn't complete in time
                        _ = System.Threading.Tasks.Task.Run(async () => {
                            await System.Threading.Tasks.Task.Delay(2000);
                            // If we're still running after 2 seconds, force exit
                            Environment.Exit(0);
                        });
                        
                        // Normal shutdown
                        desktop.Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error shutting down: {ex.Message}");
                        Environment.Exit(0);
                    }
                }
                else
                {
                    // Force exit if we can't use desktop shutdown
                    Environment.Exit(0);
                }
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
