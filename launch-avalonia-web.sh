#!/bin/bash

echo "Starting web server for Avalonia UI..."
cd /home/runner/workspace

# Create a fallback web page if WebAssembly can't be built
mkdir -p web_fallback
cat > web_fallback/poker.js << EOF
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
            
            servicesList.innerHTML += \`
                <li>
                    <span class="service-name">\${service}:</span>
                    <span class="service-status \${statusClass}">\${status}</span>
                </li>
            \`;
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
            
            gamesList.innerHTML += \`
                <tr>
                    <td>\${game.name}</td>
                    <td><span class="player-count">\${game.players} / \${game.maxPlayers}</span></td>
                    <td><span class="game-status \${statusClass}">\${game.status}</span></td>
                    <td>
                        <button 
                            class="join-button" 
                            data-game-id="\${game.id}" 
                            \${buttonDisabled ? 'disabled' : ''}
                        >\${buttonText}</button>
                    </td>
                </tr>
            \`;
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
        
        showMessage(\`Joining game: \${game.name}\`);
        
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
        
        showMessage(\`Creating new game: \${gameName}\`);
        
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
EOF

cat > web_fallback/index.html << EOF
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Poker Game Web Edition</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f0f0f0; }
        .container { max-width: 800px; margin: 0 auto; background: white; padding: 20px; border-radius: 5px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #333; text-align: center; }
        h2 { color: #444; margin-top: 20px; }
        .card { background: #f9f9f9; border-left: 4px solid #007bff; padding: 15px; margin-bottom: 20px; }
        .status-banner { text-align: center; margin: 20px 0; padding: 10px; background: #ffe0e0; border-radius: 4px; }
        .status-indicator { display: flex; align-items: center; justify-content: flex-end; padding: 10px 0; }
        .status-connected, .status-disconnected, .status-ok, .status-warning { 
            display: inline-block; 
            padding: 3px 8px; 
            border-radius: 10px; 
            font-size: 12px; 
            font-weight: bold;
            margin-left: 10px;
        }
        .status-connected, .status-ok { background: #d4f7d4; color: #0a8a0a; }
        .status-disconnected, .status-warning { background: #f7d4d4; color: #8a0a0a; }
        .button { display: inline-block; background: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px; margin-top: 10px; border: none; cursor: pointer; }
        .button:hover { background: #0056b3; }
        .button:disabled { background: #cccccc; cursor: not-allowed; }
        .center { text-align: center; }
        
        .game-table {
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
        }
        .game-table th, .game-table td {
            padding: 10px;
            text-align: left;
            border-bottom: 1px solid #ddd;
        }
        .game-table th {
            background-color: #f2f2f2;
            font-weight: bold;
        }
        .game-table tr:hover {
            background-color: #f5f5f5;
        }
        .join-button {
            background: #28a745;
            color: white;
            border: none;
            padding: 5px 10px;
            border-radius: 3px;
            cursor: pointer;
        }
        .join-button:hover {
            background: #218838;
        }
        .join-button:disabled {
            background: #cccccc;
            cursor: not-allowed;
        }
        
        .form-group {
            margin-bottom: 15px;
        }
        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: bold;
        }
        .form-group input {
            width: 100%;
            padding: 8px;
            border: 1px solid #ddd;
            border-radius: 4px;
        }
        
        .services-list {
            list-style: none;
            padding: 0;
        }
        .services-list li {
            padding: 5px 0;
            display: flex;
            justify-content: space-between;
        }
        .service-name {
            font-weight: bold;
        }
        
        .message {
            padding: 10px;
            margin: 10px 0;
            border-radius: 4px;
            display: none;
        }
        .message.info {
            background-color: #d4edff;
            color: #0058a8;
        }
        .message.error {
            background-color: #ffd4d4;
            color: #a80000;
        }
        
        .view {
            display: block;
        }
        #game-view {
            display: none;
        }
        
        .game-board {
            margin-top: 20px;
            background: #2c7522;
            border-radius: 10px;
            padding: 20px;
            color: white;
            position: relative;
        }
        .player-area {
            text-align: center;
            margin-top: 15px;
        }
        .poker-card {
            display: inline-block;
            width: 50px;
            height: 70px;
            background: white;
            border-radius: 5px;
            margin: 0 5px;
            color: black;
            text-align: center;
            line-height: 70px;
            font-weight: bold;
            box-shadow: 0 2px 5px rgba(0,0,0,0.3);
        }
        .community-cards {
            display: flex;
            justify-content: center;
            margin: 20px 0;
        }
        .player-actions {
            margin-top: 20px;
            display: flex;
            justify-content: center;
            gap: 10px;
        }
    </style>
    <script src="poker.js"></script>
</head>
<body>
    <div class="container">
        <div class="status-indicator">
            Status: <span id="connection-status" class="status-disconnected">Disconnected</span>
        </div>
        
        <h1>Poker Game Web Edition</h1>
        
        <div class="status-banner">
            This is a temporary web interface while WebAssembly support is being configured.
        </div>
        
        <div id="message-area" class="message info"></div>
        
        <div id="lobby-view" class="view">
            <div class="card">
                <h2>Player Information</h2>
                <form id="player-form">
                    <div class="form-group">
                        <label for="player-name">Your Name:</label>
                        <input type="text" id="player-name" placeholder="Enter your name" required>
                    </div>
                    <button type="submit" class="button">Save</button>
                </form>
            </div>
            
            <div class="card">
                <h2>Available Games</h2>
                <table class="game-table">
                    <thead>
                        <tr>
                            <th>Game</th>
                            <th>Players</th>
                            <th>Status</th>
                            <th>Action</th>
                        </tr>
                    </thead>
                    <tbody id="games-list">
                        <!-- Games will be populated here -->
                    </tbody>
                </table>
                <div class="center">
                    <button id="create-game-button" class="button">Create New Game</button>
                </div>
            </div>
            
            <div class="card">
                <h2>Server Information</h2>
                <ul class="services-list" id="services-list">
                    <!-- Services will be populated here -->
                </ul>
            </div>
            
            <div class="center">
                <button id="refresh-button" class="button">Refresh Status</button>
            </div>
        </div>
        
        <div id="game-view" class="view">
            <div class="card">
                <h2>Game: <span id="game-name"></span></h2>
                <p>Game ID: <span id="game-id"></span></p>
                <p>Playing as: <span id="player-name-display"></span></p>
                <button id="back-to-lobby" class="button">Back to Lobby</button>
            </div>
            
            <div class="game-board">
                <h3>Texas Hold'em</h3>
                
                <div class="community-cards">
                    <div class="poker-card">A♠</div>
                    <div class="poker-card">K♥</div>
                    <div class="poker-card">Q♦</div>
                    <div class="poker-card">?</div>
                    <div class="poker-card">?</div>
                </div>
                
                <div class="player-area">
                    <h4>Your Cards</h4>
                    <div class="poker-card">J♣</div>
                    <div class="poker-card">10♠</div>
                </div>
                
                <div class="player-actions">
                    <button class="button">Fold</button>
                    <button class="button">Check</button>
                    <button class="button">Bet</button>
                </div>
            </div>
        </div>
    </div>
</body>
</html>
EOF

# Try to install WebAssembly workload
echo "Attempting to install WebAssembly workload..."
dotnet workload install wasm-tools || echo "Failed to install wasm-tools workload, continuing with fallback approach"

# Try to build the Avalonia application for browser
echo "Building Avalonia UI for browser..."
dotnet publish PokerGame.Avalonia/PokerGame.Avalonia.csproj -c Debug -r browser-wasm --self-contained || echo "Failed to build WebAssembly version, using fallback"

# Start the services in the background
echo "Starting backend services..."
dotnet run --project PokerGame.Launcher/PokerGame.Launcher.csproj --configuration Debug -- start-services --port-offset 0 --verbose &
SERVICES_PID=$!

# Give services time to start
sleep 3

# Set up the web server for Avalonia UI or fallback
echo "Setting up web server..."
if [ -d "/home/runner/workspace/PokerGame.Avalonia/bin/Debug/net8.0/browser-wasm/AppBundle" ]; then
  echo "WebAssembly build found, serving that"
  cd /home/runner/workspace/PokerGame.Avalonia/bin/Debug/net8.0/browser-wasm/AppBundle
else
  echo "Using fallback web page"
  cd /home/runner/workspace/web_fallback
fi

# Start HTTP server
python3 -m http.server 5000 &
HTTP_SERVER_PID=$!

echo "Web server running at http://localhost:5000"
echo "Press Ctrl+C to stop all services"

# Wait for Ctrl+C
trap "echo 'Shutting down...'; kill $SERVICES_PID; kill $HTTP_SERVER_PID; exit 0" INT TERM

# Keep the script running
while true; do
    sleep 1
done