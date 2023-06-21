namespace Smop.SmellInsp;

/// <summary>
/// The interface lists the minimum fuctionality required from <see cref="System.IO.Ports.SerialPort"/>.
/// This functionality is implemented in a serial port emulator <see cref="SerialPortEmulator"/>.
/// </summary>
public interface ISerialPort
{
    /// <summary>
    /// Interval between data events, seconds
    /// </summary>
    readonly static double Interval = 1.8;

    bool IsOpen { get; }

    void Open();
    void WriteLine(string text);
    string ReadLine();
    void Close();
}
