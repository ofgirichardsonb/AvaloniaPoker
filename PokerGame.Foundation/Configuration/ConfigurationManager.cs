using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace PokerGame.Foundation.Configuration
{
    /// <summary>
    /// Configuration manager for the application
    /// </summary>
    public class ConfigurationManager
    {
        private static readonly Lazy<ConfigurationManager> _instance = new Lazy<ConfigurationManager>(() => new ConfigurationManager());
        
        /// <summary>
        /// Gets the singleton instance of the configuration manager
        /// </summary>
        public static ConfigurationManager Instance => _instance.Value;
        
        private IConfiguration? _configuration;
        
        /// <summary>
        /// Gets the application configuration
        /// </summary>
        public IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    InitializeConfiguration();
                }
                
                return _configuration!;
            }
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationManager"/> class
        /// </summary>
        private ConfigurationManager()
        {
            InitializeConfiguration();
        }
        
        /// <summary>
        /// Initializes the configuration
        /// </summary>
        private void InitializeConfiguration()
        {
            try
            {
                Console.WriteLine($"Initializing configuration in {Directory.GetCurrentDirectory()}");
                
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
                
                _configuration = builder.Build();
                
                Console.WriteLine("Configuration initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing configuration: {ex.Message}");
                _configuration = new ConfigurationBuilder().Build(); // Empty configuration
            }
        }
        
        /// <summary>
        /// Gets a configuration value
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <returns>The configuration value, or null if not found</returns>
        public string? GetValue(string key)
        {
            // First try environment variable (prioritize this)
            string? envValue = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(envValue))
            {
                return envValue;
            }
            
            // Then try configuration from file
            return Configuration[key];
        }
        
        /// <summary>
        /// Gets a configuration section
        /// </summary>
        /// <param name="sectionKey">The section key</param>
        /// <returns>The configuration section</returns>
        public IConfigurationSection GetSection(string sectionKey)
        {
            return Configuration.GetSection(sectionKey);
        }
        
        /// <summary>
        /// Gets the Application Insights instrumentation key
        /// </summary>
        /// <returns>The instrumentation key, or null if not found</returns>
        public string? GetApplicationInsightsKey()
        {
            // Try environment variable first (most reliable approach)
            string? instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
            
            // Log what we found
            if (instrumentationKey != null)
            {
                Console.WriteLine("Using Application Insights key from environment variable");
                return instrumentationKey;
            }
            
            // Fall back to configuration
            instrumentationKey = Configuration["ApplicationInsights:InstrumentationKey"];
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                Console.WriteLine("Using Application Insights key from configuration");
                return instrumentationKey;
            }
            
            // Not found
            Console.WriteLine("Application Insights key not found in environment or configuration");
            return null;
        }
    }
}