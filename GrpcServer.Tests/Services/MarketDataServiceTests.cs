using Grpc.Core;
using GrpcMarket;
using GrpcServer.Services;
using Moq;
using Xunit;

namespace GrpcServer.Tests.Services;

public class MarketDataServiceTests
{
    [Fact]
    public async Task SubscribePrices_StreamsPriceUpdates()
    {
        // Arrange
        var service = new MarketDataService();
        var request = new PriceRequest { Asset = "AAPL" };
        var responseStreamMock = new Mock<IServerStreamWriter<PriceResponse>>();
        var contextMock = new Mock<ServerCallContext>();
        var cts = new CancellationTokenSource();

        var writtenPrices = new List<PriceResponse>();
        responseStreamMock
            .Setup(x => x.WriteAsync(It.IsAny<PriceResponse>()))
            .Callback<PriceResponse>(response => writtenPrices.Add(response))
            .Returns(Task.CompletedTask);

        // Use TestServerCallContext instead of mocking
        var context = TestServerCallContext.Create(
            contextType: typeof(MarketDataService),
            cancellationToken: cts.Token
        );

        // Act
        var streamTask = service.SubscribePrices(request, responseStreamMock.Object, context);
        
        // Wait for 2 price updates
        await Task.Delay(4100); // Slightly more than 2 updates (2000ms each)
        cts.Cancel();

        try
        {
            await streamTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.True(writtenPrices.Count >= 2);
        Assert.All(writtenPrices, price => 
        {
            Assert.Equal("AAPL", price.Asset);
            Assert.InRange(price.Price, 0, 100);
            Assert.NotNull(price.Timestamp);
        });
    }
} 