using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PokerGame.Avalonia
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => 
            {
                Console.WriteLine("Process exit detected, cleaning up...");
                // Try to do any necessary cleanup here
                MSA.Foundation.ServiceManagement.ExecutionContext.CleanupAll();
            };
            
            try 
            {
                var builder = BuildAvaloniaApp();
                
                // Use platform detection to select appropriate backend
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine("Windows platform detected");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Console.WriteLine("macOS platform detected");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Console.WriteLine("Linux platform detected");
                }
                
                // Start with the appropriate lifetime
                builder.StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting application: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                // On fatal error, ensure we clean up any lingering processes
                try 
                {
                    MSA.Foundation.ServiceManagement.ExecutionContext.CleanupAll();
                }
                catch 
                {
                    // Last-ditch effort - ignore any errors here
                }
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseReactiveUI()
                .WithInterFont()
                .LogToTrace();
    }
}
