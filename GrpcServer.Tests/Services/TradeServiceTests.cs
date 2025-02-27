using Grpc.Core;
using GrpcMarket;
using GrpcServer.Services;
using Moq;
using Xunit;

namespace GrpcServer.Tests.Services;

public class TradeServiceTests
{
    [Fact]
    public async Task PlaceOrders_CalculatesTotalsCorrectly()
    {
        // Arrange
        var service = new TradeServiceService();
        var requestStreamMock = new Mock<IAsyncStreamReader<TradeOrder>>();
        var contextMock = new Mock<ServerCallContext>();

        var orders = new List<TradeOrder>
        {
            new TradeOrder { TraderId = "trader1", Asset = "AAPL", Quantity = 100, OrderType = "BUY" },
            new TradeOrder { TraderId = "trader2", Asset = "MSFT", Quantity = 50, OrderType = "SELL" }
        };

        var orderQueue = new Queue<TradeOrder>(orders);

        requestStreamMock
            .Setup(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => orderQueue.Count > 0);

        requestStreamMock
            .Setup(x => x.Current)
            .Returns(() => orderQueue.Dequeue());

        // Act
        var result = await service.PlaceOrders(requestStreamMock.Object, contextMock.Object);

        // Assert
        Assert.Equal(2, result.TotalOrders);
        Assert.Equal(150, result.TotalVolume); // 100 + 50
    }
} 