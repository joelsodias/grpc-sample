using Grpc.Core;
using GrpcServer;
using GrpcServer.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GrpcServer.Tests.Services;

public class GreeterServiceTests
{
    [Fact]
    public async Task SayHello_ReturnsGreeting()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<GreeterService>>();
        var service = new GreeterService(loggerMock.Object);
        var request = new HelloRequest { Name = "Test User" };
        var contextMock = new Mock<ServerCallContext>();

        // Act
        var response = await service.SayHello(request, contextMock.Object);

        // Assert
        Assert.Equal("Hello Test User", response.Message);
    }
} 