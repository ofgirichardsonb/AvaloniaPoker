using System;
using System.Collections.Generic;
using System.Linq;
using PokerGame.Core.Models;
using Hand = PokerGame.Core.Game.Hand;
using HandEvaluator = PokerGame.Core.Game.HandEvaluator;
using PokerGame.Core.Interfaces;

namespace PokerGame.Core.Game
{
    /// <summary>
    /// The main engine for the poker game that manages game state and rules
    /// </summary>
    public class PokerGameEngine
    {
        private readonly IPokerGameUI _ui;
        private readonly Deck _deck = new Deck();
        private readonly List<Player> _players = new List<Player>();
        private readonly List<Card> _communityCards = new List<Card>();
        private GameState _gameState;
        
        private int _dealerPosition = -1;
        private int _currentPlayerIndex = -1;
        private int _smallBlind = 5;
        private int _bigBlind = 10;
        private int _pot = 0;
        private int _currentBet = 0;
        
        /// <summary>
        /// Creates a new poker game engine
        /// </summary>
        /// <param name="ui">The UI implementation to use for player interaction</param>
        public PokerGameEngine(IPokerGameUI ui)
        {
            _ui = ui;
            _gameState = GameState.Setup;
        }
        
        /// <summary>
        /// Gets the current state of the game
        /// </summary>
        public GameState State => _gameState;
        
        /// <summary>
        /// Gets the current pot size
        /// </summary>
        public int Pot => _pot;
        
        /// <summary>
        /// Gets the current minimum bet amount
        /// </summary>
        public int CurrentBet => _currentBet;
        
        /// <summary>
        /// Gets the community cards
        /// </summary>
        public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();
        
        /// <summary>
        /// Adds a card to the community cards
        /// </summary>
        /// <param name="card">The card to add</param>
        public void AddCommunityCard(Card card)
        {
            _communityCards.Add(card);
        }
        
        /// <summary>
        /// Moves the game to the next round in the Texas Hold'em sequence (PreFlop -> Flop -> Turn -> River -> Showdown)
        /// </summary>
        public void MoveToNextRound()
        {
            switch (_gameState)
            {
                case GameState.Setup:
                    _gameState = GameState.PreFlop;
                    break;
                case GameState.PreFlop:
                    _gameState = GameState.Flop;
                    break;
                case GameState.Flop:
                    _gameState = GameState.Turn;
                    break;
                case GameState.Turn:
                    _gameState = GameState.River;
                    break;
                case GameState.River:
                    _gameState = GameState.Showdown;
                    break;
                case GameState.Showdown:
                    _gameState = GameState.HandComplete;
                    break;
                case GameState.HandComplete:
                    // Already at the end of the hand
                    break;
            }
        }
        
        /// <summary>
        /// Resets the game state for the next hand
        /// </summary>
        public void ResetForNextHand()
        {
            // Reset game state
            _gameState = GameState.Setup;
            
            // Clear community cards
            _communityCards.Clear();
            
            // Reset pot and bets
            _pot = 0;
            _currentBet = 0;
            
            // Reset player statuses
            foreach (var player in _players)
            {
                player.ResetForNewHand();
            }
            
            // Move dealer position
            if (_players.Count > 0)
            {
                _dealerPosition = (_dealerPosition + 1) % _players.Count;
            }
        }
        
        /// <summary>
        /// Clears all community cards
        /// </summary>
        public void ClearCommunityCards()
        {
            _communityCards.Clear();
        }
        
        /// <summary>
        /// Gets the players in the game
        /// </summary>
        public IReadOnlyList<Player> Players => _players.AsReadOnly();
        
        /// <summary>
        /// Gets the current player
        /// </summary>
        public Player? CurrentPlayer => _currentPlayerIndex >= 0 && _currentPlayerIndex < _players.Count 
                                      ? _players[_currentPlayerIndex] 
                                      : null;
        
        /// <summary>
        /// Starts a new game with the specified players
        /// </summary>
        /// <param name="playerNames">The names of the players</param>
        /// <param name="startingChips">The starting chips for each player</param>
        public void StartGame(string[] playerNames, int startingChips = 1000)
        {
            if (playerNames.Length < 2)
                throw new ArgumentException("At least 2 players are required", nameof(playerNames));
                
            _players.Clear();
            foreach (var name in playerNames)
            {
                _players.Add(new Player(Guid.NewGuid().ToString(), name, startingChips));
            }
            
            _ui.ShowMessage("Game started! Let's play Texas Hold'em Poker.");
            _gameState = GameState.WaitingToStart;
        }
        
