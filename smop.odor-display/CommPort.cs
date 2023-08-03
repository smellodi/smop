using Smop.OdorDisplay.Packets;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;

namespace Smop.OdorDisplay;

/// <summary>
/// Communication over COM port with the odor printer
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
    /// Fires when the device pushes Data packet a request
    /// The handler MUST be async!
    /// </summary>
    public event EventHandler<Data>? Data;

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
    /// Sends a request to the device and waits until a reponse is received
    /// (at least <see cref="Packets.Type.Ack"/> packet, unless an error occurs)
    /// </summary>
    /// <typeparam name="T">Request type</typeparam>
    /// <typeparam name="U">Response type</typeparam>
    /// <param name="request">Request</param>
    /// <param name="ack"><see cref="Packets.Type.Ack"/> response packet </param>
    /// <param name="response">Another optional response packet</param>
    /// <returns>Error code and description</returns>
    public Result Request<T, U>(T request, out Ack? ack, out U? response)
        where T : Request
        where U : Response
    {
        var error = GetResponse(request, out ack, out Response? res);   // does not return until the response is received, or an error occurs
        if (error == Error.Success && ack?.Result != Packets.Result.OK)
        {
            error = Error.DeviceError;
        }
        response = res as U;

        return new Result()
        {
            Error = error,
            Reason = error switch
            {
                Error.Success => "OK",
                Error.InvalidData => "The response missing or excessing data packets",
                Error.CRC => "Invalid CRC",
                Error.AccessFailed => "Failed to reading from the port",
                Error.Timeout => "No response was received within a reasonable time",
                Error.DeviceError => ack!.Result switch
                {
                    Packets.Result.InvalidValue => "Some request parameter has invalid value",
                    Packets.Result.NotAvailable => "Resource is not available",
                    Packets.Result.OutOfMemory => "There isn’t enough memory for the operation",
                    Packets.Result.InvalidMode => "Requested operation is not valid in the current operating mode",
                    Packets.Result.Timeout => "Operation timed out",
                    Packets.Result.NoData => "There’s no data to retrieve",
                    Packets.Result.UnknownPacket => "Unknown or unsupported packet type",
                    Packets.Result.InvalidLength => "Invalid amount of data",
                    Packets.Result.InvalidDeviceID => "Invalid index or device number",
                    Packets.Result.Busy => "Device is busy and can’t perform the requested operation",
                    Packets.Result.Error => "Unknown error",
                    _ => $"Internal error: unknown device error {ack!.Result}"
                },
                _ => $"Internal error: unexpected error {error}"
            }
        };
    }


    // Internal

    const int MAX_BUFFER_LENGTH = 990;
    const int PAYLOAD_LENGTH_OFFSET = 6;
    const int MIN_RESPONSE_LENGTH = 9;      // minimum length: Packet.PREAMBLE_LENGTH + 1B type + 1B from + 1B to + 2B payload length + 1B CRC

    readonly Queue<TimedRequest> _requests = new();

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
    /// Sends a request, returns an error and two replies
    /// </summary>
    /// <param name="request">Request that requires two replies</param>
    /// <param name="result">Result returned by the device</param>
    /// <param name="response">Optional reply with some info that was requested</param>
    /// <returns>Error</returns>
    private Error GetResponse(Request request, out Ack? result, out Response? response)
    {
        response = null;
        result = null;

        Error error;

        lock (this)
        {
            Write(request);

            if (request.ExpectedResponse == Packets.Type.None)
            {
                error = ReadResponse(Packets.Type.Ack, out Response? ack);

                if (error == Error.Success)
                {
                    result = ack as Ack;
                }
            }
            else
            {
                error = ReadResponses(new Packets.Type[]
                {
                    request.ExpectedResponse,
                    Packets.Type.Ack,
                }, out Response[] responses);

                if (error == Error.Success)
                {
                    if (responses.Length == 2)
                    {
                        response = responses.Length > 0 ? responses[0] : null;
                        result = responses.Length > 1 ? responses[1] as Ack : null;
                    }
                    else
                    {
                        error = Error.InvalidData;
                    }
                }
            }
        }

        return error;
    }

    /// <summary>
    /// Calls to this method must be inside lock(this) { } block
    /// </summary>
    /// <param name="type">Packet type</param>
    /// <param name="res">Packet received</param>
    /// <returns><see cref="Error.Success"/> or an error code</returns>
    private Error ReadResponse(Packets.Type type, out Response? res)
    {
        var result = ReadResponses(new Packets.Type[] { type }, out Response[] responses);
        res = responses.Length > 0 ? responses[0] : null;
        return result;
    }

    /// <summary>
    /// Calls to this method must be inside lock(this) { } block
    /// </summary>
    /// <param name="types">Packet types</param>
    /// <param name="responses">Packets received</param>
    /// <returns><see cref="Error.Success"/> or <see cref="Error.Timeout"/></returns>
    private Error ReadResponses(Packets.Type[] types, out Response[] responses)
    {
        List<TimedRequest> requests = new();
        List<Response> replies = new();

        lock (_requests)
        {
            foreach (var type in types)
            {
                TimedRequest req = new(type);
                _requests.Enqueue(req);
                requests.Add(req);
            }
        }

        foreach (var req in requests)
        {
            if (!req.WaitUntilReceived())       // The thread stops here until the response is received (true), or a timeout (false)
            {
                Debug?.Invoke(this, $"REQ !TIMEOUT after {req.Duration} ms: {req.Type}");
                responses = replies.ToArray();
                return Error.Timeout;
            }

            if (req.Response != null && req.IsValid)
            {
                replies.Add(req.Response);
            }
        }

        responses = replies.ToArray();
        return Error.Success;
    }

    /// <summary>
    /// Writes a packet to the port
    /// </summary>
    /// <param name="packet">Packet to write</param>
    private void Write(Packet packet)
    {
        PortError = null;

        var bytes = packet.ToArray();

        var packetStr = packet.ToString();
        var separator = packetStr.Contains('\n') ? "\n    " : " ";
        Debug?.Invoke(this, $"SND {packetStr}{separator}({packet.ByteString})");

        //await _port.BaseStream.WriteAsync(bytes);
        _port?.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Reads packets from the port. Gets the packet payload length from the second byte.
    /// </summary>
    /// <param name="packet">Packet to be filled with data received from the port</param>
    /// <returns><see cref="Error.Success"/> or an error code</returns>
    private Error Read(out Response? packet)
    {
        byte[] buffer = new byte[MAX_BUFFER_LENGTH];

        int bytesRemaining = MIN_RESPONSE_LENGTH;
        int packageStartOffset = 0;
        int bufferOffset = 0;
        bool isLengthKnown = false;
        int payloadLength = 0;

        while (bytesRemaining > 0)
        {
            int readCount;
            try
            {
                readCount = _port?.Read(buffer, bufferOffset, bytesRemaining) ?? 0;
                if (readCount == 0)
                {
                    packet = null;
                    return Error.NotReady;
                }
            }
            catch
            {
                packet = null;
                return Error.AccessFailed;
            }

            if (PortError != null)           // return immediately (with error) if port read fails.
            {
                packet = null;
                return (Error)Marshal.GetLastWin32Error();
            }

            int packageBytesRead = bufferOffset + readCount - packageStartOffset;
            if (!isLengthKnown && packageBytesRead >= MIN_RESPONSE_LENGTH - 1)  // After we have all bytes received from the port except payload and CRC , we try to 
            {
                // ensure this is a valid start of the package
                while (packageStartOffset < bufferOffset + readCount - Packet.PREAMBLE_LENGTH)
                {
                    if (buffer[packageStartOffset] == Packet.PREAMBLE_BYTE &&
                        buffer[packageStartOffset + 1] == Packet.PREAMBLE_BYTE &&
                        buffer[packageStartOffset + 2] == Packet.PREAMBLE_BYTE)
                    {
                        break;
                    }

                    bytesRemaining++;
                    packageStartOffset++;
                }

                packageBytesRead = bufferOffset + readCount - packageStartOffset;
                if (packageBytesRead >= MIN_RESPONSE_LENGTH - 1)
                {
                    // payload length is a little-endian WORD
                    payloadLength =                             // get the length of the payload, so we known 
                        buffer[packageStartOffset + PAYLOAD_LENGTH_OFFSET] +
                        (buffer[packageStartOffset + PAYLOAD_LENGTH_OFFSET + 1] << 8);
                    bytesRemaining += payloadLength;            // how many bytes (including checksum) we need to read more
                    isLengthKnown = true;
                }
            }

            bytesRemaining -= readCount;
            bufferOffset += readCount;
        }

        if (bytesRemaining > 0)
        {
            packet = null;
            return Error.Timeout;
        }

        packageStartOffset += Packet.PREAMBLE_LENGTH;
        int packageEndOffset = packageStartOffset + (MIN_RESPONSE_LENGTH - Packet.PREAMBLE_LENGTH) + payloadLength;
        packet = new Response(buffer[packageStartOffset..packageEndOffset]);

        if (!packet.IsValidCRC)
        {
            return Error.CRC;
        }
        else
        {
            packet = packet.Type switch
            {
                Packets.Type.Ack => Ack.From(packet),
                Packets.Type.Version => Packets.Version.From(packet),
                Packets.Type.Devices => Devices.From(packet),
                Packets.Type.Capabilities => Capabilities.From(packet),
                Packets.Type.Data => Packets.Data.From(packet),
                _ => null
            };
            return packet == null ? Error.InvalidData : Error.Success;
        }
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

            Error error = Read(out Response? response);
            if (error == Error.NotReady)
            {
                return;
            }
            else if (error != Error.Success)
            {
                Debug?.Invoke(this, $"ERR '{error}'");
            }

            lock (_requests)
            {
                TimedRequest? request = null;
                while (_requests.Count > 0 && !(request = _requests.Peek()).IsValid)
                {
                    _requests.Dequeue();
                    Debug?.Invoke(this, $"REQ !CLEANED after {request.Duration} ms: {request.Type}");
                    request = null;
                }

                if (response != null)
                {
                    if (request?.Type == response.Type)
                    {
                        _requests.Dequeue();
                        Debug?.Invoke(this, $"RCV [{request.Duration}ms] {response.Type} ({response.ByteString})");

                        if (error == Error.Success)
                        {
                            /// allows <see cref="ReadResponses(Packets.Type[], out Response[])"/> to continue
                            request.SetResponse(response);
                        }
                    }
                    else if (response.Type == Packets.Type.Data)
                    {
                        Debug?.Invoke(this, $"RCV [pop] {response.Type} ({response.ByteString})");
                        Data?.Invoke(this, (Data)response);
                    }
                    else
                    {
                        Debug?.Invoke(this, $"RCV !UNEXPECTED {response.Type} ({response.ByteString})");
                    }
                }
                else
                {
                    Debug?.Invoke(this, $"RCV !NO_PACKET");
                }
            }
        }
    }
}
