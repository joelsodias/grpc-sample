# gRPC Sample Project

## Overview
This project demonstrates a gRPC-based client-server application implementing various streaming patterns for a financial market data system.

## Services

### Market Data Service
* Streaming price updates for financial assets
* Server streams real-time price updates to subscribed clients
* Simulates price changes every 2 seconds

### Trade Service
* Handles client-streaming of trade orders
* Processes multiple orders in a single session
* Returns trade summary with total orders and volume

### Trading Chat Service
* Bi-directional streaming chat service
* Enables real-time communication between traders
* Broadcasts messages to all connected clients

### Greeter Service
* Simple unary RPC example
* Basic request-response pattern

## Project Structure
```
├── Protos/
│   ├── market.proto    # Market-related service definitions
│   └── greet.proto     # Greeter service definition
├── GrpcServer/
│   ├── Services/       # Service implementations
│   └── Program.cs      # Server configuration
└── GrpcClient/
    └── Program.cs      # Client implementation
```

## Getting Started

### Prerequisites
* .NET 9.0
* Visual Studio 2022 or VS Code

### Running the Application
1. Start the server:
   ```
   cd GrpcServer
   dotnet run
   ```

2. Start the client:
   ```
   cd GrpcClient
   dotnet run
   ```

## Features
* Real-time price streaming
* Order processing
* Chat functionality
* Secure communication

## Technologies
* gRPC
* .NET 9.0
* Protocol Buffers
* ASP.NET Core

## Development
The solution includes:
* Server-side streaming (Market Data)
* Client-side streaming (Trade Service)
* Bi-directional streaming (Trading Chat)
* Unary RPC (Greeter) 