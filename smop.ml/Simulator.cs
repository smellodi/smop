using Smop.Common;
using System;
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
    float _threshold = 0;
    int _maxSteps = 10;
    int _step = 0;

    const float RMSQ = 0.2f;

    protected abstract Task SendData(string data);

    protected async void ParseJson(string json)
    {
        ScreenLogger.Print("[CLIENT] received: " + json.Max(700));

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
                _channelIDs = config.Printer.Channels.Select(c => c.Id).ToArray();
                _step = 0;
                _maxSteps = config.MaxIterationNumber;
                _threshold = config.Threshold;
            }
            else if (packet.Type == PacketType.Measurement)
            {
                await Task.Delay(2000);

                _step++;
                var rmsq = RMSQ / _step;
                bool isFinished = rmsq < _threshold || _step >= _maxSteps;

                var recipe = new Recipe("Recipe for you!", isFinished ? 1 : 0, rmsq, _channelIDs.Select(c => new ChannelRecipe(c, 10, 25)).ToArray());
                json = JsonSerializer.Serialize(new Packet(PacketType.Recipe, recipe));
                ScreenLogger.Print("[CLIENT] recipe sent");
                await SendData(json);
            }
            else
            {
                ScreenLogger.Print($"[CLIENT] unknown packet type: {packet.Type}");
            }
        }
        catch (Exception ex)
        {
            ScreenLogger.Print(ex.Message);
        }
    }
}
