using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smop.ML;

internal abstract class Simulator : IDisposable
{
    public abstract void Dispose();


    // Internal

    protected readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    int[] _channelIDs = new int[1] { 0 };

    protected abstract Task SendData(string data);

    protected async void ParseJson(string json)
    {
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
                _channelIDs = config.Printer.Channels.Select(c => c.Slot).ToArray();
            }
            else if (packet.Type == PacketType.Measurement)
            {
                await Task.Delay(2000);

                var recipe = new Recipe("Recipe for you!", _channelIDs.Select(c => new ChannelRecipe(c, 10, 25, 0)).ToArray());
                json = JsonSerializer.Serialize(new Packet(PacketType.Recipe, recipe));
                Console.WriteLine("[CLIENT] recipe sent");
                await SendData(json);
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
}
