using Smop.Common;
using Smop.OdorDisplay;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smop.ML;

internal abstract class Simulator : IDisposable
{
    public bool UseScopeMode => false;

    public abstract void Dispose();


    // Internal

    protected readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    int[] _channelIDs = new int[2] { (int)Device.ID.Odor1, (int)Device.ID.Odor2 };
    bool _hasDmsSource = false;
    float _threshold = 0.015f;
    int _maxSteps = 20;

    int _step = 0;
    int _sntSampleCount = 0;

    const float FLOW_DURATION_ENDLESS = -1;
    const float RMSE = 0.2f;
    const int SNT_SAMPLE_MAX_COUNT = 10;

    protected abstract Task SendData(string data);

    protected async void ParseJson(string json)
    {
        ScreenLogger.Print("[MlSimul] received: " + json.Max(700));

        try
        {
            if (Communicator.IsDemo)
            {
                await HandleDemoPacket(json);
            }
            else
            {
                await HandlePacket(json);
            }
        }
        catch (Exception ex)
        {
            ScreenLogger.Print($"[MlSimul] {ex.Message}");
        }
    }

    private async Task HandleDemoPacket(string json)
    {
        var packet = JsonSerializer.Deserialize<float[]>(json, _serializerOptions);
        if (packet == null || packet.Length < 3)
        {
            _step = 0;
            return;
        }

        if (packet.Length != (int)(2 + packet[0] * packet[1]))
            throw new Exception("The packet should consist of a row count, column count, and 'row x col' number of values");

        await Task.Delay(2000);

        _step++;
        var rmse = RMSE / _step;
        var usv = UseScopeMode ? 400 + _step * 20 : 0;
        bool isFinished = rmse < _threshold || _step >= _maxSteps;

        var recipe = new float[] { isFinished ? 1 : 0, 7 + _step * 2, 25 - _step * 2, rmse, usv };
        json = JsonSerializer.Serialize(recipe);
        ScreenLogger.Print("[MlSimul] recipe sent");
        await SendData(json);
    }

    private async Task HandlePacket(string json)
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
                var rmse = RMSE / _step;
                var usv = UseScopeMode ? 400 + _step * 20 : 0;
                bool isFinished = rmse < _threshold || _step >= _maxSteps;

                var recipe = new Recipe("Normal", isFinished ? 1 : 0, rmse, usv, 
                    _channelIDs.Select(c => new ChannelRecipe(c, 10 + _step * 2, FLOW_DURATION_ENDLESS)).ToArray());
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
}
