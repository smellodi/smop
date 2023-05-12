using System.Linq;
using System.Threading;

namespace Smop.SmellInsp;

/// <summary>
/// This class is used to emulate communication with the device via <see cref="CommPort"/>
/// </summary>
public class SerialPortEmulator : ISerialPort
{
    public bool IsOpen => _isOpen;

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

    public void Open() { _isOpen = true; }

    public void Close()
    {
        _isOpen = false;
        Thread.Sleep(10);
    }

    public string ReadLine()
    {
        while (_isOpen)
        {
            Thread.Sleep(10);
            if (_hasDataToSend)
            {
                _hasDataToSend = false;
                return GenerateData();
            }
            else if (_hasInfoToSend)
            {
                _hasInfoToSend = false;
                return $"V{VERSION}{SEPARATOR}{ADDRESS}";
            }
        }

        throw new System.Exception("Closed");
    }

    public void WriteLine(string text)
    {
        text = text.Trim();
        if (text == $"{Command.GET_INFO}")
            _hasInfoToSend = true;
    }

    // Internal

    readonly char SEPARATOR = ';';
    readonly string VERSION = "1.0.0";
    readonly string ADDRESS = "A1:5F:53:C7:31:0B";

    bool _isOpen = false;
    bool _timerAutoStop = false;
    bool _hasDataToSend = false;
    bool _hasInfoToSend = false;

    System.Timers.Timer _dataTimer = new(1800);

    private string GenerateData()
    {
        var resistance = new float[64];
        for (int i = 0; i < resistance.Length; i++)
        {
            resistance[i] = 4.6f + Random.Range(0.3f);
        }
        var data = new Data(
            resistance,
            24.6f + Random.Range(0.1f),
            47.1f + Random.Range(0.1f)
        );
        return string.Join(SEPARATOR,
            new string[] {
                "start",
                string.Join(SEPARATOR, data.Resistances.Select(v => v.ToString("F1"))),
                data.Temperature.ToString("F1"),
                data.Humidity.ToString("F1")
            }
        );
    }

    private static class Random
    {
        public static float Range(float pm) => (float)((_random.NextDouble() - 0.5) * 2 * pm);

        static System.Random _random = new();
    }
}