        /// <summary>
        /// Starts a new hand of poker
        /// </summary>
        public void StartHand()
        {
            Console.WriteLine($"PokerGameEngine.StartHand() called with current state: {_gameState}");
            
            // Reset game state
            _communityCards.Clear();
            _pot = 0;
            _currentBet = 0;
            
            foreach (var player in _players)
            {
                player.ResetForNewHand();
            }
            
            // Move dealer button to next player (make sure we have players)
            if (_players.Count > 0)
            {
                _dealerPosition = (_dealerPosition + 1) % _players.Count;
            }
            else
            {
                Console.WriteLine("ERROR: No players registered yet. Cannot start hand.");
                return;
            }
            
            // In microservice mode, we might already have cards dealt externally
            // So only use the internal deck if needed
            bool needToUseInternalDeck = _players.All(p => p.HoleCards.Count == 0);
            
            if (needToUseInternalDeck)
            {
                // Shuffle the deck
                _deck.Reset();
                _deck.Shuffle();
                
                // Deal hole cards to each player
                for (int i = 0; i < 2; i++) // Two cards per player
                {
                    foreach (var player in _players)
                    {
                        Card? card = _deck.DealCard();
                        if (card != null)
                            player.HoleCards.Add(card);
                    }
                }
            }
            
            _ui.ShowMessage("New hand started. Hole cards dealt.");
            _ui.UpdateGameState(this);
            
            // Post blinds (need at least 2 players)
            if (_players.Count < 2)
            {
                Console.WriteLine("ERROR: Need at least 2 players to play poker.");
                return;
            }
            
            int smallBlindPos = (_dealerPosition + 1) % _players.Count;
            int bigBlindPos = (_dealerPosition + 2) % _players.Count;
            
            Player smallBlindPlayer = _players[smallBlindPos];
            Player bigBlindPlayer = _players[bigBlindPos];
            
            int smallBlindAmount = smallBlindPlayer.PlaceBet(_smallBlind);
            _ui.ShowMessage($"{smallBlindPlayer.Name} posts small blind: {smallBlindAmount}");
            
            int bigBlindAmount = bigBlindPlayer.PlaceBet(_bigBlind);
            _ui.ShowMessage($"{bigBlindPlayer.Name} posts big blind: {bigBlindAmount}");
            
            _currentBet = bigBlindAmount;
            
            // Start with player after big blind
            _currentPlayerIndex = (bigBlindPos + 1) % _players.Count;
            
            _gameState = GameState.PreFlop;
            Console.WriteLine($"Game state changed to: {_gameState}");
            _ui.UpdateGameState(this);
            
            // Start the first betting round
            ProcessBettingRound();
        }
        
        /// <summary>
        /// Processes a player's action
        /// </summary>
        /// <param name="action">The action to take (fold, check, call, raise)</param>
        /// <param name="betAmount">The bet amount (for raise actions)</param>
        public void ProcessPlayerAction(string action, int betAmount = 0)
        {
            Player player = _players[_currentPlayerIndex];
            
            switch (action.ToLower())
            {
                case "fold":
                    player.Fold();
                    _ui.ShowMessage($"{player.Name} folds.");
                    break;
                    
                case "check":
                    if (_currentBet > player.CurrentBet)
                    {
                        // Can't check if there's a bet to call
                        _ui.ShowMessage("You can't check, you must call or fold.");
                        return;
                    }
                    _ui.ShowMessage($"{player.Name} checks.");
                    break;
                    
                case "call":
                    {
                        int callAmount = _currentBet - player.CurrentBet;
                        int actualBet = player.PlaceBet(callAmount);
                        _ui.ShowMessage($"{player.Name} calls {actualBet}.");
                    }
                    break;
                    
                case "raise":
                    {
                        int minRaise = _currentBet + _bigBlind;
                        if (betAmount < minRaise)
                        {
                            _ui.ShowMessage($"Minimum raise is {minRaise}.");
                            return;
                        }
                        
                        int raiseAmount = betAmount - player.CurrentBet;
                        int actualBet = player.PlaceBet(raiseAmount);
                        _currentBet = player.CurrentBet;
                        _ui.ShowMessage($"{player.Name} raises to {player.CurrentBet}.");
                    }
                    break;
                    
                default:
                    _ui.ShowMessage("Invalid action. Try fold, check, call, or raise.");
                    return;
            }
            
            // Move to next player
            MoveToNextPlayer();
            _ui.UpdateGameState(this);
            
            // Check if betting round is complete
            if (IsBettingRoundComplete())
            {
                AdvanceGameState();
            }
        }
        
