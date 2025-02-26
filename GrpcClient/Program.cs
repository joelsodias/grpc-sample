using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using GrpcServer;

class Program
{
    static async Task Main()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5224");
        var client = new Greeter.GreeterClient(channel);

        var reply = await client.SayHelloAsync(new HelloRequest { Name = "Joelso" });
        Console.WriteLine($"Resposta do servidor: {reply.Message}");
    }
}
