using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MSA.Foundation.ServiceManagement;
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
                
                // Perform all necessary cleanup
                MSA.Foundation.ServiceManagement.ExecutionContext.CleanupAll();
                
                // If we have any other custom cleanup, do it here
                
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