        /// <summary>
        /// Processes an entire betting round
        /// </summary>
        private void ProcessBettingRound()
        {
            // Keep going until the betting round is complete
            while (!IsBettingRoundComplete())
            {
                Player player = _players[_currentPlayerIndex];
                
                // Skip players who have folded or are all-in
                if (!player.IsActive || player.IsAllIn)
                {
                    MoveToNextPlayer();
                    continue;
                }
                
                // Get action from the current player via the UI
                _ui.GetPlayerAction(player, this);
                
                // Note: The action processing continues in ProcessPlayerAction
                // which will be called by the UI after getting player input
                return;
            }
            
            // When betting round is complete, advance the game state
            AdvanceGameState();
        }
        
        /// <summary>
        /// Advances the game state to the next stage
        /// </summary>
        private void AdvanceGameState()
        {
            // First, collect bets into the pot
            foreach (var player in _players)
            {
                _pot += player.CurrentBet;
                player.ResetBetForNewRound();
            }
            
            _currentBet = 0;
            
            // Check if only one player remains
            if (GetActivePlayers().Count <= 1)
            {
                EndHand();
                return;
            }
            
            // Advance game state based on current state
            switch (_gameState)
            {
                case GameState.PreFlop:
                    DealFlop();
                    break;
                    
                case GameState.Flop:
                    DealTurn();
                    break;
                    
                case GameState.Turn:
                    DealRiver();
                    break;
                    
                case GameState.River:
                    EndHand();
                    break;
            }
        }
        
        /// <summary>
        /// Deals the flop (first three community cards)
        /// </summary>
        private void DealFlop()
        {
            // Burn a card
            _deck.DealCard();
            
            // Deal the flop (3 cards)
            for (int i = 0; i < 3; i++)
            {
                Card? card = _deck.DealCard();
                if (card != null)
                    _communityCards.Add(card);
            }
            
            _gameState = GameState.Flop;
            _ui.ShowMessage("Flop dealt.");
            _ui.UpdateGameState(this);
            
            // Reset for new betting round
            _currentPlayerIndex = (_dealerPosition + 1) % _players.Count;
            EnsureCurrentPlayerIsActive();
            
            ProcessBettingRound();
        }
        
        /// <summary>
        /// Deals the turn (fourth community card)
        /// </summary>
        private void DealTurn()
        {
            // Burn a card
            _deck.DealCard();
            
            // Deal the turn (1 card)
            Card? card = _deck.DealCard();
            if (card != null)
                _communityCards.Add(card);
            
            _gameState = GameState.Turn;
            _ui.ShowMessage("Turn dealt.");
            _ui.UpdateGameState(this);
            
            // Reset for new betting round
            _currentPlayerIndex = (_dealerPosition + 1) % _players.Count;
            EnsureCurrentPlayerIsActive();
            
            ProcessBettingRound();
        }
        
        /// <summary>
        /// Deals the river (fifth community card)
        /// </summary>
        private void DealRiver()
        {
            // Burn a card
            _deck.DealCard();
            
            // Deal the river (1 card)
            Card? card = _deck.DealCard();
            if (card != null)
                _communityCards.Add(card);
            
            _gameState = GameState.River;
            _ui.ShowMessage("River dealt.");
            _ui.UpdateGameState(this);
            
            // Reset for new betting round
            _currentPlayerIndex = (_dealerPosition + 1) % _players.Count;
            EnsureCurrentPlayerIsActive();
            
            ProcessBettingRound();
        }
        
