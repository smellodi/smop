using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WatsonTcp;

namespace Smop.ML;

internal class Server : IDisposable
{
    public static int Port => 2339;

    public event EventHandler<Recipe>? RecipeReceived;

    public bool IsClientConnected => _client != Guid.Empty;

    public Server()
    {
        _server = new WatsonTcpServer("127.0.0.1", Port);
        _server.Events.ClientConnected += ClientConnected;
        _server.Events.ClientDisconnected += ClientDisconnected;
        _server.Events.MessageReceived += MessageReceived;
        _server.Callbacks.SyncRequestReceived = SyncRequestReceived;
        _server.Start();
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task SendAsync<T>(T packet)
    {
        if (_client != Guid.Empty)
        {
            var data = JsonSerializer.Serialize(packet, _serializerOptions);
            Console.WriteLine("[SERVER] sent: " + data.Max(700));
            await _server.SendAsync(_client, data);
        }
    }


    // Internal

    readonly WatsonTcpServer _server;
    readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    Guid _client = Guid.Empty;

    private void ClientConnected(object? sender, ConnectionEventArgs args)
    {
        _client = args.Client.Guid;
        Console.WriteLine("[SERVER] connected: " + args.Client.ToString());
    }

    private void ClientDisconnected(object? sender, DisconnectionEventArgs args)
    {
        _client = Guid.Empty;
        Console.WriteLine("[SERVER] disconnected: " + args.Client.ToString() + ": " + args.Reason.ToString());
    }

    private void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        var json = Encoding.UTF8.GetString(args.Data);
        Console.WriteLine("[SERVER] received: " + json.Max(700));

        try
        {
            var packet = JsonSerializer.Deserialize<Packet>(json, _serializerOptions);
            if (packet?.Type == PacketType.Recipe)
            {
                json = JsonSerializer.Serialize(packet.Content);
                var recipe = JsonSerializer.Deserialize<Recipe>(json, _serializerOptions)!;
                RecipeReceived?.Invoke(this, recipe);
            }
            else
            {
                throw new Exception($"[SERVER] unknown packet type: {packet?.Type}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private SyncResponse SyncRequestReceived(SyncRequest req)
    {
        return new SyncResponse(req, "Ack Sync");
    }
}
