using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;
using PokerGame.Avalonia.ViewModels;
using System.Runtime.Versioning;
using System.Threading.Tasks;

[assembly: SupportedOSPlatform("browser")]

namespace PokerGame.Avalonia;

public class BrowserProgram
{
    public static async Task Main(string[] args)
    {
        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("app");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseReactiveUI()
            .WithInterFont();
}