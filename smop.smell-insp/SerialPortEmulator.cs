using System;
using System.Linq;
using System.Threading;

namespace Smop.SmellInsp;

public static class SimulatedData
{
    public static Data Generate()
    {
        float SumAll(int sensorIndex)
        {
            float result = 0;
            for (int i = 0; i < Common.Simulation.SntGains.Length; i++)
            {
                result += _impacts[i][sensorIndex] * Common.Simulation.SntGains[i];
            }
            return result;
        }

        var resistance = new float[Data.ResistantCount];
        for (int i = 0; i < resistance.Length; i++)
        {
            resistance[i] = i switch
            {
                >= 2 and <= 4 => SumAll(10) + Random.Range(0.3f) + TS(100),
                >= 5 and <= 7 => SumAll(2) + Random.Range(0.3f) + TS(10),
                >= 8 and <= 10 => SumAll(1) + Random.Range(0.3f) + TS(3),
                >= 11 and <= 13 => SumAll(0) + Random.Range(0.3f) + TS(30),
                >= 21 and <= 23 => SumAll(8) + Random.Range(0.3f) + TS(40),
                >= 24 and <= 26 => SumAll(5) + Random.Range(0.3f) + TS(500),
                >= 27 and <= 29 => SumAll(4) + Random.Range(0.3f) + TS(30),
                >= 34 and <= 36 => SumAll(13) + Random.Range(0.3f) + TS(10),
                >= 37 and <= 39 => SumAll(9) + Random.Range(0.3f) + TS(2),
                >= 40 and <= 42 => SumAll(6) + Random.Range(0.3f) + TS(200),
                >= 43 and <= 45 => SumAll(3) + Random.Range(0.3f) + TS(6),
                >= 50 and <= 52 => SumAll(14) + Random.Range(0.3f) + TS(20),
                >= 53 and <= 55 => SumAll(12) + Random.Range(0.3f) + TS(30),
                >= 56 and <= 58 => SumAll(11) + Random.Range(0.3f) + TS(100),
                >= 59 and <= 61 => SumAll(7) + Random.Range(0.3f) + TS(20),

                _ => 4.6f + Random.Range(0.3f)
            };
        }
        return new Data(
            resistance,
            24.6f + Random.Range(0.1f), // Temperature
            47.1f + Random.Range(0.1f)  // Humidity
        );
    }

    // internal

    private static class Random
    {
        public static float Range(float pm) => (float)((_random.NextDouble() - 0.5) * 2 * pm);

        static readonly System.Random _random = new();
    }

    static readonly float[][] _impacts = new float[Common.OdorPrinter.MaxOdorCount][]
    {
        new float[15] { 0.1f, 0.3f, 0f, 0f, 0.9f, 1f, 0.1f, 0f, 0.2f, 0f, 0f, 0.6f, 0f, 0.8f, 0f},
        new float[15] { 0f, 0.1f, 0f, 0.8f, 0.4f, 0f, 0.3f, 0f, 0f, 0.8f, 1f, 0.2f, 0f, 0f, 0.5f},
        new float[15] { 0.4f, 0.6f, 0.2f, 0f, 0.9f, 0,2f, 0f, 0.1f, 0.1f, 0f, 0.5f, 1f, 1f, 0.1f},
        new float[15] { 0.3f, 0.9f, 0.9f, 0f, 0f, 0.5f, 0f, 1f, 0f, 0.1f, 0.4f, 0.8f, 0f, 0.8f, 0.1f},
        new float[15] { 0.5f, 0f, 0f, 0.1f, 0.9f, 0.5f, 0f, 0.7f, 1f, 0.1f, 0f, 0f, 0.4f, 0.4f, 0f},
    };

    static readonly long _start = DateTime.Now.Ticks;

    private static float TS(double gain)
    {
        var x = (double)(DateTime.Now.Ticks - _start) / 10_000_000;  // half-period is 1 second
        return (float)(gain * x / Math.Sqrt(1 + x * x));
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