        /// <summary>
        /// Ends the current hand, determines winners, and distributes the pot
        /// </summary>
        private void EndHand()
        {
            var activePlayers = GetActivePlayers();
            
            // If only one player remains, they win by default
            if (activePlayers.Count == 1)
            {
                Player winner = activePlayers[0];
                winner.AwardChips(_pot);
                _ui.ShowMessage($"{winner.Name} wins {_pot} chips.");
            }
            else
            {
                // Evaluate hands for all active players
                var playerHandEvaluations = new Dictionary<Player, Hand>();
                foreach (var player in activePlayers)
                {
                    var bestHand = HandEvaluator.EvaluateBestHand(player.HoleCards, _communityCards, player.Id);
                    playerHandEvaluations[player] = bestHand;
                    _ui.ShowMessage($"{player.Name}: {bestHand.Description}");
                }
                
                // Find the winner(s)
                List<Player> winners = FindWinners(playerHandEvaluations);
                
                // Distribute the pot
                int winAmount = _pot / winners.Count;
                foreach (var winner in winners)
                {
                    winner.AwardChips(winAmount);
                    _ui.ShowMessage($"{winner.Name} wins {winAmount} chips with {playerHandEvaluations[winner].Description}.");
                }
            }
            
            _pot = 0;
            _gameState = GameState.HandComplete;
            _ui.UpdateGameState(this);
        }
        
        /// <summary>
        /// Finds the winner(s) from the dictionary of players and their evaluated hands
        /// </summary>
        private List<Player> FindWinners(Dictionary<Player, Hand> playerHands)
        {
            List<Player> winners = new List<Player>();
            Player? bestPlayer = null;
            
            foreach (var kvp in playerHands)
            {
                var player = kvp.Key;
                var hand = kvp.Value;
                
                if (bestPlayer == null || 
                    CompareHands(hand, playerHands[bestPlayer]) > 0)
                {
                    bestPlayer = player;
                    winners.Clear();
                    winners.Add(player);
                }
                else if (bestPlayer != null && 
                        CompareHands(hand, playerHands[bestPlayer]) == 0)
                {
                    winners.Add(player);
                }
            }
            
            return winners;
        }
        
        /// <summary>
        /// Finds the winner(s) from the given list of players (deprecated, for compatibility)
        /// </summary>
        private List<Player> FindWinners(List<Player> players)
        {
            // This is kept for compatibility with existing code
            // It should evaluate the hands on the fly and find winners
            var playerHands = new Dictionary<Player, Hand>();
            foreach (var player in players)
            {
                var bestHand = HandEvaluator.EvaluateBestHand(player.HoleCards, _communityCards, player.Id);
                playerHands[player] = bestHand;
            }
            
            return FindWinners(playerHands);
        }
        
        /// <summary>
        /// Checks if the current betting round is complete
        /// </summary>
        private bool IsBettingRoundComplete()
        {
            var activePlayers = GetActivePlayers();
            
            // If only one player remains, betting is complete
            if (activePlayers.Count <= 1)
                return true;
                
            // Check if all active players have bet the same amount or are all-in
            int targetBet = _currentBet;
            foreach (var player in activePlayers)
            {
                if (player.CurrentBet < targetBet && !player.IsAllIn)
                {
                    return false;
                }
            }
            
            // Check if everyone has had a chance to act since the last raise
            int startIndex = _currentPlayerIndex;
            int index = startIndex;
            
            do
            {
                Player player = _players[index];
                if (player.IsActive && !player.IsAllIn && player.CurrentBet < targetBet)
                {
                    return false;
                }
                
                index = (index + 1) % _players.Count;
            } while (index != startIndex);
            
            return true;
        }
        
        /// <summary>
        /// Moves to the next active player
        /// </summary>
        private void MoveToNextPlayer()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
            EnsureCurrentPlayerIsActive();
        }
        
        /// <summary>
        /// Ensures the current player is active (not folded)
        /// </summary>
        private void EnsureCurrentPlayerIsActive()
        {
            int startIndex = _currentPlayerIndex;
            
            while (!_players[_currentPlayerIndex].IsActive)
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                
                // If we've gone through all players and none are active, break
                if (_currentPlayerIndex == startIndex)
                    break;
            }
        }
        
        /// <summary>
        /// Gets the list of active players (not folded)
        /// </summary>
        private List<Player> GetActivePlayers()
        {
            return _players.Where(p => p.IsActive).ToList();
        }
        
        /// <summary>
        /// Compares two poker hands
        /// </summary>
        /// <param name="hand1">The first hand</param>
        /// <param name="hand2">The second hand</param>
        /// <returns>A positive value if hand1 is better, 0 if they're equal, a negative value if hand2 is better</returns>
        private int CompareHands(Hand hand1, Hand hand2)
        {
            // Use the Hand's built-in comparison functionality
            return hand1.CompareTo(hand2);
        }
    }
}
