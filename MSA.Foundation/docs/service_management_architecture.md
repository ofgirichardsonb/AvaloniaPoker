# MSA.Foundation Service Management Architecture

## Service Lifecycle Management

```mermaid
stateDiagram-v2
    [*] --> Initialized: Create ExecutionContext
    Initialized --> Ready: Configure Thread & Cancellation
    Ready --> Running: Start Service
    Running --> Processing: Process Messages
    Processing --> Running: Continue Processing
    
    Running --> Stopping: Stop Request
    Running --> Stopping: Cancellation Requested
    Stopping --> Stopped: Stop Service
    Stopped --> [*]: Dispose Resources
    
    Processing --> Stopped: Exception
    
    note right of Initialized
        Thread and cancellation token created
    end note
    
    note right of Running
        Service actively processing messages
    end note
    
    note right of Stopping
        Graceful shutdown sequence
    end note
```

## Service Management Components Interaction

```mermaid
graph TD
    subgraph AppLayer["Application Layer"]
        style AppLayer fill:#d1e7dd,stroke:#198754,stroke-width:2px
        App["Application Code"]
        Config["Configuration"]
    end
    
    subgraph FoundationLayer["MSA.Foundation"]
        style FoundationLayer fill:#cfe2ff,stroke:#0d6efd,stroke-width:2px
        ExecCtx["ExecutionContext"]
        ServiceConst["ServiceConstants"]
        
        subgraph MsgLayer["Messaging"]
            style MsgLayer fill:#f8d7da,stroke:#dc3545,stroke-width:1px
            MsgBroker["MessageBroker"]
        end
    end
    
    subgraph ServiceLayer["Services"]
        style ServiceLayer fill:#fff3cd,stroke:#fd7e14,stroke-width:2px
        Service1["Service A"]
        Service2["Service B"]
        Service3["Service C"]
    end
    
    App -->|"1. Creates"| ExecCtx
    App -->|"2. Configures"| ServiceConst
    App -->|"3. Starts"| MsgBroker
    
    ExecCtx -->|"4. Manages"| Service1
    ExecCtx -->|"4. Manages"| Service2
    ExecCtx -->|"4. Manages"| Service3
    
    ServiceConst -.->|"Uses for configuration"| Service1
    ServiceConst -.->|"Uses for configuration"| Service2
    ServiceConst -.->|"Uses for configuration"| Service3
    
    MsgBroker -.->|"Communication Channel"| Service1
    MsgBroker -.->|"Communication Channel"| Service2
    MsgBroker -.->|"Communication Channel"| Service3
    
    Config -->|"Provides settings"| App
```

## Thread Management Model

```mermaid
flowchart LR
    subgraph MainApp["Main Application"]
        Main["Main Thread"]
    end
    
    subgraph ExecContexts["Execution Contexts"]
        Ctx1["ExecutionContext 1"]
        Ctx2["ExecutionContext 2"]
        Ctx3["ExecutionContext 3"]
    end
    
    subgraph Threads["Managed Threads"]
        T1["Service Thread 1"]
        T2["Service Thread 2"]
        T3["Service Thread 3"]
    end
    
    subgraph Cancellation["Cancellation Management"]
        CTS1["CancellationTokenSource 1"]
        CTS2["CancellationTokenSource 2"]
        CTS3["CancellationTokenSource 3"]
    end
    
    Main -->|"Creates"| Ctx1
    Main -->|"Creates"| Ctx2
    Main -->|"Creates"| Ctx3
    
    Ctx1 -->|"Manages"| T1
    Ctx2 -->|"Manages"| T2
    Ctx3 -->|"Manages"| T3
    
    Ctx1 -->|"Controls"| CTS1
    Ctx2 -->|"Controls"| CTS2
    Ctx3 -->|"Controls"| CTS3
    
    CTS1 -->|"Signals"| T1
    CTS2 -->|"Signals"| T2
    CTS3 -->|"Signals"| T3
    
    style MainApp fill:#d1e7dd,stroke:#198754,stroke-width:2px
    style ExecContexts fill:#cfe2ff,stroke:#0d6efd,stroke-width:2px
    style Threads fill:#fff3cd,stroke:#fd7e14,stroke-width:2px
    style Cancellation fill:#f8d7da,stroke:#dc3545,stroke-width:2px
```

## Service Configuration and Discovery

```mermaid
sequenceDiagram
    participant App as Application
    participant Constants as ServiceConstants
    participant Service as Service Instance
    participant Broker as MessageBroker
    
    App->>Constants: RegisterStaticId("ServiceA", "static_service_a")
    App->>Constants: Configure port ranges and offsets
    App->>Service: Create Service
    Service->>Constants: GetStaticId("ServiceA")
    Constants-->>Service: "static_service_a"
    Service->>Constants: GetCentralBrokerPort(portOffset)
    Constants-->>Service: Calculated port
    Service->>Broker: Connect with Static ID and Port
    
    Note over Service,Broker: Service Registration Process
    
    Service->>Broker: PublishMessage(ServiceRegistration)
    Broker->>Service: Confirmation
    
    Note over App,Broker: Communication established
```