using System;
using System.Threading.Tasks;

namespace PokerGame.Core.Microservices
{
    /// <summary>
    /// Extension methods for MicroserviceBase to support the new message broker
    /// </summary>
    public static class MicroserviceBaseExtensions
    {
        /// <summary>
        /// Gets the service name from a microservice
        /// </summary>
        /// <param name="service">The microservice</param>
        /// <returns>The service name</returns>
        public static string GetServiceName(this MicroserviceBase service)
        {
            // Since we can't modify MicroserviceBase directly, we'll use reflection to access private field
            var type = service.GetType();
            var fieldInfo = type.GetField("_serviceName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldInfo != null)
            {
                var serviceName = fieldInfo.GetValue(service) as string;
                return serviceName ?? service.GetType().Name;
            }
            
            // Fallback to type name
            return service.GetType().Name;
        }
        
        /// <summary>
        /// Handles a service registration asynchronously
        /// </summary>
        /// <param name="service">The microservice</param>
        /// <param name="registration">The service registration information</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task HandleServiceRegistrationAsync(this MicroserviceBase service, ServiceRegistrationPayload registration)
        {
            // This is a pass-through method that will be used by the message broker
            Console.WriteLine($"Service registered: {registration.ServiceName} ({registration.ServiceType})");
            await Task.CompletedTask;
        }
    }
}