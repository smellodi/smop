using Smop.Common;
using System;
using System.Linq;
using System.Printing;
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
    bool _hasDmsSource = false;
    float _threshold = 0;
    int _maxSteps = 10;
    int _step = 0;
    int _sntSampleCount = 0;

    const float RMSQ = 0.2f;
    const int SNT_SAMPLE_MAX_COUNT = 10;

    protected abstract Task SendData(string data);

    protected async void ParseJson(string json)
    {
        ScreenLogger.Print("[MlSimul] received: " + json.Max(700));

        try
        {
            var packet = JsonSerializer.Deserialize<Packet>(json, _serializerOptions);
            if (packet == null)
            {
                throw new Exception($"packet is not a valid JSON:\n  {json}");
            }

            if (packet.Type == PacketType.Config)
            {
                json = JsonSerializer.Serialize(packet.Content, _serializerOptions);
                var config = JsonSerializer.Deserialize<Config>(json, _serializerOptions)!;
                _channelIDs = config.Printer.Channels.Select(c => c.Id).ToArray();
                _hasDmsSource = config.Sources.Contains(Source.DMS);
                _maxSteps = config.MaxIterationNumber;
                _threshold = config.Threshold;

                _step = 0;
            }
            else if (packet.Type == PacketType.Measurement)
            {
                json = JsonSerializer.Serialize(packet.Content, _serializerOptions);
                var content = JsonSerializer.Deserialize<Content>(json, _serializerOptions)!;

                bool createRecipe = false;

                if (content.Source == Source.SNT)
                {
                    _sntSampleCount++;
                    if (!_hasDmsSource && _sntSampleCount == SNT_SAMPLE_MAX_COUNT)
                    {
                        createRecipe = true;
                        _sntSampleCount = 0;
                    }
                }

                if (content.Source == Source.DMS)
                {
                    createRecipe = true;
                }

                if (createRecipe)
                {
                    await Task.Delay(2000);

                    _step++;
                    var rmsq = RMSQ / _step;
                    bool isFinished = rmsq < _threshold || _step >= _maxSteps;

                    var recipe = new Recipe("Normal reproduction", isFinished ? 1 : 0, rmsq, _channelIDs.Select(c => new ChannelRecipe(c, 10 + _step * 2, 25 - _step * 1)).ToArray());
                    json = JsonSerializer.Serialize(new Packet(PacketType.Recipe, recipe));
                    ScreenLogger.Print("[MlSimul] recipe sent");
                    await SendData(json);
                }
            }
            else
            {
                ScreenLogger.Print($"[MlSimul] unknown packet type: {packet.Type}");
            }
        }
        catch (Exception ex)
        {
            ScreenLogger.Print($"[MlSimul] {ex.Message}");
        }
    }
}
