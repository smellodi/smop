using System;
using System.Runtime.InteropServices;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;

namespace SMOP.Comm
{
    /// <summary>
    /// A subset of Windows ERROR_XXX codes that are used to return from methods,
    /// except the last value
    /// </summary>
    public enum Error
    {
        Success = 0,                    // ERROR_SUCCESS, all is OK
        BadDataFormat = 0x0B,           // ERROR_BAD_FORMAT
        WrongDevice = 0x0C,             // ERROR_INVALID_ACCESS
        InvalidData = 0x0D,             // ERROR_INVALID_DATA
        NotReady = 0x15,                // ERROR_NOT_READY, the port is not open yer/already
        CRC = 0x17,                     // ERROR_CRC
        AccessFailed = 0x1F,            // ERROR_GEN_FAILURE, ports were available, but access was not successful
        OpenFailed = 0x6E,              // ERROR_OPEN_FAILED, not succeeded to open a port, 
        Timeout = 0x5B4,                // ERROR_TIMEOUT
        DeviceError = 0x8000,           // Error received from the device
    }

    /// <summary>
    /// We use this (error, reason) pair to return from public methods
    /// </summary>
    public class Result
    {
        public Error Error;
        public string? Reason;

        public override string ToString()
        {
            return "0x" + ((int)Error).ToString("X4") + $" ({Error}): {Reason}";
        }
    }

    public class Request
    {
        public const int WAIT_INTERVAL = 1000;

        public PacketType Type { get; }
        public Packet? Message { get; private set; } = null;
        public Error Error { get; private set; } = Error.Success;
        public bool IsValid { get; set; } = true;
        public long Duration => (System.Diagnostics.Stopwatch.GetTimestamp() - _timestamp) / 10000;

        public Request(PacketType type)
        {
            Type = type;
        }

        public bool WaitUntilUnlocked()
        {
            _timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            bool result = _mutex.WaitOne(WAIT_INTERVAL);
            return result;
        }

        public void SetReply(Packet message, Error error)
        {
            Message = message;
            Error = error;
            _mutex.Set();
        }

        // Internal

        readonly AutoResetEvent _mutex = new(false);
        long _timestamp = 0;
    }

    /// <summary>
    /// COM port functionality common for both MFC and PID
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
        /// Fires when high-level error (Sistem.IO.Ports.SerialPort) is received from COM port
        /// </summary>
        public event EventHandler<Result>? RequestResult;

        /// <summary>
        /// Fires when the device pushes MFC message without a request
        /// The handler MUST be async!
        /// </summary>
        public event EventHandler<MessageMFCResult?>? MFC;

        /// <summary>
        /// Fires when the device pushes PID Sample message without a request
        /// The handler MUST be async!
        /// </summary>
        public event EventHandler<MessageSample?>? Sample;

        /// <summary>
        /// Debug info
        /// The handler MUST be async!
        /// </summary>
        public event EventHandler<string>? Debug;

        public bool IsOpen { get; protected set; } = false;
        public bool IsDebugging { get; private set; } = false;
        public SerialError? PortError { get; private set; } = null;

