using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PokerGame.Avalonia.Views
{
    public partial class LobbyView : UserControl
    {
        public LobbyView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}