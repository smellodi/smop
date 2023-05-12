using System;
using System.Runtime.InteropServices;
using System.IO.Ports;
using System.Threading;
using Result = Smop.OdorDisplay.Result;
using Error = Smop.OdorDisplay.Error;
using System.Linq;

namespace Smop.SmellInsp;

/// <summary>
/// Communication over COM port with Smell Inpspector device
/// Note that this is a simpliefied version of <see cref="Smop.OdorDisplay.CommPort"/>
/// </summary>
public class CommPort
{
    public static CommPort Instance => _instance ??= new();

    /// <summary>
    /// Fires when COM port is opened
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>
    /// Fires when COM port is closed
    /// </summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Fires when high-level OS error is received from COM port
    /// </summary>
    public event EventHandler<Result>? COMError;

    /// <summary>
    /// Fires when a data packet is received
    /// The handler MUST be async!
    /// </summary>
    public event EventHandler <Data>? Data;

    /// <summary>
    /// Fires when an info packet is received
    /// The handler MUST be async!
    /// </summary>
    public event EventHandler<DeviceInfo>? DeviceInfo;

    /// <summary>
    /// Debug info
    /// The handler MUST be async!
    /// </summary>
    public event EventHandler<string>? Debug;

    public bool IsOpen { get; private set; } = false;

    /// <summary>
    /// High-level OS error of the last read/write operation
    /// </summary>
    public SerialError? PortError { get; private set; } = null;

    /// <summary>
    /// Closes the communication port
    /// </summary>
    public void Close()
    {
        try
        {
            _port?.Close();
            _readingThread?.Interrupt();
        }
        finally
        {
            _port = null;
            _readingThread = null;
        }

        if (IsOpen)
        {
            IsOpen = false;
            Closed?.Invoke(this, new EventArgs());
        }
    }

    /// <summary>
    /// Opens the communication port
    /// </summary>
    /// <param name="portName">COM1..COM255, or empty string to start simulation</param>
    /// <returns>Error code and description</returns>
    public Result Open(string? portName)
    {
        PortError = null;

        try
        {
            _port = OpenSerialPort(portName);
        }
        catch (Exception ex)
        {
            Debug?.Invoke(this, $"COM [open] {ex.Message}\n{ex.StackTrace}");
            return new Result()
            {
                Error = Error.OpenFailed,
                Reason = ex.Message
            };
        }

        if (!_port.IsOpen)
        {
            Close();
            return new Result()
            {
                Error = Error.OpenFailed,
                Reason = "The port was created but still closed"
            };
        }

        _readingThread = new Thread(ReadPacketsThread);
        _readingThread.Start();

        PortError = null;

        IsOpen = true;

        Opened?.Invoke(this, new EventArgs());

        return new Result()
        {
            Error = Error.Success,
            Reason = "Port opened"
        };
    }

    /// <summary>
    /// </summary>
    /// <param name="command">The command to send</param>
    public Result Send(Command command)
    {
        PortError = null;

        var cmdStr = command.ToString();

        Debug?.Invoke(this, $"SND {cmdStr}");

        try
        {
            _port?.WriteLine(cmdStr + "\n");
        }
        catch
        {
            return new Result()
            {
                Error = Error.AccessFailed,
                Reason = "Failed to writing to the port"
            };
        }

        return new Result()
        {
            Error = Error.Success,
            Reason = "OK"
        };
    }


    // Internal

    static CommPort? _instance;

    ISerialPort? _port;
    Thread? _readingThread;


    /// <summary>
    /// Creates and opens a serial port
    /// </summary>
    /// <param name="portName">COM1..COM255, or empty to instantiate an emulator</param>
    /// <returns>The port</returns>
    /// <exception cref="ArgumentException">Invalid COM port</exception>
    private ISerialPort OpenSerialPort(string? portName)
    {
        if (string.IsNullOrEmpty(portName))
        {
            var emulator = new SerialPortEmulator();
            emulator.Open();
            return emulator;
        }

        if (!portName.StartsWith("COM") || !int.TryParse(portName[3..], out int portID) || portID < 1 || portID > 255)
        {
            throw new ArgumentException("Invalid COM port");
        }

        var port = new SerialPortCOM(portName);
        port.Open();
        port.ErrorReceived += (s, e) =>
        {
            PortError = e.EventType;
            COMError?.Invoke(this, new Result()
            {
                Error = (Error)Marshal.GetLastWin32Error(),
                Reason = $"COM internal error ({e.EventType})"
            });
        };

        return port;
    }

    /// <summary>
    /// Reads packets from the port. Gets the packet payload length from the second byte.
    /// </summary>
    /// <param name="packet">Packet to be filled with data received from the port</param>
    /// <returns><see cref="Error.Success"/> or an error code</returns>
    private Error Read(out string? response)
    {
        try
        {
            response = _port?.ReadLine();
            if (string.IsNullOrEmpty(response))
            {
                response = null;
                return Error.NotReady;
            }
        }
        catch
        {
            response = null;
            return Error.AccessFailed;
        }

        if (PortError != null)           // return immediately (with error) if port read fails.
        {
            response = null;
            return (Error)Marshal.GetLastWin32Error();
        }

        return Error.Success;
    }

    /// <summary>
    /// The procedure that is running in a separate thread, reading data from the port and assigning replies to requests
    /// </summary>
    private void ReadPacketsThread()
    {
        int PAUSE_INTERVAL = 10;

        while (true)
        {
            try { Thread.Sleep(PAUSE_INTERVAL); }
            catch (ThreadInterruptedException) { break; }  // will exit the loop upon Interrupt is called

            if (!IsOpen)
            {
                break;
            }

            Error error = Read(out string? response);
            if (error == Error.NotReady || error == Error.AccessFailed)
            {
                return;
            }
            else if (error != Error.Success)
            {
                Debug?.Invoke(this, $"ERR '{error}'");
            }
            else
            {
                if (response?.StartsWith("start") ?? false)
                {
                    var p = response.Split(';');
                    if (p.Length != 67)
                    {
                        Debug?.Invoke(this, $"RCV !UNEXPECTED data [count] ({response})");
                    }

                    Data data;
                    try
                    {
                        data = new Data(
                            p[1..65].Select(v => float.Parse(v)).ToArray(),
                            float.Parse(p[65]),
                            float.Parse(p[66])
                        );
                    }
                    catch
                    {
                        Debug?.Invoke(this, $"RCV !UNEXPECTED data [format] ({response})");
                        continue;
                    }

                    Debug?.Invoke(this, $"RCV {response}");
                    Data?.Invoke(this, data);
                }
                else if (response?.StartsWith("V") ?? false)
                {
                    var p = response.Split(';');
                    if (p.Length != 2)
                    {
                        Debug?.Invoke(this, $"RCV !UNEXPECTED ver [count] ({response})");
                    }

                    var deviceInfo = new DeviceInfo(p[0][1..], p[1]);

                    Debug?.Invoke(this, $"RCV {response}");
                    DeviceInfo?.Invoke(this, deviceInfo);
                }
                else
                {
                    Debug?.Invoke(this, $"RCV !UNEXPECTED {response}");
                }
            }
        }
    }
}
