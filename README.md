# Cross-Platform Poker Game (v1.0.0)

A sophisticated Texas Hold'em poker game leveraging advanced microservices architecture with a cross-platform Avalonia UI interface, built on .NET 8.0.

## Features

- Complete Texas Hold'em poker gameplay with AI opponents
- Advanced microservices-based architecture for improved modularity
- Cross-platform compatibility (Windows and macOS)
- Robust messaging framework with Channel-based in-process communication
- Application Insights telemetry for comprehensive logging and diagnostics
- Clean separation between service layer and UI components
- Smart AI opponents with dynamic decision making

## Project Structure

- **MSA.Foundation**: Reusable microservices architecture foundation with messaging, telemetry, and service management
- **PokerGame.Core**: Contains the core game logic, models, and AI players
- **PokerGame.Avalonia**: Cross-platform GUI interface built with Avalonia UI
- **PokerGame.Services**: Service implementations for the poker game
- **PokerGame.Abstractions**: Interface definitions and abstractions for the poker game services

## Architecture

The game employs a "Bonsai Architecture" pattern - miniaturized microservices in a single process that can later be promoted to distributed services. This provides the modularity benefits of microservices while maintaining the simplicity of a monolithic deployment.

Key architectural features:
- **Messaging Framework**: Channel-based in-process communication with central message broker
- **Service Management**: Comprehensive execution context and shutdown coordination
- **Telemetry**: Application Insights integration for monitoring and diagnostics
- **Clean Separation**: UI and game logic completely decoupled through service interfaces

## Requirements

- .NET 8.0 or later
- Git (for version control)
- Windows or macOS for running the desktop application

## Getting Started

### Clone the Repository

```bash
git clone https://github.com/your-username/PokerGame
cd PokerGame
```

### Build and Run

#### Run the Avalonia UI Application

```bash
./run-avalonia-ui.sh
```

#### Build a Standalone Executable

```bash
./build-standalone.sh
```
The executable will be created in the `publish` directory.

## Release Notes for v1.0.0

This is the first stable release of the Cross-Platform Poker Game, featuring:

- Complete Texas Hold'em gameplay flow with proper betting rounds, showdowns, and winner determination
- Smart AI opponents that make challenging decisions based on hand strength and betting patterns
- Cross-platform UI using Avalonia framework with a polished poker table display
- Microservices-based architecture with clean separation of concerns
- Table limits to prevent unreasonable bets ($1000 max chips, $100 max bet)
- Comprehensive telemetry with Application Insights integration
- Improved exception handling and stability
- Optimized memory usage and performance

### Architectural Highlights

- Migrated from NetMQ to Channel-based messaging for simplicity
- Enhanced shutdown coordination for proper resource cleanup
- Implemented comprehensive player and card state tracking
- Refined AI decision-making process for better gameplay
- Added detailed diagnostic logging throughout the application
