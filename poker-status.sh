#!/bin/bash

echo "Starting Poker Game Status Page on port 5000..."
cd /home/runner/workspace

# Create a web directory
mkdir -p /tmp/poker-web
chmod 777 /tmp/poker-web

# Create status page HTML
cat > /tmp/poker-web/index.html << 'EOT'
<!DOCTYPE html>
<html>
<head>
    <title>Poker Game - Status</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f0f0f0; }
        .container { max-width: 800px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; }
        .status { padding: 15px; background-color: #d4edda; color: #155724; border-radius: 4px; margin: 20px 0; }
        .feature { margin-bottom: 20px; }
        .feature h3 { margin-top: 0; }
        .complete { border-left: 4px solid #28a745; padding-left: 15px; }
        .in-progress { border-left: 4px solid #ffc107; padding-left: 15px; }
        .info { background-color: #f8f9fa; padding: 15px; border-radius: 4px; margin-top: 20px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>Poker Game Desktop Application</h1>
        <div class="status">
            <strong>✓ Project Status:</strong> Development in progress.
        </div>
        
        <h2>Completed Features</h2>
        <div class="feature complete">
            <h3>✓ Messaging Architecture</h3>
            <p>Successfully implemented a robust messaging system using NetMQ with full acknowledgment support and central broker.</p>
        </div>
        
        <div class="feature complete">
            <h3>✓ Microservice Architecture</h3>
            <p>Implemented a fully decoupled microservice architecture with service discovery and registration.</p>
        </div>
        
        <div class="feature complete">
            <h3>✓ Poker Game Flow</h3>
            <p>Completed Texas Hold'em game flow implementation (Pre-flop → Flop → Turn → River → Showdown → HandComplete).</p>
        </div>
        
        <div class="feature complete">
            <h3>✓ Application Insights Integration</h3>
            <p>Added telemetry using Application Insights for monitoring game performance and usage patterns.</p>
        </div>
        
        <div class="feature complete">
            <h3>✓ Avalonia UI</h3>
            <p>Built reactive desktop UI using Avalonia with proper MVVM architecture.</p>
        </div>
        
        <div class="feature complete">
            <h3>✓ Bug Fixes</h3>
            <p>Fixed critical stack overflow bugs and circular reference issues in the ViewModels.</p>
        </div>
        
        <h2>Current Focuses</h2>
        <div class="feature in-progress">
            <h3>→ Platform Optimization</h3>
            <p>Removed web/browser support to focus exclusively on Windows and macOS desktop platforms.</p>
        </div>
        
        <div class="feature in-progress">
            <h3>→ Code Maintenance</h3>
            <p>Enhancing ExecutionContext with proper process exit handlers and improving Dispose methods.</p>
        </div>
        
        <div class="info">
            <p>This is v0.1.0 of the Poker Game, focusing on creating a robust foundation for the MSA.Foundation library.</p>
            <p>The desktop application UI using Avalonia is not shown here because Replit is a web environment, but the code has been updated to support desktop platforms.</p>
        </div>
    </div>
</body>
</html>
EOT

# Set proper permissions on HTML file
chmod 644 /tmp/poker-web/index.html

# Start a simple HTTP server on port 5000
cd /tmp/poker-web && python3 -m http.server 5000