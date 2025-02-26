using Grpc.Core;
using GrpcMarket;

namespace GrpcServer.Services;

public class TradingChatService : TradingChat.TradingChatBase
{
    private readonly List<IServerStreamWriter<ChatMessage>> _clients = new();

    public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
    {
        _clients.Add(responseStream);

        await foreach (var message in requestStream.ReadAllAsync())
        {
            Console.WriteLine($"{message.TraderId}: {message.Message}");

            foreach (var client in _clients)
            {
                await client.WriteAsync(message);
            }
        }
    }
}
