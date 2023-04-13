using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WatsonTcp;

namespace Smop.ML;

internal class Simulator
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

    // Internal

    WatsonTcpClient _client;

    private async void MessageReceived(object? sender, MessageReceivedEventArgs args)
    {
        string json = Encoding.UTF8.GetString(args.Data);
        Console.WriteLine("[CLIENT] received: " + json.Max(700));

        try
        {
            var measurement = JsonSerializer.Deserialize<Measurement>(json);
            if (measurement != null)
            {
                await Task.Delay(2000);

                json = JsonSerializer.Serialize(new Request(RequestType.Recipe, new Recipe("Recipe for you!")));
                Console.WriteLine("[CLIENT] recipe sent");
                await _client.SendAsync(json);
            }
            else
            {
                Console.WriteLine("[CLIENT] measurement is expected");
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
