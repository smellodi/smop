using System;
using System.Linq;
using System.Threading;

namespace Smop.SmellInsp;

public static class SimulatedData
{
    public static Data Generate()
    {
        var resistance = new float[64];
        for (int i = 0; i < resistance.Length; i++)
        {
            resistance[i] = 4.6f + Random.Range(0.3f);
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
