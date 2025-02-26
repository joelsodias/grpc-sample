using Grpc.Core;
using GrpcMarket;

namespace GrpcServer.Services;

public class TradeServiceService : TradeService.TradeServiceBase
{
    public override async Task<TradeSummary> PlaceOrders(IAsyncStreamReader<TradeOrder> requestStream, ServerCallContext context)
    {
        int totalOrders = 0;
        double totalVolume = 0;

        await foreach (var order in requestStream.ReadAllAsync())
        {
            Console.WriteLine($"Ordem recebida: {order.TraderId} - {order.OrderType} {order.Quantity} {order.Asset}");

            totalOrders++;
            totalVolume += order.Quantity;
        }

        return new TradeSummary { TotalOrders = totalOrders, TotalVolume = totalVolume };
    }
}
