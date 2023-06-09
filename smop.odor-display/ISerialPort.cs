﻿namespace Smop.OdorDisplay;

/// <summary>
/// The interface lists the minimum fuctionality required from <see cref="System.IO.Ports.SerialPort"/>.
/// This functionality is implemented in a serial port emulator <see cref="SerialPortEmulator"/>.
/// </summary>
public interface ISerialPort
{
    bool IsOpen { get; }

    void Open();
    void Write(byte[] buffer, int offset, int count);
    int Read(byte[] buffer, int offset, int count);
    void Close();
}
