using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcMarket;
using GrpcServer;

class Program 
{
    static async Task Main(string[] args)
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5224");

        // Initialize all service clients
        var greeterClient = new Greeter.GreeterClient(channel);
        var marketClient = new MarketData.MarketDataClient(channel);
        var tradeClient = new TradeService.TradeServiceClient(channel);
        var chatClient = new TradingChat.TradingChatClient(channel);

        while (true)
        {
            Console.WriteLine("\nSelect a service to test:");
            Console.WriteLine("1. Greeter Service (Unary)");
            Console.WriteLine("2. Market Data Service (Server Streaming)");
            Console.WriteLine("3. Trade Service (Client Streaming)");
            Console.WriteLine("4. Trading Chat (Bidirectional Streaming)");
            Console.WriteLine("5. Exit");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await TestGreeterService(greeterClient);
                    break;
                case "2": 
                    await TestMarketDataService(marketClient);
                    break;
                case "3":
                    await TestTradeService(tradeClient);
                    break;
                case "4":
                    await TestTradingChatService(chatClient);
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Invalid choice");
                    break;
            }
        }
    }

    static async Task TestGreeterService(Greeter.GreeterClient client)
    {
        Console.Write("Enter your name: ");
        var name = Console.ReadLine();
        var reply = await client.SayHelloAsync(new HelloRequest { Name = name });
        Console.WriteLine($"Greeting: {reply.Message}");
    }

    static async Task TestMarketDataService(MarketData.MarketDataClient client)
    {
        Console.Write("Enter asset symbol (e.g. AAPL): ");
        var symbol = Console.ReadLine();
        
        using var call = client.SubscribePrices(new PriceRequest { Asset = symbol });
        var cts = new CancellationTokenSource();

        Console.WriteLine("Receiving price updates (Press Enter to stop)...");
        
        var readTask = Task.Run(async () => {
            await Console.In.ReadLineAsync();
            cts.Cancel();
        });

        try 
        {
            while (await call.ResponseStream.MoveNext(cts.Token))
            {
                var update = call.ResponseStream.Current;
                Console.WriteLine($"Price Update: {update.Asset} - ${update.Price:F2} ({update.Timestamp})");
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine("Stream cancelled");
        }
    }

    static async Task TestTradeService(TradeService.TradeServiceClient client)
    {
        using var call = client.PlaceOrders();
        
        Console.WriteLine("Enter trades (empty line to finish):");
        while (true)
        {
            Console.Write("Asset symbol: ");
            var symbol = Console.ReadLine();
            if (string.IsNullOrEmpty(symbol)) break;

            Console.Write("Quantity: ");
            if (!int.TryParse(Console.ReadLine(), out int quantity)) break;

            Console.Write("Price: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal price)) break;

            await call.RequestStream.WriteAsync(new TradeOrder 
            {
                Asset = symbol,
                Quantity = quantity,
                OrderType = "BUY"  // or "SELL" based on user input
            });
        }

        await call.RequestStream.CompleteAsync();
        var summary = await call.ResponseAsync;
        Console.WriteLine($"Trade Summary: {summary.TotalOrders} orders, Total Volume: ${summary.TotalVolume:F2}");
    }

    static async Task TestTradingChatService(TradingChat.TradingChatClient client)
    {
        Console.Write("Enter your trader name: ");
        var traderName = Console.ReadLine();

        using var call = client.Chat();
        var cts = new CancellationTokenSource();

        // Receive messages
        var readTask = Task.Run(async () => {
            try 
            {
                while (await call.ResponseStream.MoveNext(cts.Token))
                {
                    var message = call.ResponseStream.Current;
                    Console.WriteLine($"{message.TraderId}: {message.Message}");
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                Console.WriteLine("Chat ended");
            }
        });

        Console.WriteLine("Start chatting (empty line to exit):");
        
        // Send messages
        while (true)
        {
            var content = Console.ReadLine();
            if (string.IsNullOrEmpty(content)) break;

            await call.RequestStream.WriteAsync(new ChatMessage 
            {
                TraderId = traderName,
                Message = content,
                Timestamp = DateTime.UtcNow.ToString("o")
            });
        }

        cts.Cancel();
        await call.RequestStream.CompleteAsync();
    }
}


