using Grpc.Core;
using GrpcMarket;
using GrpcServer.Services;
using Moq;
using Xunit;

namespace GrpcServer.Tests.Services;

public class TradingChatServiceTests
{
    [Fact]
    public async Task Chat_BroadcastsMessageToAllClients()
    {
        // Arrange
        var service = new TradingChatService();
        var message = new ChatMessage 
        { 
            TraderId = "trader1", 
            Message = "Hello", 
            Timestamp = DateTime.UtcNow.ToString("o") 
        };

        var requestStream1Mock = new Mock<IAsyncStreamReader<ChatMessage>>();
        var requestStream2Mock = new Mock<IAsyncStreamReader<ChatMessage>>();
        var responseStream1Mock = new Mock<IServerStreamWriter<ChatMessage>>();
        var responseStream2Mock = new Mock<IServerStreamWriter<ChatMessage>>();
        var cts = new CancellationTokenSource();

        var receivedMessages1 = new List<ChatMessage>();
        var receivedMessages2 = new List<ChatMessage>();
        var hasReturnedMessage1 = false;
        var hasReturnedMessage2 = false;

        // Setup request streams
        requestStream1Mock
            .Setup(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => 
            {
                if (!hasReturnedMessage1)
                {
                    hasReturnedMessage1 = true;
                    return true;
                }
                return false;
            });

        requestStream2Mock
            .Setup(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => 
            {
                if (!hasReturnedMessage2)
                {
                    hasReturnedMessage2 = true;
                    return true;
                }
                return false;
            });

        requestStream1Mock.Setup(x => x.Current).Returns(message);
        requestStream2Mock.Setup(x => x.Current).Returns(message);

        // Setup response streams with debug logging
        responseStream1Mock
            .Setup(x => x.WriteAsync(It.IsAny<ChatMessage>()))
            .Callback<ChatMessage>(msg => 
            {
                Console.WriteLine($"Stream 1 received: {msg.TraderId}: {msg.Message}");
                lock (receivedMessages1)
                {
                    receivedMessages1.Add(msg);
                }
            })
            .Returns(Task.CompletedTask);

        responseStream2Mock
            .Setup(x => x.WriteAsync(It.IsAny<ChatMessage>()))
            .Callback<ChatMessage>(msg => 
            {
                Console.WriteLine($"Stream 2 received: {msg.TraderId}: {msg.Message}");
                lock (receivedMessages2)
                {
                    receivedMessages2.Add(msg);
                }
            })
            .Returns(Task.CompletedTask);

        // Create test context with cancellation token
        var context = TestServerCallContext.Create(
            contextType: typeof(TradingChatService),
            cancellationToken: cts.Token
        );

        // Act
        // Start both clients
        var chat1Task = service.Chat(requestStream1Mock.Object, responseStream1Mock.Object, context);
        var chat2Task = service.Chat(requestStream2Mock.Object, responseStream2Mock.Object, context);

        // Wait for message processing
        var processingTimeout = TimeSpan.FromSeconds(5);
        using var timeoutCts = new CancellationTokenSource(processingTimeout);
        try
        {
            await WaitForConditionAsync(
                () => receivedMessages1.Count > 0 && receivedMessages2.Count > 0, 
                timeoutCts.Token
            );
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Test timed out waiting for messages");
            Console.WriteLine("Current state:");
            Console.WriteLine($"Messages received by stream 1: {receivedMessages1.Count}");
            Console.WriteLine($"Messages received by stream 2: {receivedMessages2.Count}");
        }

        // Cleanup
        cts.Cancel();
        try
        {
            await Task.WhenAll(chat1Task, chat2Task);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.True(receivedMessages1.Any(), "No messages received by stream 1");
        Assert.True(receivedMessages2.Any(), "No messages received by stream 2");
        Assert.Equal(message.Message, receivedMessages1.First().Message);
        Assert.Equal(message.Message, receivedMessages2.First().Message);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition() && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken);
        }

        if (!condition() && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
    }

    [Fact]
    public async Task Chat_RemovesDisconnectedClients()
    {
        // Arrange
        var service = new TradingChatService();
        var message = new ChatMessage 
        { 
            TraderId = "trader1", 
            Message = "Hello", 
            Timestamp = DateTime.UtcNow.ToString("o") 
        };

        var requestStream1Mock = new Mock<IAsyncStreamReader<ChatMessage>>();
        var requestStream2Mock = new Mock<IAsyncStreamReader<ChatMessage>>();
        var workingClientMock = new Mock<IServerStreamWriter<ChatMessage>>();
        var failingClientMock = new Mock<IServerStreamWriter<ChatMessage>>();
        var cts = new CancellationTokenSource();

        var receivedMessages = new List<ChatMessage>();
        var failedAttempts = 0;
        var hasReturnedMessage1 = false;
        var hasReturnedMessage2 = false;

        // Setup request streams
        requestStream1Mock
            .Setup(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => 
            {
                if (!hasReturnedMessage1)
                {
                    hasReturnedMessage1 = true;
                    return true;
                }
                return false;
            });

        requestStream2Mock
            .Setup(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => 
            {
                if (!hasReturnedMessage2)
                {
                    hasReturnedMessage2 = true;
                    return true;
                }
                return false;
            });

        requestStream1Mock.Setup(x => x.Current).Returns(message);
        requestStream2Mock.Setup(x => x.Current).Returns(message);

        // Setup working client
        workingClientMock
            .Setup(x => x.WriteAsync(It.IsAny<ChatMessage>()))
            .Callback<ChatMessage>(msg => 
            {
                lock (receivedMessages)
                {
                    receivedMessages.Add(msg);
                    Console.WriteLine($"Working client received: {msg.TraderId}: {msg.Message}");
                }
            })
            .Returns(Task.CompletedTask);

        // Setup failing client
        failingClientMock
            .Setup(x => x.WriteAsync(It.IsAny<ChatMessage>()))
            .Callback(() => 
            {
                Interlocked.Increment(ref failedAttempts);
                Console.WriteLine("Failing client attempted");
            })
            .ThrowsAsync(new Exception("Connection lost"));

        // Create test context with cancellation token
        var context = TestServerCallContext.Create(
            contextType: typeof(TradingChatService),
            cancellationToken: cts.Token
        );

        // Act
        var chat1Task = service.Chat(requestStream1Mock.Object, workingClientMock.Object, context);
        var chat2Task = service.Chat(requestStream2Mock.Object, failingClientMock.Object, context);

        // Wait for message processing
        var processingTimeout = TimeSpan.FromSeconds(5);
        using var timeoutCts = new CancellationTokenSource(processingTimeout);
        try
        {
            await WaitForConditionAsync(
                () => receivedMessages.Count > 0 && failedAttempts > 0, 
                timeoutCts.Token
            );
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Test timed out waiting for messages");
            Console.WriteLine($"Messages received: {receivedMessages.Count}");
            Console.WriteLine($"Failed attempts: {failedAttempts}");
        }

        // Cleanup
        cts.Cancel();
        try
        {
            await Task.WhenAll(chat1Task, chat2Task);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        Assert.True(receivedMessages.Any(), "No messages received by working client");
        Assert.Equal(message.Message, receivedMessages.First().Message);
        Assert.True(failedAttempts > 0, "Failing client was never attempted");
    }
} 