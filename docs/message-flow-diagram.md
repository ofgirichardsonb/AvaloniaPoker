# Poker Game Message Flow Diagram

The following diagram illustrates the message flow between the different microservices in the poker game architecture.

## Game Initialization and Service Discovery

```mermaid
sequenceDiagram
    participant CU as ConsoleUIService
    participant CMB as CentralMessageBroker
    participant GE as GameEngineService
    participant CD as CardDeckService

    Note over CU,CD: Service Registration Phase
    
    CU->>CMB: ServiceRegistration (ConsoleUI)
    CMB->>CU: Registered confirmation
    
    GE->>CMB: ServiceRegistration (GameEngine)
    CMB->>GE: Registered confirmation
    
    CD->>CMB: ServiceRegistration (CardDeck)
    CMB->>CD: Registered confirmation
    
    Note over CU,CD: Service Discovery Phase
    
    CU->>CMB: ServiceDiscovery broadcast
    CMB->>GE: ServiceDiscovery message
    GE->>CMB: ServiceRegistration response
    CMB->>CU: ServiceRegistration (from GameEngine)
    
    Note over CU: Stores GameEngine ID
    
    CMB->>CD: ServiceDiscovery message
    CD->>CMB: ServiceRegistration response
    CMB->>CU: ServiceRegistration (from CardDeck)
```

## Game Start and Hand Dealing Sequence

```mermaid
sequenceDiagram
    participant CU as ConsoleUIService
    participant CMB as CentralMessageBroker
    participant GE as GameEngineService
    participant CD as CardDeckService

    CU->>CMB: StartGame with player names
    CMB->>GE: StartGame message
    
    GE->>CD: CreateNewDeck request
    CD->>CD: Creates and shuffles deck
    CD->>CMB: DeckCreated response
    CMB->>GE: DeckCreated message
    
    GE->>CMB: GameState update broadcast
    CMB->>CU: GameState update
    
    CU->>CMB: StartHand request
    CMB->>GE: StartHand message
    
    GE->>CMB: Acknowledgment to StartHand
    CMB->>CU: Acknowledgment received
    
    GE->>CMB: DeckShuffled message
    CMB->>CU: DeckShuffled message
    
    Note over CU: Process DeckShuffled
    Note over CU: Display UI update
```

## Player Action Sequence

```mermaid
sequenceDiagram
    participant CU as ConsoleUIService
    participant CMB as CentralMessageBroker
    participant GE as GameEngineService
    participant CD as CardDeckService

    GE->>CMB: PlayerAction request
    CMB->>CU: PlayerAction request
    
    Note over CU: User input for action
    
    CU->>CMB: PlayerAction response (fold/call/raise)
    CMB->>GE: PlayerAction response
    
    GE->>GE: Process player action
    GE->>CMB: GameState update
    CMB->>CU: GameState update
    
    Note over CU: Display UI update
```

## Community Card Dealing Sequence

```mermaid
sequenceDiagram
    participant CU as ConsoleUIService
    participant CMB as CentralMessageBroker
    participant GE as GameEngineService
    participant CD as CardDeckService

    GE->>CD: DealCommunityCards (Flop)
    CD->>CD: Deals 3 cards
    CD->>CMB: CommunityCardsDealt response
    CMB->>GE: CommunityCardsDealt message
    
    GE->>CMB: GameState update (with Flop)
    CMB->>CU: GameState update
    
    Note over CU: Display Flop
    
    Note over GE,CU: Player action round repeats
    
    GE->>CD: DealCommunityCards (Turn)
    CD->>CD: Deals 1 card
    CD->>CMB: CommunityCardsDealt response
    CMB->>GE: CommunityCardsDealt message
    
    GE->>CMB: GameState update (with Turn)
    CMB->>CU: GameState update
    
    Note over CU: Display Turn
    
    Note over GE,CU: Player action round repeats
    
    GE->>CD: DealCommunityCards (River)
    CD->>CD: Deals 1 card
    CD->>CMB: CommunityCardsDealt response
    CMB->>GE: CommunityCardsDealt message
    
    GE->>CMB: GameState update (with River)
    CMB->>CU: GameState update
    
    Note over CU: Display River
```

## Hand Completion and Winner Determination

```mermaid
sequenceDiagram
    participant CU as ConsoleUIService
    participant CMB as CentralMessageBroker
    participant GE as GameEngineService
    participant CD as CardDeckService

    Note over GE,CU: Final player action round repeats
    
    GE->>GE: Evaluate hands and determine winner
    GE->>CMB: HandComplete message with winners
    CMB->>CU: HandComplete message
    
    Note over CU: Display winners
    
    GE->>CMB: GameState update (hand complete)
    CMB->>CU: GameState update
    
    Note over CU: Option to start new hand
    
    CU->>CMB: StartHand request (for next hand)
    CMB->>GE: StartHand message
    
    Note over GE,CU: Process repeats from StartHand
```

## Heartbeat and Health Check

```mermaid
sequenceDiagram
    participant CU as ConsoleUIService
    participant CMB as CentralMessageBroker
    participant GE as GameEngineService
    participant CD as CardDeckService

    loop Every 10 seconds
        CU->>CMB: Heartbeat message
        CMB->>GE: Heartbeat from CU
        CMB->>CD: Heartbeat from CU
        
        GE->>CMB: Heartbeat message
        CMB->>CU: Heartbeat from GE
        CMB->>CD: Heartbeat from GE
        
        CD->>CMB: Heartbeat message
        CMB->>CU: Heartbeat from CD
        CMB->>GE: Heartbeat from CD
    end
```

## Message Types and Flow Summary

| Message Type | Direction | Purpose |
|--------------|-----------|---------|
| ServiceRegistration | All → CMB | Microservices register with the broker |
| ServiceDiscovery | CU → CMB → All | ConsoleUI discovers available services |
| StartGame | CU → CMB → GE | Begin a new game with players |
| CreateNewDeck | GE → CD | Request to create and shuffle a new deck |
| DeckCreated | CD → CMB → GE | Notification that a new deck is ready |
| StartHand | CU → CMB → GE | Start a new hand of poker |
| DeckShuffled | GE → CMB → CU | Notification that deck is shuffled for new hand |
| PlayerAction | GE → CMB → CU (request) <br> CU → CMB → GE (response) | Request player action and send response |
| DealCommunityCards | GE → CD | Request to deal community cards (flop/turn/river) |
| CommunityCardsDealt | CD → CMB → GE | Notification that community cards have been dealt |
| GameState | GE → CMB → CU | Update UI with current game state |
| HandComplete | GE → CMB → CU | Notification of hand completion with winners |
| Heartbeat | All → CMB → All | Health check messages |
| Acknowledgment | All → CMB → All | Confirm message receipt |

## Critical Message Flow Issues

1. **StartHand → DeckShuffled Flow**: 
   - ConsoleUI sends StartHand to GameEngine
   - GameEngine should respond with DeckShuffled
   - ConsoleUI must handle DeckShuffled to continue game flow
   - If DeckShuffled handler is missing, game stalls

2. **Service Discovery Cycle**:
   - ConsoleUI must discover GameEngine before game can start
   - If discovery fails, ConsoleUI falls back to static service IDs
   - Proper registration/discovery ensures dynamic service communication

3. **Message Conversion**:
   - Messages convert between MSA.Foundation.Messaging.Message and PokerGame.Core.Messaging.NetworkMessage formats
   - MessageType enum references must be fully qualified to avoid ambiguity
   - Headers like "MessageSubType" aid in routing between services