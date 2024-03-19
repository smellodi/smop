using System;
using System.Linq;
using System.Threading;

namespace Smop.SmellInsp;

public static class SimulatedData
{
    public record class SimulatedResponseGains(float First, float Second);

    public static SimulatedResponseGains Gains = new(8, 6);

    public static Data Generate()
    {
        var resistance = new float[Data.ResistantCount];
        for (int i = 0; i < resistance.Length; i++)
        {
            resistance[i] = i switch
            {
                1 => 0.1f * Gains.First + Random.Range(0.3f),
                5 => 0.35f * Gains.First + Random.Range(0.3f),
                9 => 0.8f * Gains.First + Random.Range(0.3f),
                13 => 1.0f * Gains.First + Random.Range(0.3f),

                2 => 0.1f * Gains.Second + Random.Range(0.3f),
                6 => 0.35f * Gains.Second + Random.Range(0.3f),
                10 => 0.8f * Gains.Second + Random.Range(0.3f),
                14 => 1.0f * Gains.Second + Random.Range(0.3f),

                0 => 0.05f * Gains.First + 0.05f * Gains.Second + Random.Range(0.3f),
                4 => 0.1f * Gains.First + 0.05f * Gains.Second + Random.Range(0.3f),
                8 => 0.1f * Gains.First + 0.2f * Gains.Second + Random.Range(0.3f),
                12 => 0.5f * Gains.First + 0.5f * Gains.Second + Random.Range(0.3f),

                _ => 4.6f + Random.Range(0.3f)
            };
        }
        return new Data(
            resistance,
            24.6f + Random.Range(0.1f),
            47.1f + Random.Range(0.1f)
        );
    }

    private static class Random
    {
        public static float Range(float pm) => (float)((_random.NextDouble() - 0.5) * 2 * pm);

        static readonly System.Random _random = new();
    }
}

/// <summary>
/// This class is used to emulate communication with the device via <see cref="CommPort"/>
/// </summary>
internal class SerialPortEmulator : ISerialPort, IDisposable
{
    public bool IsOpen { get; private set; } = false;

    public SerialPortEmulator()
    {
        _dataTimer.Elapsed += (s, e) =>
        {
            if (_timerAutoStop)
            {
                _timerAutoStop = false;
                _dataTimer.Stop();
            }

            _hasDataToSend = true;
        };
        _dataTimer.Start();
    }

    public void Open() { IsOpen = true; }

    public void Close()
    {
        IsOpen = false;
        Thread.Sleep(10);
    }

    public string ReadLine()
    {
        while (IsOpen)
        {
            Thread.Sleep(10);
            if (_hasDataToSend)
            {
                _hasDataToSend = false;
                var data = SimulatedData.Generate();
                return string.Join(SEPARATOR,
                    new string[] {
                        "start",
                        string.Join(SEPARATOR, data.Resistances.Select(v => v.ToString("F1"))),
                        data.Temperature.ToString("F1"),
                        data.Humidity.ToString("F1")
                    }
                );
            }
            else if (_hasInfoToSend)
            {
                _hasInfoToSend = false;
                return $"V{VERSION}{SEPARATOR}{ADDRESS}";
            }
        }

        throw new Exception("Closed");
    }

    public void WriteLine(string text)
    {
        text = text.Trim();
        if (text == $"{Command.GET_INFO}")
            _hasInfoToSend = true;
    }

    public void Dispose()
    {
        _dataTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly static char SEPARATOR = ';';
    readonly static string VERSION = "1.0.0";
    readonly static string ADDRESS = "A1:5F:53:C7:31:0B";

    bool _timerAutoStop = false;
    bool _hasDataToSend = false;
    bool _hasInfoToSend = false;

    readonly System.Timers.Timer _dataTimer = new((int)(CommPort.SamplingInterval * 1000));
}
