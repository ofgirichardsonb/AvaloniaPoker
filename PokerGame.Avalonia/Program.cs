using Avalonia;
using Avalonia.ReactiveUI;
using System;
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
            // Check if we're running in a browser environment
            // If we are, the BrowserProgram.Main method will be called instead
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseReactiveUI()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
