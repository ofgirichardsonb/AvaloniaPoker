using System;
using System.Threading;
using System.Threading.Tasks;
using PokerGame.Core.Microservices;
using PokerGame.Core.Messaging;
using PokerGame.Core.ServiceManagement;

class TestStartHandResponse
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("========== StartHand Response Test Program ==========");
        
        // Create execution contexts for our services
        Console.WriteLine("Creating execution contexts for services...");
        var gameEngineContext = new ExecutionContext("static_game_engine_service", "Game Engine Test", 5555, 5556);
        var consoleUIContext = new ExecutionContext("static_console_ui_service", "Console UI Test", 5557, 5558);
        
        // Set up services
        Console.WriteLine("Creating service instances...");
        var gameEngine = new GameEngineService(gameEngineContext);
        var consoleUI = new ConsoleUIService(5557, 5558);
        
        // Start services
        Console.WriteLine("Starting services...");
        gameEngine.Start();
        consoleUI.Start();
        
        Console.WriteLine("Waiting for services to initialize...");
        await Task.Delay(2000);
        
        // Create StartHand message
        Console.WriteLine("Creating StartHand message...");
        var startHandMessage = Message.Create(MessageType.StartHand);
        startHandMessage.SenderId = consoleUI.ServiceId;
        startHandMessage.ReceiverId = gameEngine.ServiceId;
        startHandMessage.MessageId = Guid.NewGuid().ToString();
        
        // Send the message
        Console.WriteLine($"Sending StartHand message with ID: {startHandMessage.MessageId}");
        consoleUI.SendTo(startHandMessage, gameEngine.ServiceId);
        
        // Wait for response
        Console.WriteLine("Waiting for response...");
        await Task.Delay(5000);
        
        // Clean up
        Console.WriteLine("Test complete. Shutting down services...");
        gameEngine.Dispose();
        consoleUI.Dispose();
    }
}