using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using GrpcMarket;
using GrpcServer;

class Program
{
    static async Task Main()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5224");

        var greeterClient = new Greeter.GreeterClient(channel);

        var reply = await greeterClient.SayHelloAsync(new HelloRequest { Name = "Joelso" });
        Console.WriteLine($"Resposta do servidor: {reply.Message}");


        var marketClient = new MarketData.MarketDataClient(channel);

        using var call = marketClient.SubscribePrices(new PriceRequest { Asset = "AAPL" });

        while (await call.ResponseStream.MoveNext(default))
        {
            var response = call.ResponseStream.Current;
            Console.WriteLine($"Preço atualizado: {response.Asset} - ${response.Price} ({response.Timestamp})");
        }


    }
}
