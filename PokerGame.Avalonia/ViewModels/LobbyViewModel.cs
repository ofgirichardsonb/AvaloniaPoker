using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace PokerGame.Avalonia.ViewModels
{
    /// <summary>
    /// View model for the lobby screen
    /// </summary>
    public class LobbyViewModel : ViewModelBase
    {
        private string _playerName;
        private ObservableCollection<string> _availableGames;
        private string _selectedGame;
        private ReactiveCommand<Unit, Unit> _joinGameCommand;
        private ReactiveCommand<Unit, Unit> _createGameCommand;
        
        /// <summary>
        /// Creates a new instance of the LobbyViewModel
        /// </summary>
        public LobbyViewModel()
        {
            _playerName = "Player";
            _availableGames = new ObservableCollection<string>
            {
                "Texas Hold'em - Table 1",
                "Texas Hold'em - Table 2",
                "Texas Hold'em - High Stakes"
            };
            
            // Initialize selected game to avoid null warning
            _selectedGame = _availableGames.FirstOrDefault() ?? string.Empty;
            
            _joinGameCommand = ReactiveCommand.Create(JoinGame);
            _createGameCommand = ReactiveCommand.Create(CreateGame);
        }
        
        /// <summary>
        /// Gets or sets the player's name
        /// </summary>
        public string PlayerName
        {
            get => _playerName;
            set => this.RaiseAndSetIfChanged(ref _playerName, value);
        }
        
        /// <summary>
        /// Gets the collection of available games
        /// </summary>
        public ObservableCollection<string> AvailableGames => _availableGames;
        
        /// <summary>
        /// Gets or sets the selected game
        /// </summary>
        public string SelectedGame
        {
            get => _selectedGame;
            set => this.RaiseAndSetIfChanged(ref _selectedGame, value);
        }
        
        /// <summary>
        /// Gets the command to join a game
        /// </summary>
        public ReactiveCommand<Unit, Unit> JoinGameCommand => _joinGameCommand;
        
        /// <summary>
        /// Gets the command to create a game
        /// </summary>
        public ReactiveCommand<Unit, Unit> CreateGameCommand => _createGameCommand;
        
        /// <summary>
        /// Joins the selected game
        /// </summary>
        private void JoinGame()
        {
            // TODO: Implement joining a game
            Console.WriteLine($"Joining game: {SelectedGame} as {PlayerName}");
        }
        
        /// <summary>
        /// Creates a new game
        /// </summary>
        private void CreateGame()
        {
            // TODO: Implement creating a game
            Console.WriteLine($"Creating new game as {PlayerName}");
        }
    }
}