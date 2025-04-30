using System;
using System.Threading.Tasks;
using MSA.Foundation.Messaging;

// Minimal test for service registration
public class MinimalServiceTest 
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Starting Minimal Service Registration Test");
        
        // Basic test to check if we can load the message types
        Console.WriteLine("Message Types from MSA.Foundation:");
        foreach (var value in Enum.GetValues(typeof(MessageType)))
        {
            Console.WriteLine($"  - {value}");
        }
        
        // Create a test message
        Console.WriteLine("\nCreating a test service registration message:");
        var message = Message.Create(MessageType.ServiceRegistration);
        message.MessageId = Guid.NewGuid().ToString();
        message.SenderId = "test_service";
        
        Console.WriteLine($"Message created with ID: {message.MessageId}");
        Console.WriteLine($"Message type: {message.Type}");
        Console.WriteLine($"Sender ID: {message.SenderId}");
        
        Console.WriteLine("\nTest completed successfully!");
    }
}