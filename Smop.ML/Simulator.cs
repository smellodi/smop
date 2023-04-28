using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WatsonTcp;

namespace Smop.ML;

internal class Simulator : IDisposable
{
    public Simulator()
    {
        _client = new WatsonTcpClient("127.0.0.1", Server.Port);
        _client.Events.ServerConnected += ServerConnected;
        _client.Events.ServerDisconnected += ServerDisconnected;
        _client.Events.MessageReceived += MessageReceived;
        _client.Callbacks.SyncRequestReceived = SyncRequestReceived;
        _client.Connect();
    }

    public void Dispose()
    {
        _client.Dispose();
    }


    // Internal

    readonly WatsonTcpClient _client;
    readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    int[] _channelIDs = new int[1] { 0 };


    private async void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        string json = Encoding.UTF8.GetString(args.Data);
        Console.WriteLine("[CLIENT] received: " + json.Max(700));

        try
        {
            var packet = JsonSerializer.Deserialize<Packet>(json, _serializerOptions);
            if (packet == null)
            {
                throw new Exception($"[CLIENT] packet is not a valid JSON:\n  {json}");
            }

            if (packet.Type == PacketType.Config)
            {
                json = JsonSerializer.Serialize(packet.Content, _serializerOptions);
                var config = JsonSerializer.Deserialize<Config>(json, _serializerOptions)!;
                _channelIDs = config.Channels.Select(c => c.Slot).ToArray();
            }
            else if (packet.Type == PacketType.Measurement)
            {
                await Task.Delay(2000);

                var recipe = new Recipe("Recipe for you!", _channelIDs.Select(c => new ChannelRecipe(c, 10, 25, 0)).ToArray());
                json = JsonSerializer.Serialize(new Packet(PacketType.Recipe, recipe));
                Console.WriteLine("[CLIENT] recipe sent");
                await _client.SendAsync(json);
            }
            else
            {
                Console.WriteLine($"[CLIENT] unknown packet type: {packet.Type}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void ServerConnected(object? sender, ConnectionEventArgs args)
    {
        Console.WriteLine("[CLIENT] connected");
    }

    private void ServerDisconnected(object? sender, DisconnectionEventArgs args)
    {
        Console.WriteLine("[CLIENT] disconnected");
    }

    private SyncResponse SyncRequestReceived(SyncRequest req)
    {
        return new SyncResponse(req, "Ack Sync");
    }
}
