using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SMOP.Comm.Packets
{
    /*NEW
    public enum DeviceOutputID : byte
    {
        OdorValve = 0,
        UserValve = 1,
        Reserved1 = 2,
        NoiseValve = 3, 
        PumpRelay = 4
    }
    */
    public enum DeviceState : byte
    {
        Off = 0,
        On = 1
    }
    /*
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DeviceOutput
    {
        public DeviceOutputID ID { get; }
        public DeviceState State { get; }
        /// <summary>
        /// The pulse length in milliseconds for 'on' state.
        /// If 0 is given, the output does not turn off automatically.
        /// The parameter is ignored when the output is to be turned off.
        /// </summary>
        public int PulseLengthMs { get; }
        public DeviceOutput(DeviceOutputID id, DeviceState state, int pulseLengthMs = 0)
        {
            ID = id;
            State = state;
            PulseLengthMs = pulseLengthMs;
        }
        public override string ToString()
        {
            return State == DeviceState.Off ? $"{ID} = off" : $"{ID} = on ({PulseLengthMs})";
        }
    }*/

    public enum PacketType
    {
        None = 0,
        Ack = 0xFA,
        QueryVersion = 0x70,
        Version = 0x71,
        QueryDevices = 0x50,
        Devices = 0x51,
        QueryCapabilities = 0x40,
        Capabilities = 0x41,
        SetActuators = 0x20,
        Data = 0x31,
        SetSystem = 0x60,
        SetMeasurements = 0x80,
        Reset = 0x90,
    }

    public abstract class Packet
    {
        public enum Result
        {
            OK = 0,                 // No error, request was executed without problems
            InvalidValue = 0xF6,         // Parameter has an invalid value
            NotAvailable = 0xF5,    // Resource is not available
            OutOfMemory = 0xF4,     // There is not enough memory for the operation
            InvalidMode = 0xF3,     // Requested operation is not possible in current operating mode
            Timeout = 0xF2,         // Operation timed out
            NoData = 0xF1,          // Data is not available
            UnknownPacket = 0xF0,   // Unknown or unsupported packet type
            InvalidLength = 0xEF,   // Invalid amount of data
            InvalidDeviceID = 0xEE, // Invalid index or device number 
            Busy = 0xED,            // Device is busy and can’t perform the requested operation
            Error = 0x80,           // Non-specific error
        }

        public PacketType Type => _type;
        public PacketType SecondResponse { get; protected set; } = PacketType.None;

        //public byte CRC => _checksum;
        public bool IsValidCRC => CalcChecksum() == _checksum;

        public Packet(byte[] buffer)
        {
            _type = (PacketType)buffer[0];
            _from = buffer[1];
            _to = buffer[2];
            _payload = buffer[5..^1];
            _checksum = buffer[^1];
            System.Diagnostics.Debug.Assert(_checksum == CalcChecksum());
        }

        public Packet(PacketType type, byte[]? payload = null)
        {
            _type = type;
            _payload = payload;
            _checksum = CalcChecksum();
        }

        /// <summary>
        /// Makes an array of bytes out of a structure
        /// </summary>
        /// <typeparam name="T">Structure type to convert to bytes</typeparam>
        /// <param name="str">Structure instance</param>
        /// <returns>Message instance</returns>
        /*
        public static Message From<T>(T str) where T : Message
        {
            Type type = str switch
            {
                MessageResult => Type.Result,
                MessageNoop => Type.Noop,
                MessageSetOutput => Type.SetOutput,
                MessageSendMFC => Type.SendMFC,
                MessageMFCResult => Type.MFCResult,
                MessageSetPID => Type.SetPID,
                MessageSetSampling => Type.SetSampling,
                MessageGetSample => Type.GetSample,
                MessageSample => Type.Sample,
                _ => throw new NotImplementedException($"Type of {str} in Message.From")
            };

            int size = Marshal.SizeOf(str);

            if (size > 0)
            {
                byte[] bytes = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, bytes, 0, size);
                Marshal.FreeHGlobal(ptr);

                return new Message(type, bytes);
            }
            else
            {
                return new Message(type, null);
            }
        }*/

        /// <summary>
        /// Makes a structure out of array of bytes
        /// </summary>
        /// <typeparam name="T">Structure type to restore from bytes</typeparam>
        /// <returns>Structure instance</returns>
        /*
        public T As<T>() where T : new()
        {
            var str = new T();

            int size = Marshal.SizeOf(str);

            if (size != Length)
            {
                throw new InvalidCastException($"Payload ({typeof(T)}) and type ({_type}) mismatch");
            }

            if (size > 0)
            {
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(_payload, 0, ptr, size);
                str = (T)Marshal.PtrToStructure(ptr, str.GetType());
                Marshal.FreeHGlobal(ptr);
            }

            return str;
        }*/

        public byte[] ToArray()
        {
            List<byte> result = new()
            {
                (byte)_type,
                _from, _to,
                (byte)Length
            };
            for (int i = 0; i < Length; i++)
            {
                result.Add(_payload![i]);
            }
            result.Add(_checksum);

            return result.ToArray();
        }

        public override string ToString()
        {
            return $"{_type}";
        }


        // Internal

        protected const byte SENDER_DEVICE = 0xF0;
        protected const byte SENDER_PC = 0xF1;

        protected readonly PacketType _type;
        protected readonly byte[]? _payload;
        protected readonly byte _checksum;
        protected byte _from;
        protected byte _to;

        protected int Length => _payload?.Length ?? 0;

        protected byte CalcChecksum()
        {
            long result = (long)_type + Length;
            for (int i = 0; i < Length; i++)
            {
                result += _payload![i];
            }

            return (byte)~(result + 1);
        }

        /// <summary>
        /// Makes an array of bytes out of a structure
        /// </summary>
        /// <typeparam name="T">Structure type to convert to bytes</typeparam>
        /// <param name="str">Structure instance</param>
        /// <returns>Arrays of bytes</returns>
        /*
        protected static byte[] ToBytes<T>([DisallowNull]T str)
        {
            int size = Marshal.SizeOf(str);
            byte[] bytes = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, bytes, 0, size);
            Marshal.FreeHGlobal(ptr);

            return bytes;
        }*/

        /// <summary>
        /// Makes an array of bytes out of an array of structures
        /// </summary>
        /// <typeparam name="T">Structure type to convert to bytes</typeparam>
        /// <param name="str">Array of structure instances</param>
        /// <returns>Arrays of bytes</returns>
        protected static byte[] ToBytes<T>(T[] str)
        {
            int size = Marshal.SizeOf(str[0]);
            byte[] bytes = new byte[size * str.Length];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            for (int i = 0; i < str.Length; i++)
            {
                var item = str[i];
                if (item != null)
                {
                    Marshal.StructureToPtr(item, ptr, true);
                    Marshal.Copy(ptr, bytes, i * size, size);
                }
            }
            Marshal.FreeHGlobal(ptr);

            return bytes;
        }

        /// <summary>
        /// Makes a structure out of array of bytes
        /// </summary>
        /// <typeparam name="T">Structure type to restore from bytes</typeparam>
        /// <param name="bytes">Array of bytes</param>
        /// <returns>Structure instance</returns>
        protected static T? FromBytes<T>(byte[] bytes) where T : new()
        {
            var str = new T();

            int size = Marshal.SizeOf(str);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(bytes, 0, ptr, size);

            str = (T?)Marshal.PtrToStructure(ptr, str.GetType());
            Marshal.FreeHGlobal(ptr);

            return str;
        }
    }
    /*
    // Incoming

    public class MessageMFCResult : Packet
    {
        public new string Result { get; }
        public MessageMFCResult(string result) : base(PacketType.None, System.Text.Encoding.ASCII.GetBytes(result))
        {
            Result = result;
        }
        public override string ToString()
        {
            return $"{_type} [{Result}]";
        }
    }

    public class MessageSample : Packet
    {
        public readonly int Time;           // milliseconds, or 0 in response of MessageGetSample
        public readonly float PID0;         // volts; PID in the system
        public readonly float PID1;         // volts; PID in the mask
        public readonly float Thermistor0;  // ohms; 100kΩ at 25°C and a temperature coefficient of -4.5%/K. Exhale shows as decreasing resistance.
        public readonly float Thermistor1;  // ohms; could be out of use: a missing thermistor is indicated with positive infinity.
        public readonly float IC0;          // volts; a temperature sensor close to the butanol evaporator, 10mV/K (0°C = 2.732V)
        public readonly float IC1;          // volts; a humidity sensor measuring delivered air, 0.958V + 0.03068V/%
        public readonly float IC2;          // volts; not used
        public readonly float Aux0;         // 0...10V input 1 voltage in volts, not used
        public readonly float Aux1;         // 0...10V input 1 voltage in volts, not used
        public readonly float Aux2;         // 0...10V input 1 voltage in volts, not used
        public readonly DeviceState OdorValve;// 0 = off, 1 = on
        public readonly DeviceState UserValve;// 0 = off, 1 = on
        public readonly DeviceState Reserved1;// 0 = off, 1 = on
        public readonly DeviceState FakeValve;// 0 = off, 1 = on
        public readonly DeviceState PumpRelay;// 0 = off, 1 = on

        public static MessageSample? From(Packet msg)
        {
            if (msg?.Type != PacketType.Data || msg?.Payload?.Length != 49)
            {
                return null;
            }

            return new MessageSample(msg.ToArray());
        }
        private MessageSample(byte[] data) : base(data)
        {
            if (_payload == null)
                return;

            Time = (int)new BtoD(_payload[0..4]).D;
            PID0 = new BtoD(_payload[4..8]).F;
            PID1 = new BtoD(_payload[8..12]).F;
            Thermistor0 = new BtoD(_payload[12..16]).F;
            Thermistor1 = new BtoD(_payload[16..20]).F;
            IC0 = new BtoD(_payload[20..24]).F;
            IC1 = new BtoD(_payload[24..28]).F;
            IC2 = new BtoD(_payload[28..32]).F;
            Aux0 = new BtoD(_payload[32..36]).F;
            Aux1 = new BtoD(_payload[36..40]).F;
            Aux2 = new BtoD(_payload[40..44]).F;
            OdorValve = (DeviceState)_payload[44];
            UserValve = (DeviceState)_payload[45];
            Reserved1 = (DeviceState)_payload[46];
            FakeValve = (DeviceState)_payload[47];
            PumpRelay = (DeviceState)_payload[48];
        }
        public override string ToString()
        {
            return $"{_type} [{Time}, {PID0}, {PID1}, {Thermistor0}, {Thermistor1}, {IC0}, {IC1}, {IC2}, {Aux0}, {Aux1}, {Aux2}, {OdorValve}, {UserValve}, {Reserved1}, {FakeValve}, {PumpRelay}]";
        }
        public static string[] Fields => new string[]
        {
            nameof(Time),
            nameof(PID0),
            nameof(PID1),
            nameof(Thermistor0),
            nameof(Thermistor1),
            nameof(IC0),
            nameof(IC1),
            nameof(IC2),
            nameof(Aux0),
            nameof(Aux1),
            nameof(Aux2),
        };

        public string[] ToStrings()
        {
            return new string[]
            {
                Time.ToString(),
                PID0.ToString(),
                PID1.ToString(),
                float.IsFinite(Thermistor0) ? Thermistor0.ToString() : "0",
                float.IsFinite(Thermistor1) ? Thermistor1.ToString() : "0",
                IC0.ToString(),
                IC1.ToString(),
                IC2.ToString(),
                Aux0.ToString(),
                Aux1.ToString(),
                Aux2.ToString(),
            };
        }
    }

    // Outcoming
    
    /// <summary>
    /// Sets the output state. Note that the API supports 0-5 payloads
    /// </summary>
    public class MessageSetOutput : Packet
    {
        public DeviceOutput[] DeviceOutputs { get; }
        public MessageSetOutput(DeviceOutput[] deviceOutputs) : base(PacketType.SetActuators, ToBytes(deviceOutputs))
        {
            DeviceOutputs = deviceOutputs;

            int maxItems = Enum.GetValues(typeof(DeviceOutputID)).Length;
            if (deviceOutputs.Length > maxItems)
            {
                throw new ArgumentOutOfRangeException($"Max number of device outputs is {maxItems}");
            }
        }
        public override string ToString()
        {
            string data = string.Join(", ", DeviceOutputs.Select(output => output.ToString()));
            return $"{_type} [{data}]";
        }
    }

    /// <summary>
    /// Requests MFC data
    /// </summary>
    public class MessageSendMFC : Packet
    {
        public string Cmd { get; }
        public char Channel { get; }
        public MessageSendMFC(string cmd) : base(PacketType.SendMFC, System.Text.Encoding.ASCII.GetBytes(cmd))
        {
            Cmd = cmd;
            Channel = cmd[0];
            SecondResponse = PacketType.MFCResult;
        }
        public override string ToString()
        {
            return $"{_type} [{Cmd.Replace(MFC.DATA_END, ';')}]";
        }
    }

    /// <summary>
    /// Triggers PIDs (enables/disables their lamps)
    /// </summary>
    public class MessageSetPID : Packet
    {
        public DeviceState State { get; }
        public MessageSetPID(DeviceState state) : base(PacketType.SetPID, new byte[] { (byte)state })
        {
            State = state;
        }
        public override string ToString()
        {
            return $"{_type} [{State}]";
        }
    }

    /// <summary>
    /// Sets/removes automatic PID samples transmission
    /// When sampling is running, the device will send periodic samples 
    /// until the sampling is stopped with another MessageSetSampling.
    /// </summary>
    public class MessageSetSampling : Packet
    {
        public ushort Interval { get; }

        /// <summary>Constructor</summary>
        /// <param name="interval">The value range 100-2000ms with the step 100ms.
        /// Value 0 is handled as a special case: it stops sampling.</param>
        public MessageSetSampling(ushort interval) : base(PacketType.SetSampling, new BtoW(interval).ToArray())
        {
            Interval = interval;
            if (interval > 2000 || (interval % 100) != 0)
            {
                throw new ArgumentException($"Sampling interval {interval} is not valid");
            }
        }
        public override string ToString()
        {
            return $"{_type} [{Interval}]";
        }
    }

    /// <summary>
    /// Requests the latest PID sample
    /// </summary>
    public class MessageGetSample : Packet
    {
        public MessageGetSample() : base(PacketType.GetSample, new byte[] { })
        {
            SecondResponse = PacketType.Sample;
        }
    }
    */

    public class Request : Packet
    {
        public Request(PacketType type, byte[]? payload = null) : base(type, payload)
        {
            _from = SENDER_PC;
            _to = SENDER_DEVICE;
        }
    }

    public class Response : Packet
    {
        public byte[] Payload => _payload!;
        public Response(PacketType type, byte[] payload) : base(type, payload)
        {
            _from = SENDER_DEVICE;
            _to = SENDER_PC;
            System.Diagnostics.Debug.Assert(payload?.Length > 0);
        }
    }
}