        /// <summary>
        /// Closes the communication port
        /// </summary>
        public void Stop()
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
        /// <param name="portName">COM1..COM255</param>
        /// <returns>Error code and description</returns>
        public Result Start(string? portName)
        {
            PortError = null;

            IsDebugging = string.IsNullOrEmpty(portName);

            if (!IsDebugging)
            {
                try
                {
                    _port = OpenSerialPort(portName!);
                    _port.ErrorReceived += (s, e) =>
                    {
                        PortError = e.EventType;
                        RequestResult?.Invoke(this, new Result()
                        {
                            Error = (Error)Marshal.GetLastWin32Error(),
                            Reason = $"COM internal error ({e.EventType})"
                        });
                    };
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
                    Stop();
                    return new Result()
                    {
                        Error = Error.OpenFailed,
                        Reason = "The port was created but is still closed"
                    };
                }

                _readingThread = new Thread(ReadMessagesThread);
                _readingThread.Start();
            }

            PortError = null;

            IsOpen = true;

            Opened?.Invoke(this, new EventArgs());

            return new Result()
            {
                Error = Error.Success,
                Reason = "Port opened"
            };
        }
        public Error Request(Packets.QueryVersion cmd, out Packets.Ack? result, out Packets.Version? version)
        {
            var error = Request(cmd as Packet, out result, out Packet? v);
            version = v as Packets.Version;
            return error;
        }
        /*
        public Error Request(MessageNoop cmd, out MessageResult? result)
        {
            return Request(cmd as Packet, out result);
        }
        public Error Request(MessageSetPID cmd, out MessageResult? result)
        {
            return Request(cmd as Packet, out result);
        }
        public Error Request(MessageSetSampling cmd, out MessageResult? result)
        {
            return Request(cmd as Packet, out result);
        }
        public Error Request(MessageSetOutput cmd, out MessageResult? result)
        {
            return Request(cmd as Packet, out result);
        }

        public Error Request(MessageSendMFC cmd, out MessageResult result, out MessageMFCResult reply)
        {
            var error = Request(cmd, out result, out reply);
            return error;
        }
        public Error Request(MessageGetSample cmd, out MessageResult result, out MessageSample reply)
        {
            var error = Request(cmd, out result, out reply);
            return error;
        }
        */

        // Internal

        const int PORT_SPEED = 230400;
        const Parity PORT_PARITY = Parity.None;
        const StopBits PORT_STOP_BITS = StopBits.One;
        const int PORT_WRITE_TIMEOUT = 300;          // only for writing.. reading should be able to hand until it returns with some data 

        const int MAX_BUFFER_LENGTH = 990;
        const byte PREAMBLE_BYTE = 0xCC;
        const int PREAMBLE_LENGTH = 3;
        const int PAYLOAD_LENGTH_OFFSET = 6;
        const int MIN_PACKAGE_LENGTH = 9; // the packet minimum length: PREAMBLE_LENGTH + 1B type + 1B from + 1B to + 2B payload length + 2B CRC

        readonly Queue<Request> _requests = new();
        readonly System.Diagnostics.Stopwatch _stopWatch = System.Diagnostics.Stopwatch.StartNew();

        static CommPort? _instance;

        SerialPort? _port;
        Thread? _readingThread;

        long Timestamp => _stopWatch.ElapsedMilliseconds;


        /// <summary>
        /// Creates and opens a serial port
        /// </summary>
        /// <param name="portName">COM1..COM255</param>
        /// <returns>The port</returns>
        private SerialPort OpenSerialPort(string portName)
        {
            if (!portName.StartsWith("COM") || !int.TryParse(portName[3..], out int portID) || portID < 1 || portID > 255)
            {
                throw new ArgumentException("Invalid COM port name");
            }

            SerialPort port = new(portName)
            {
                StopBits = PORT_STOP_BITS,
                Parity = PORT_PARITY,
                BaudRate = PORT_SPEED,
                DataBits = 8,
                DtrEnable = true,
                RtsEnable = true,
                DiscardNull = false,
                WriteTimeout = PORT_WRITE_TIMEOUT,
                NewLine = "\r"
            };

            port.Open();

            Thread.Sleep(50);       // TODO - maybe, we do not need this
            try
            {
                port.BaseStream.Flush();
            }
            catch { }  // do nothing

            return port;
        }

        /// <summary>
        /// Sends a request, returns an error and a reply
        /// </summary>
        /// <param name="cmd">Request that requires a single reply (Noop, SetOutput, SetPID or SetSampling) </param>
        /// <param name="ack">Result returned by the device</param>
        /// <returns>Error</returns>
        private Error Request(Packet cmd, out Packets.Ack? ack)
        {
            ack = null;
            Error error;

            lock (this)
            {
                Write(cmd);

                error = ReadMessage(PacketType.Ack, out Packet? msg);

                if (error == Error.Success)
                {
                    ack = msg as Packets.Ack;
                }
            }

            return error;
        }

