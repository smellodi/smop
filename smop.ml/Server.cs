using Smop.Common;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smop.ML;

internal abstract class Server : IDisposable
{
    public event EventHandler<Recipe>? RecipeReceived;

    public virtual bool IsClientConnected { get; }

    public abstract void Dispose();

    public async Task SendAsync<T>(T packet)
    {
        if (IsClientConnected)
        {
            var data = JsonSerializer.Serialize(packet, _serializerOptions);
            ScreenLogger.Print("[MlServer] sent: " + data.Max(700));
            await SendTextAsync(data);
        }
    }

    // Internal

    protected abstract Task SendTextAsync(string data);

    protected readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    protected void ParseJson(string json)
    {
        ScreenLogger.Print("[MlServer] received: " + json.Max(700));

        try
        {
            Recipe recipe = Communicator.IsDemo ? CreateDemoRecipe(json) : CreateRecipe(json);
            RecipeReceived?.Invoke(this, recipe);
        }
        catch (Exception ex)
        {
            ScreenLogger.Print($"[MlServer] error: {ex.Message}");
        }
    }

    private Recipe CreateDemoRecipe(string json)
    {
        var packet = JsonSerializer.Deserialize<float[]>(json, _serializerOptions);
        if (packet?.Length == 3)
        {
            return new Recipe("Recipe", (int)packet[0], 0, new ChannelRecipe[] {
                new ChannelRecipe(1, packet[1], -1),
                new ChannelRecipe(2, packet[2], -1),
            });
        }
        else
        {
            throw new Exception($"invalid packet data length");
        }
    }

    private Recipe CreateRecipe(string json)
    {
        var packet = JsonSerializer.Deserialize<Packet>(json, _serializerOptions);
        if (packet?.Type == PacketType.Recipe)
        {
            json = JsonSerializer.Serialize(packet.Content);
            return JsonSerializer.Deserialize<Recipe>(json, _serializerOptions)!;
        }
        else
        {
            throw new Exception($"unknown packet type: {packet?.Type}");
        }
    }
}
