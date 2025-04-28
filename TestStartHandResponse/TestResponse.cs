using System;
using System.Threading.Tasks;
using PokerGame.Core.Logging;
using PokerGame.Core.Messaging;
using PokerGame.Core.Microservices;

namespace TestStartHandResponse
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("StartHand Response Test");
            Console.WriteLine("======================");
            
            // Initialize our FileLogger with an explicit path for easier monitoring
            string logPath = "/home/runner/workspace/starthand_test.log";
            FileLogger.Initialize(logPath);
            
            Console.WriteLine($"Test initialized with log file at: {logPath}");
            
            // Test message generation
            var message = Message.Create(PokerGame.Core.Microservices.MessageType.StartHand);
            message.MessageId = Guid.NewGuid().ToString();
            message.SenderId = "test_sender";
            
            Console.WriteLine($"Created test message: {message.MessageId}");
            FileLogger.MessageTrace("TestHarness", $"Created test message with ID: {message.MessageId}");
            
            // Test response generation
            var responseMessage = Message.Create(PokerGame.Core.Microservices.MessageType.GenericResponse);
            responseMessage.MessageId = Guid.NewGuid().ToString();
            responseMessage.InResponseTo = message.MessageId;
            
            // Create response payload
            var responsePayload = new GenericResponsePayload
            {
                Success = true,
                OriginalMessageType = PokerGame.Core.Microservices.MessageType.StartHand,
                Message = "Hand started successfully. Test response."
            };
            responseMessage.SetPayload(responsePayload);
            
            Console.WriteLine($"Created test response: {responseMessage.MessageId}");
            Console.WriteLine($"Response references original: {responseMessage.InResponseTo}");
            
            FileLogger.MessageTrace("TestHarness", 
                $"Created response message with ID: {responseMessage.MessageId}, referencing: {responseMessage.InResponseTo}");
            
            // Extract and display the payload
            var extractedPayload = responseMessage.GetPayload<GenericResponsePayload>();
            if (extractedPayload != null)
            {
                Console.WriteLine("Extracted payload successfully:");
                Console.WriteLine($"- Original message type: {extractedPayload.OriginalMessageType}");
                Console.WriteLine($"- Success: {extractedPayload.Success}");
                Console.WriteLine($"- Message: {extractedPayload.Message}");
                
                FileLogger.MessageTrace("TestHarness", 
                    $"Extracted payload: Type={extractedPayload.OriginalMessageType}, " +
                    $"Success={extractedPayload.Success}, Message={extractedPayload.Message}");
            }
            else
            {
                Console.WriteLine("ERROR: Failed to extract payload");
                FileLogger.Error("TestHarness", "Failed to extract payload from response message");
            }
            
            Console.WriteLine("\nMessage/response test complete.");
            Console.WriteLine($"Check log file at: {logPath} for results");
        }
    }
}