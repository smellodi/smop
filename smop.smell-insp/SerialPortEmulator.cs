using System;
using System.Linq;
using System.Threading;

namespace Smop.SmellInsp;

public static class SimulatedData
{
    public record class GasImpact(float First, float Second);

    public static GasImpact Gains { get; set; } = new(8, 6);

    public static Data Generate()
    {
        var resistance = new float[Data.ResistantCount];
        for (int i = 0; i < resistance.Length; i++)
        {
            resistance[i] = i switch
            {
                >= 2 and <= 4 => _impacts.Gas1[10] * Gains.First + _impacts.Gas2[10] * Gains.Second + Random.Range(0.3f) + TS(100),
                >= 5 and <= 7 => _impacts.Gas1[2] * Gains.First + _impacts.Gas2[2] * Gains.Second + Random.Range(0.3f) + TS(10),
                >= 8 and <= 10 => _impacts.Gas1[1] * Gains.First + _impacts.Gas2[1] * Gains.Second + Random.Range(0.3f) + TS(3),
                >= 11 and <= 13 => _impacts.Gas1[0] * Gains.First + _impacts.Gas2[0] * Gains.Second + Random.Range(0.3f) + TS(30),
                >= 21 and <= 23 => _impacts.Gas1[8] * Gains.First + _impacts.Gas2[8] * Gains.Second + Random.Range(0.3f) + TS(40),
                >= 24 and <= 26 => _impacts.Gas1[5] * Gains.First + _impacts.Gas2[5] * Gains.Second + Random.Range(0.3f) + TS(500),
                >= 27 and <= 29 => _impacts.Gas1[4] * Gains.First + _impacts.Gas2[4] * Gains.Second + Random.Range(0.3f) + TS(30),
                >= 34 and <= 36 => _impacts.Gas1[13] * Gains.First + _impacts.Gas2[13] * Gains.Second + Random.Range(0.3f) + TS(10),
                >= 37 and <= 39 => _impacts.Gas1[9] * Gains.First + _impacts.Gas2[9] * Gains.Second + Random.Range(0.3f) + TS(2),
                >= 40 and <= 42 => _impacts.Gas1[6] * Gains.First + _impacts.Gas2[6] * Gains.Second + Random.Range(0.3f) + TS(200),
                >= 43 and <= 45 => _impacts.Gas1[3] * Gains.First + _impacts.Gas2[3] * Gains.Second + Random.Range(0.3f) + TS(6),
                >= 50 and <= 52 => _impacts.Gas1[14] * Gains.First + _impacts.Gas2[14] * Gains.Second + Random.Range(0.3f) + TS(20),
                >= 53 and <= 55 => _impacts.Gas1[12] * Gains.First + _impacts.Gas2[12] * Gains.Second + Random.Range(0.3f) + TS(30),
                >= 56 and <= 58 => _impacts.Gas1[11] * Gains.First + _impacts.Gas2[11] * Gains.Second + Random.Range(0.3f) + TS(100),
                >= 59 and <= 61 => _impacts.Gas1[7] * Gains.First + _impacts.Gas2[7] * Gains.Second + Random.Range(0.3f) + TS(20),

                _ => 4.6f + Random.Range(0.3f)
            };
        }
        return new Data(
            resistance,
            24.6f + Random.Range(0.1f),
            47.1f + Random.Range(0.1f)
        );
    }

    // internal

    private static class Random
    {
        public static float Range(float pm) => (float)((_random.NextDouble() - 0.5) * 2 * pm);

        static readonly System.Random _random = new();
    }

    private record class FeatureImpact(float[] Gas1, float[] Gas2);

    static readonly FeatureImpact _impacts = new(
        new float[15] { 0.1f, 0.3f, 0f, 0f, 0.9f, 1f, 0.1f, 0f, 0.2f, 0f, 0f, 0.6f, 0f, 0.8f, 0f},
        new float[15] { 0f, 0.1f, 0f, 0.8f, 0.4f, 0f, 0.3f, 0f, 0f, 0.8f, 1f, 0.2f, 0f, 0f, 0.5f}
    );

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
