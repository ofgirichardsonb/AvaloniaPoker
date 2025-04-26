# Cross-Platform Poker Game

A Texas Hold'em poker game implementation with both GUI and console interfaces, built with C# and Avalonia UI.

## Features

- Play Texas Hold'em poker
- Cross-platform compatibility (Windows, macOS, Linux)
- Two interface options:
  - Graphical interface using Avalonia UI
  - Text-based console interface

## Project Structure

- **PokerGame.Core**: Contains the core game logic, models, and microservice abstractions
- **PokerGame.Console**: Console-based interface for the game
- **PokerGame.Avalonia**: GUI interface built with Avalonia UI
- **PokerGame.Services**: Standalone microservice host for running game services
- **PokerGame.Abstractions**: Common interfaces and abstractions for the project

## Requirements

- .NET 6.0 or later
- Git (for version control)

## Getting Started

### Clone the Repository

```bash
git clone https://github.com/ofgirichardsonb/AvaloniaPoker
cd AvaloniaPoker
