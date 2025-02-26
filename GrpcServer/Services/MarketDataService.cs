using Grpc.Core;
using GrpcMarket;

namespace GrpcServer.Services;

public class MarketDataService : MarketData.MarketDataBase
{
    private static readonly Random _random = new();

    public override async Task SubscribePrices(PriceRequest request, IServerStreamWriter<PriceResponse> responseStream, ServerCallContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var price = _random.NextDouble() * 100; // Simula um preço entre 0 e 100
            var response = new PriceResponse
            {
                Asset = request.Asset,
                Price = Math.Round(price, 2),
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            await responseStream.WriteAsync(response);
            await Task.Delay(2000); // Envia preço a cada 2 segundos
        }
    }
}
