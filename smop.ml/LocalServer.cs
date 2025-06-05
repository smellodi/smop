using Smop.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smop.ML;

internal class LocalServer : Server
{
    public override bool IsConnected => true;
    public override string DisplayName => "ready";

    public LocalServer() : base() { }

    public void SetSearchAlgorithmParameters(string parameters)
    {
        _deParams = parameters
            .Split(' ')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p) && p.IndexOf('=') > 0)
            .Select(p =>
            {
                var pair = p.Split('=');
                if (double.TryParse(pair[1], out double value))
                    return new KeyValuePair<string, double?>(pair[0], value);
                return new KeyValuePair<string, double?>(pair[0], null);
            })
            .ToDictionary();
    }

    public override async Task SendAsync<T>(T data)
    {
        if (data is not Packet packet)
            return;

        if (packet.Type == PacketType.Config && packet.Content is Config config)
        {
            ScreenLogger.Print($"[ML] got {packet.Type}");
            _searchAlgorithm = new Search.SearchAlgorithm(config, _deParams);
        }
        else if (packet.Type == PacketType.Measurement)
        {
            if (_searchAlgorithm == null)
                return;

            Content? content = null;
            if (packet.Content is DmsMeasurement dms)
            {
                ScreenLogger.Print($"[ML] got DMS {dms.Setup.Usv} x {dms.Setup.Ucv}");
                content = dms;
            }
            else if (packet.Content is SntMeasurement snt)
            {
                ScreenLogger.Print($"[ML] got SNT {snt.Data.Features.Length} features");
                content = snt;
            }
            else if (packet.Content is PIDMeasurement pid)
            {
                ScreenLogger.Print($"[ML] got PID {pid.Data:F3} V");
                content = pid;
            }
            else
            {
                ScreenLogger.Print($"[ML] unknown data source: {(packet.Content as Content)?.Source}");
            }

            Recipe? recipe = null;

            if (content != null && _searchAlgorithm.AddMeasurement(content))
            {
                recipe = await _searchAlgorithm.GetRecipe();
            }

            if (recipe != null)
            {
                FireRecipeEvent(recipe);
                ScreenLogger.Print("[ML] recipe sent");
            }
        }
        else
        {
            ScreenLogger.Print($"[ML] unknown packet type: {packet.Type}");
        }
    }

    public override void Dispose()
    {
        CleanUp();
        GC.SuppressFinalize(this);
    }

    public override void CleanUp()
    {
        base.CleanUp();

        _searchAlgorithm?.Dispose();
        _searchAlgorithm = null;
    }

    // Internal

    Dictionary<string, double?> _deParams = new();
    Search.SearchAlgorithm? _searchAlgorithm;
}
