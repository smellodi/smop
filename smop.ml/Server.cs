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
            var packet = JsonSerializer.Deserialize<Packet>(json, _serializerOptions);
            if (packet?.Type == PacketType.Recipe)
            {
                json = JsonSerializer.Serialize(packet.Content);
                var recipe = JsonSerializer.Deserialize<Recipe>(json, _serializerOptions)!;
                RecipeReceived?.Invoke(this, recipe);
            }
            else
            {
                throw new Exception($"[MlServer] unknown packet type: {packet?.Type}");
            }
        }
        catch (Exception ex)
        {
            ScreenLogger.Print(ex.Message);
        }
    }
}
