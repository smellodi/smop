using Smop.Common;
using System.Text.Json;
using System;
using System.Threading.Tasks;

namespace Smop.ML;

internal class LocalServer : Server
{
    public override bool IsConnected => true;
    public override string DisplayName => "ready";

    public LocalServer() : base() { }

    public override void Dispose()
    {
        // do nothing here?
    }

    // Internal

    Search.SearchAlgorithm? _searchAlgorithm;

    protected override async Task SendTextAsync(string data)
    {
        var packet = JsonSerializer.Deserialize<Packet>(data, _serializerOptions)
            ?? throw new Exception($"packet is not a valid JSON:\n  {data}");

        if (packet.Type == PacketType.Config)
        {
            var json = JsonSerializer.Serialize(packet.Content, _serializerOptions);
            Config config = JsonSerializer.Deserialize<Config>(json, _serializerOptions)!;

            _searchAlgorithm = new Search.SearchAlgorithm(config);
        }
        else if (packet.Type == PacketType.Measurement)
        {
            if (_searchAlgorithm == null)
            {
                return;
            }

            Recipe? recipe = null;

            var json = JsonSerializer.Serialize(packet.Content, _serializerOptions);
            Content? content = JsonSerializer.Deserialize<Content>(json, _serializerOptions)!;

            if (content.Source == Source.DMS)
            {
                content = JsonSerializer.Deserialize<DmsMeasurement>(json, _serializerOptions)!;
            }
            else if (content.Source == Source.SNT)
            {
                content = JsonSerializer.Deserialize<SntMeasurement>(json, _serializerOptions)!;
            }
            else if (content.Source == Source.PID)
            {
                content = JsonSerializer.Deserialize<PIDMeasurement>(json, _serializerOptions)!;
            }
            else
            {
                ScreenLogger.Print($"[ML] unknown data source: {content.Source}");
                content = null;
            }

            if (content != null && _searchAlgorithm.AddMeasurement(content))
            {
                recipe = await _searchAlgorithm.GetRecipe();
            }

            if (recipe != null)
            {
                json = JsonSerializer.Serialize(new Packet(PacketType.Recipe, recipe));
                ScreenLogger.Print("[ML] recipe sent");

                ParseJson(json);
            }
        }
        else
        {
            ScreenLogger.Print($"[ML] unknown packet type: {packet.Type}");
        }
    }
}
