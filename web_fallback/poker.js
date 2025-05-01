// Simple poker game client-side logic
document.addEventListener('DOMContentLoaded', function() {
    // Game state management
    const gameState = {
        connected: false,
        players: [],
        games: [
            { id: 'game-001', name: 'Texas Hold\'em - Table 1', players: 2, maxPlayers: 8, status: 'active' },
            { id: 'game-002', name: 'Texas Hold\'em - Table 2', players: 0, maxPlayers: 6, status: 'waiting' },
            { id: 'game-003', name: 'Texas Hold\'em - High Stakes', players: 4, maxPlayers: 6, status: 'active' }
        ],
        services: {
            'Game Engine': 'Running',
            'Card Deck Service': 'Running',
            'Lobby Service': 'Running',
            'Message Broker': 'Active'
        },
        playerName: localStorage.getItem('playerName') || ''
    };
    
    // Update connection status indicator
    function updateConnectionStatus(connected) {
        const statusElement = document.getElementById('connection-status');
        if (!statusElement) return;
        
        gameState.connected = connected;
        statusElement.className = connected ? 'status-connected' : 'status-disconnected';
        statusElement.textContent = connected ? 'Connected' : 'Disconnected';
    }
    
    // Initialize the UI
    function initializeUI() {
        updateConnectionStatus(true);
        updateServiceStatus();
        populateGamesList();
        
        // Set player name from storage if available
        const playerNameInput = document.getElementById('player-name');
        if (playerNameInput && gameState.playerName) {
            playerNameInput.value = gameState.playerName;
        }
        
        // Set up event listeners
        document.getElementById('refresh-button').addEventListener('click', function(e) {
            e.preventDefault();
            updateServiceStatus();
            populateGamesList();
        });
        
        // Player name save
        document.getElementById('player-form').addEventListener('submit', function(e) {
            e.preventDefault();
            const playerName = document.getElementById('player-name').value.trim();
            if (playerName) {
                localStorage.setItem('playerName', playerName);
                gameState.playerName = playerName;
                showMessage('Player name saved: ' + playerName);
            }
        });
        
        // Join game 
        document.getElementById('games-list').addEventListener('click', function(e) {
            if (e.target.classList.contains('join-button')) {
                e.preventDefault();
                const gameId = e.target.getAttribute('data-game-id');
                joinGame(gameId);
            }
        });
        
        // New game button
        document.getElementById('create-game-button').addEventListener('click', function(e) {
            e.preventDefault();
            createNewGame();
        });
    }
    
    // Update the service status display
    function updateServiceStatus() {
        const servicesList = document.getElementById('services-list');
        if (!servicesList) return;
        
        servicesList.innerHTML = '';
        for (const [service, status] of Object.entries(gameState.services)) {
            const statusClass = status === 'Running' || status === 'Active' ? 'status-ok' : 'status-warning';
            
            servicesList.innerHTML += `
                <li>
                    <span class="service-name">${service}:</span>
                    <span class="service-status ${statusClass}">${status}</span>
                </li>
            `;
        }
    }
    
    // Populate the games list
    function populateGamesList() {
        const gamesList = document.getElementById('games-list');
        if (!gamesList) return;
        
        gamesList.innerHTML = '';
        
        if (gameState.games.length === 0) {
            gamesList.innerHTML = '<tr><td colspan="4" class="no-games">No games available</td></tr>';
            return;
        }
        
        for (const game of gameState.games) {
            const statusClass = game.status === 'active' ? 'status-ok' : 'status-waiting';
            const buttonDisabled = !gameState.playerName || game.players >= game.maxPlayers;
            const buttonText = game.players >= game.maxPlayers ? 'Full' : 'Join';
            
            gamesList.innerHTML += `
                <tr>
                    <td>${game.name}</td>
                    <td><span class="player-count">${game.players} / ${game.maxPlayers}</span></td>
                    <td><span class="game-status ${statusClass}">${game.status}</span></td>
                    <td>
                        <button 
                            class="join-button" 
                            data-game-id="${game.id}" 
                            ${buttonDisabled ? 'disabled' : ''}
                        >${buttonText}</button>
                    </td>
                </tr>
            `;
        }
    }
    
    // Join a game
    function joinGame(gameId) {
        if (!gameState.playerName) {
            showMessage('Please set your player name first', 'error');
            return;
        }
        
        const game = gameState.games.find(g => g.id === gameId);
        if (!game) {
            showMessage('Game not found', 'error');
            return;
        }
        
        showMessage(`Joining game: ${game.name}`);
        
        // Simulate joining - in a real implementation, this would connect to the backend
        setTimeout(() => {
            showGameView(game);
        }, 1000);
    }
    
    // Create a new game
    function createNewGame() {
        if (!gameState.playerName) {
            showMessage('Please set your player name first', 'error');
            return;
        }
        
        const gameName = 'Texas Hold\'em - ' + gameState.playerName + '\'s Table';
        
        showMessage(`Creating new game: ${gameName}`);
        
        // Simulate creation - in a real implementation, this would connect to the backend
        setTimeout(() => {
            const newGame = {
                id: 'game-' + Math.floor(Math.random() * 1000),
                name: gameName,
                players: 1,
                maxPlayers: 8,
                status: 'waiting'
            };
            
            gameState.games.push(newGame);
            populateGamesList();
            
            showGameView(newGame);
        }, 1000);
    }
    
    // Show game view
    function showGameView(game) {
        document.getElementById('lobby-view').style.display = 'none';
        
        const gameView = document.getElementById('game-view');
        gameView.style.display = 'block';
        
        document.getElementById('game-name').textContent = game.name;
        document.getElementById('game-id').textContent = game.id;
        document.getElementById('player-name-display').textContent = gameState.playerName;
        
        // Set up the back button
        document.getElementById('back-to-lobby').addEventListener('click', function() {
            gameView.style.display = 'none';
            document.getElementById('lobby-view').style.display = 'block';
        });
    }
    
    // Show a message to the user
    function showMessage(message, type = 'info') {
        const messageElement = document.getElementById('message-area');
        if (!messageElement) return;
        
        messageElement.textContent = message;
        messageElement.className = 'message ' + type;
        messageElement.style.display = 'block';
        
        setTimeout(() => {
            messageElement.style.display = 'none';
        }, 5000);
    }
    
    // Start everything up
    initializeUI();
});
