using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smop.SmellInsp;

public class DataCollector
{
    public int SampleCount { get; set; } = 3;

    public bool IsEnabled => _smellInsp.IsOpen;

    /// <summary>
    /// Collect few data samples
    /// </summary>
    /// <returns>Returns mean values of the samples collected</returns>
    public async Task<Data?> Collect(Action<int, float> progressCallback)
    {
        _sntSamples.Clear();

        if (_smellInsp.IsOpen)
        {
            _smellInsp.Data += SmellInsp_Data;
        }
        else
        {
            return null;
        }

        while (_sntSamples.Count < SampleCount)
        {
            await Task.Delay(200);
            progressCallback(_sntSamples.Count, 100 * _sntSamples.Count / SampleCount);
        }

        _smellInsp.Data -= SmellInsp_Data;

        return Data.GetMean(_sntSamples);
    }

    // Internal

    readonly CommPort _smellInsp = CommPort.Instance;

    readonly List<Data> _sntSamples = new();

    private async void SmellInsp_Data(object? sender, Data e)
    {
        try
        {
            await Task.Run(() =>
            {
                _sntSamples.Add(e);
            });
        }
        catch (TaskCanceledException) { }
    }
}
