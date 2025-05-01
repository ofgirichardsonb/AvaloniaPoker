using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PokerGame.Avalonia.ViewModels;
using PokerGame.Avalonia.Views;
using System.Runtime.InteropServices;

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
            bool isBrowser = RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));
            
            // Desktop application lifecycle
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
            // Browser application lifecycle
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
            {
                singleViewLifetime.MainView = new MainView
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
