# Changelog

All notable changes to the Cross-Platform Poker Game will be documented in this file.

## [1.0.0] - 2025-05-05

### Added
- Complete Texas Hold'em gameplay flow with betting rounds and showdowns
- Smart AI opponents with decision-making based on hand strength and betting patterns
- Cross-platform Avalonia UI with polished poker table design
- Application Insights telemetry integration
- Table limits ($1000 max chips, $100 max bet)
- Comprehensive diagnostic logging
- Standalone deployment capabilities for Windows and macOS

### Changed
- Migrated from NetMQ to Channel-based messaging architecture
- Moved foundational services from PokerGame.Foundation to MSA.Foundation
- Refactored card visibility management for better multiplayer support
- Enhanced betting logic to properly handle raises, calls, and checks
- Updated player layout in UI from WrapPanel to fixed-position elements

### Fixed
- AI players now properly respond to player raises
- Betting round completion checks now verify both HasActed flags AND current bet amounts
- Card duplication issues resolved with HashSet-based deduplication
- Game state properly resets between hands
- UI updates consistently after player actions

### Removed
- NetMQ dependencies and implementations
- Console UI in favor of Avalonia UI
- Unused launcher and service management code
- Web and Linux platform support to focus on desktop platforms

## [0.1.0] - 2025-03-15

### Added
- Initial project structure and architecture
- Basic poker game logic
- Microservices foundation
- NetMQ message transport implementation
- First version of UI with Avalonia
- Console UI implementation

### Changed
- Initial release

### Fixed
- Initial release