        /// <summary>
        /// Sends a request, returns an error and two replies
        /// </summary>
        /// <param name="cmd">Request that requires two replies</param>
        /// <param name="result">Result returned by the device</param>
        /// <param name="reply">The reply with some info that was requested</param>
        /// <returns>Error</returns>
        private Error Request(Packet cmd, out Packets.Ack? result, out Packet? reply)
        {
            reply = null;
            result = null;

            Error error;

            lock (this)
            {
                Write(cmd);

                if (cmd.SecondResponse == PacketType.None)
                {
                    error = ReadMessage(PacketType.Ack, out Packet? msg);

                    if (error == Error.Success)
                    {
                        result = msg as Packets.Ack;
                    }
                }
                else
                {
                    error = ReadMessages(new PacketType[]
                    {
                        cmd.SecondResponse,
                        PacketType.Ack,
                    }, out Packet[] msgs);

                    if (error == Error.Success)
                    {
                        result = msgs.Length > 0 ? msgs[0] as Packets.Ack : null;
                        reply = msgs.Length > 1 ? msgs[1] : null;
                    }
                }
            }

            return error;
        }

        /// <summary>
        /// Writes message to the port
        /// </summary>
        /// <param name="message">Message to write</param>
        private void Write(Packet message)
        {
            PortError = null;

            var bytes = message.ToArray();

            Debug?.Invoke(this, $"SND {message}");

            //await _port.BaseStream.WriteAsync(bytes);
            _port?.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Reads message from the port. Gets the packet payload length from the second byte.
        /// Returns error if the time is out.
        /// </summary>
        /// <param name="message">Message to be filled with data read from the port</param>
        /// <returns>Error.Success or an error code</returns>
        private Error Read(out Packet? message)
        {
            byte[] buffer = new byte[MAX_BUFFER_LENGTH];

            int bytesRemaining = MIN_PACKAGE_LENGTH;
            int packageStartOffset = 0;
            int bufferOffset = 0;
            bool isLengthKnown = false;
            int payloadLength = 0;

            // Try to receive bytes; wait more data in POLL_PERIOD pieces;
            while (bytesRemaining > 0)
            {
                int readCount;
                try
                {
                    readCount = _port?.Read(buffer, bufferOffset, bytesRemaining) ?? 0;
                    if (readCount == 0)
                        break;
                }
                catch
                {
                    message = null;
                    return Error.AccessFailed;
                }

                if (PortError != null)           // return immediately (with error) if port read fails.
                {
                    message = null;
                    return (Error)Marshal.GetLastWin32Error();
                }

                int packageBytesRead = bufferOffset + readCount - packageStartOffset;
                if (!isLengthKnown && packageBytesRead >= MIN_PACKAGE_LENGTH - 1)  // After we have all bytes received from the port except payload and CRC , we try to 
                {
                    // ensure this is a valid start of the package
                    while (packageStartOffset < bufferOffset + readCount - PREAMBLE_LENGTH)
                    {
                        if (buffer[packageStartOffset] == PREAMBLE_BYTE &&
                            buffer[packageStartOffset + 1] == PREAMBLE_BYTE &&
                            buffer[packageStartOffset + 2] == PREAMBLE_BYTE)
                        {
                            break;
                        }

                        bytesRemaining++;
                        packageStartOffset++;
                    }

                    packageBytesRead = bufferOffset + readCount - packageStartOffset;
                    if (packageBytesRead >= MIN_PACKAGE_LENGTH - 1)
                    {
                        // payload is little-endian WORD
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
                message = null;
                return Error.Timeout;
            }

            packageStartOffset += PREAMBLE_LENGTH;
            int packageEndOffset = packageStartOffset + (MIN_PACKAGE_LENGTH - PREAMBLE_LENGTH) + payloadLength;
            message = new Packet(buffer[packageStartOffset..packageEndOffset]);

            if (!message.IsValidCRC)
            {
                return Error.CRC;
            }
            else
            {
                message = message.Type switch
                {
                    PacketType.Ack => Packets.Ack.From(message),
                    _ => throw new Exception($"Package type '{message.Type}' is not supported")
                };
                return Error.Success;
            }
        }

        /// <summary>
        /// Calls to this method must be inside lock(this) { } block
        /// </summary>
        /// <param name="type">Message type</param>
        /// <param name="msg">Message received</param>
        /// <returns>Error</returns>
        private Error ReadMessage(PacketType type, out Packet? msg)
        {
            var result = ReadMessages(new PacketType[] { type }, out Packet[] msgs);
            msg = msgs.Length > 0 ? msgs[0] : null;
            return result;
        }

        /// <summary>
        /// Calls to this method must be inside lock(this) { } block
        /// </summary>
        /// <param name="types">Message types</param>
        /// <param name="msgs">Messages received</param>
        /// <returns></returns>
        private Error ReadMessages(PacketType[] types, out Packet[] msgs)
        {
            //Debug?.Invoke(this,  $"REQ {string.Join(',', types)}");

            List<Packet> replies = new();
            List<Request> requests = new();

            lock (_requests)
            {
                foreach (var type in types)
                {
                    Request req = new(type);
                    _requests.Enqueue(req);
                    requests.Add(req);
                }
            }

            foreach (var req in requests)
            {
                if (!req.WaitUntilUnlocked())
                {
                    req.IsValid = false;
                    Debug?.Invoke(this, $"REQ !TIMEOUT after {req.Duration} ms: {req.Type}");
                    msgs = replies.ToArray();
                    return Error.Timeout;
                }

                if (req.Message != null && req.IsValid)
                {
                    replies.Add(req.Message);
                }
            }

            msgs = replies.ToArray();
            return Error.Success;
        }

        private void ReadMessagesThread()
        {
            int PAUSE_INTERVAL = 10;

            while (true)
            {
                try { Thread.Sleep(PAUSE_INTERVAL); }
                catch (ThreadInterruptedException) { break; }  // will exit the loop upon Interrupt is called

                Debug?.Invoke(this,  $"READ [cycle start]");

                Error error = Read(out Packet? message);
                if (error != Error.Success)
                {
                    Debug?.Invoke(this, $"READ [error '{error}']");
                    continue;
                }

                lock (_requests)
                {
                    Request? req = null;
                    while (_requests.Count > 0 && !(req = _requests.Peek()).IsValid)
                    {
                        _requests.Dequeue();
                        Debug?.Invoke(this, $"REQ !CLEANED after {req.Duration} ms: {req.Type}");
                        req = null;
                    }

                    if (message != null)
                    {
                        if (req?.Type == message.Type)
                        {
                            _requests.Dequeue();

                            Debug?.Invoke(this, $"RCV [{req.Duration}ms] {message}");
                            req.SetReply(message, error);
                        }
                        /*else if (message.Type == MessageType.Sample)
                        {
                            Debug?.Invoke(this, $"RCV [pop] {message}");
                            Sample?.Invoke(this, message as MessageSample);
                        }
                        else if (message.Type == MessageType.MFCResult)
                        {
                            Debug?.Invoke(this, $"RCV [pop] {message}");
                            MFC?.Invoke(this, message as MessageMFCResult);
                        }*/
                        else
                        {
                            Debug?.Invoke(this, $"RCV [pop] !UNEXPECTED {message}");
                        }
                    }
                    else
                    {
                        Debug?.Invoke(this, $"READ ! no message");
                    }
                }

                Debug?.Invoke(this, $"READ [cycle ends]");
            }
        }
    }
}
