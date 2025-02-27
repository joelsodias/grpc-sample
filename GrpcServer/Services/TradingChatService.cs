using Grpc.Core;
using GrpcMarket;
using Microsoft.Extensions.Logging;

namespace GrpcServer.Services;

public class TradingChatService : TradingChat.TradingChatBase
{
    // Modify the client tracking to include trader IDs
    private static readonly Dictionary<string, IServerStreamWriter<ChatMessage>> _clients = new();
    private static readonly object _lock = new();
    private readonly ILogger<TradingChatService> _logger;

    public TradingChatService(ILogger<TradingChatService> logger)
    {
        _logger = logger;
    }

    public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
    {
        string? currentTraderId = null;

        // Thread-safe client registration
        try
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (currentTraderId == null)
                {
                    // Register client on first message
                    lock (_lock)
                    {
                        currentTraderId = message.TraderId;
                        _clients[message.TraderId] = responseStream;
                        _logger.LogInformation($"New client connected. TraderId: {message.TraderId}, Total clients: {_clients.Count}");
                    }
                }

                if (context.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancellation requested, stopping message processing");
                    break;
                }

                // Parse message for direct messaging
                if (message.Message.StartsWith("/to "))
                {
                    await HandleDirectMessage(message);
                }
                else
                {
                    await BroadcastMessage(message, context.CancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chat stream canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in chat stream");
        }
        finally
        {
            if (currentTraderId != null)
            {
                lock (_lock)
                {
                    _clients.Remove(currentTraderId);
                    _logger.LogInformation($"Client {currentTraderId} disconnected. Remaining clients: {_clients.Count}");
                }
            }
        }
    }

    private async Task HandleDirectMessage(ChatMessage message)
    {
        // Parse "/to username message"
        var parts = message.Message.Split(new[] { ' ' }, 3);
        if (parts.Length < 3)
        {
            await SendSystemMessage(message.TraderId, "Invalid format. Use: /to username message");
            return;
        }

        var targetUser = parts[1];
        var directMessage = parts[2];

        lock (_lock)
        {
            if (!_clients.TryGetValue(targetUser, out var targetClient))
            {
                // User not found, send error message back to sender
                SendSystemMessage(message.TraderId, $"User '{targetUser}' not found or offline").Wait();
                return;
            }

            // Create private message format
            var privateMessage = new ChatMessage
            {
                TraderId = message.TraderId,
                Message = $"[Private] {directMessage}",
                Timestamp = message.Timestamp
            };

            // Send to target user
            try
            {
                targetClient.WriteAsync(privateMessage).Wait();
                // Send confirmation to sender
                SendSystemMessage(message.TraderId, $"Message sent to {targetUser}").Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending private message to {targetUser}");
                _clients.Remove(targetUser);
            }
        }
    }

    private async Task SendSystemMessage(string traderId, string message)
    {
        if (_clients.TryGetValue(traderId, out var client))
        {
            var systemMessage = new ChatMessage
            {
                TraderId = "System",
                Message = message,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            try
            {
                await client.WriteAsync(systemMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending system message to {traderId}");
            }
        }
    }

    private async Task BroadcastMessage(ChatMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"User '{message.TraderId}' sent broadcast message: '{message.Message}'");

        Dictionary<string, IServerStreamWriter<ChatMessage>> currentClients;
        lock (_lock)
        {
            currentClients = new Dictionary<string, IServerStreamWriter<ChatMessage>>(_clients);
        }

        var deadClients = new List<string>();

        foreach (var kvp in currentClients)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                await kvp.Value.WriteAsync(message).WaitAsync(cts.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is RpcException)
            {
                _logger.LogInformation($"Client {kvp.Key} disconnected during message delivery");
                deadClients.Add(kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error delivering message to client {kvp.Key}");
                deadClients.Add(kvp.Key);
            }
        }

        if (deadClients.Any())
        {
            lock (_lock)
            {
                foreach (var deadClient in deadClients)
                {
                    _clients.Remove(deadClient);
                    _logger.LogInformation($"Removed disconnected client {deadClient}");
                }
            }
        }
    }
}

