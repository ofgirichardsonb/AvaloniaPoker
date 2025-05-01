using System;
using System.Collections.Generic;
using System.Reactive;
using Avalonia.Media;
using ReactiveUI;

namespace PokerGame.Avalonia.ViewModels
{
    /// <summary>
    /// View model for the main window/view (supports both desktop and browser)
    /// </summary>
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _contentViewModel;
        private ViewModelBase _gameView;
        private ViewModelBase _lobbyView;
        private bool _isGameActive;
        private IBrush _connectionStatusColor;
        
        /// <summary>
        /// Creates a new instance of the MainWindowViewModel
        /// </summary>
        public MainWindowViewModel()
        {
            // Create views
            _lobbyView = new LobbyViewModel();
            _gameView = new GameViewModel();
            
            // Start with the game view model for desktop or lobby for browser
            _contentViewModel = _gameView;
            
            // Default state
            _isGameActive = false;
            _connectionStatusColor = new SolidColorBrush(Colors.Red);
            
            // Try to connect to the server
            TryConnectToServer();
        }

        /// <summary>
        /// Attempts to connect to the poker game server
        /// </summary>
        private async void TryConnectToServer()
        {
            // TODO: Implement actual connection logic
            await System.Threading.Tasks.Task.Delay(1000);
            
            // Simulate successful connection for now
            ConnectionStatusColor = new SolidColorBrush(Colors.Green);
        }
        
        /// <summary>
        /// Gets or sets the current content view model
        /// </summary>
        public ViewModelBase ContentViewModel
        {
            get => _contentViewModel;
            set 
            {
                this.RaiseAndSetIfChanged(ref _contentViewModel, value);
            }
        }
        
        /// <summary>
        /// Gets the game view model
        /// </summary>
        public ViewModelBase GameView => _gameView;
        
        /// <summary>
        /// Gets the lobby view model
        /// </summary>
        public ViewModelBase LobbyView => _lobbyView;
        
        /// <summary>
        /// Gets or sets whether a game is currently active
        /// </summary>
        public bool IsGameActive
        {
            get => _isGameActive;
            set => this.RaiseAndSetIfChanged(ref _isGameActive, value);
        }
        
        /// <summary>
        /// Gets or sets the connection status color
        /// </summary>
        public IBrush ConnectionStatusColor
        {
            get => _connectionStatusColor;
            set => this.RaiseAndSetIfChanged(ref _connectionStatusColor, value);
        }
    }
}
