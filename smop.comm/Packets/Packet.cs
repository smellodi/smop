using System;
using System.Collections.Generic;

namespace SMOP.Comm.Packets
{
    /// <summary>
    /// Packet type
    /// </summary>
    public enum Type : byte
    {
        None = 0,

        /// <summary>
        /// Device sends ack to every request
        /// </summary>
        Ack = 0xFA,

        /// <summary>
        /// Queries a version, response is <see cref="Version"/> + <see cref="Ack"/>
        /// </summary>
        QueryVersion = 0x70,
        /// <summary>
        /// Hardware, software and protocol versions
        /// </summary>
        Version = 0x71,
        /// <summary>
        /// Queries available devices, response is <see cref="Devices"/> + <see cref="Ack"/>
        /// </summary>
        QueryDevices = 0x50,
        /// <summary>
        /// List of connected devices
        /// </summary>
        Devices = 0x51,
        /// <summary>
        /// Queries device capabilities, response is <see cref="Capabilities"/> + <see cref="Ack"/>
        /// </summary>
        QueryCapabilities = 0x40,
        /// <summary>
        /// List of device capabilities (true/false)
        /// </summary>
        Capabilities = 0x41,

        /// <summary>
        /// Controls the device state
        /// </summary>
        SetActuators = 0x20,
        /// <summary>
        /// Control other system properties, like fans
        /// </summary>
        SetSystem = 0x60,
        /// <summary>
        /// Starts/stops measurements
        /// </summary>
        SetMeasurements = 0x80,

        /// <summary>
        /// Measurements
        /// </summary>
        Data = 0x31,

        Reset = 0x90,
    }

    /// <summary>
    /// Base class for all packets: <see cref="Request"/> (queries and setters) and <see cref="Response"/>
    /// </summary>
    public abstract class Packet
    {
        public const byte PREAMBLE_BYTE = 0xCC;
        public const int PREAMBLE_LENGTH = 3;
        public const byte DEVICE_MASK = 0x80;

        public Type Type => _type;
        public Type ExpectedResponse { get; protected set; } = Type.None;

        public bool IsValidCRC => CalcChecksum() == _checksum;
        public string ByteString => BitConverter.ToString(ToArray()).Replace("-", ",");

        public byte[] ToArray()
        {
            List<byte> result = new(32)
            {
                (byte)_type,
                _from, _to,
            };
            
            for (int i = 0; i < PREAMBLE_LENGTH; i++)
            {
                result.Insert(0, PREAMBLE_BYTE);
            }
            
            result.AddRange(TwoBytes.ToArray(Length));
            result.AddRange(_payload ?? new byte[] { });
            
            result.Add(_checksum);

            return result.ToArray();
        }

        public override string ToString() => $"{_type}";


        // Internal

        protected const byte SENDER_DEVICE = 0xF0;
        protected const byte SENDER_PC = 0xF1;

        protected readonly Type _type;
        protected readonly byte[]? _payload;
        protected byte _checksum;
        protected byte _from;
        protected byte _to;

        protected int Length => _payload?.Length ?? 0;

        /// <summary>
        /// Constructor to be used when receiving a response from COM port
        /// </summary>
        /// <param name="buffer">Byte array received from the device, without preamble</param>
        protected Packet(byte[] buffer)
        {
            _type = (Type)buffer[0];
            _from = buffer[1];
            _to = buffer[2];
            _payload = buffer[5..^1];
            _checksum = buffer[^1];
        }

        /// <summary>
        /// Base constructor for all requests and simulated responses
        /// </summary>
        /// <param name="type">Packet type</param>
        /// <param name="payload">Packet payload</param>
        protected Packet(Type type, byte[]? payload = null)
        {
            _type = type;
            _from = SENDER_PC;
            _to = SENDER_DEVICE;
            _payload = payload;
            _checksum = CalcChecksum();
        }

        protected byte CalcChecksum()
        {
            var length = Length;
            long result = (long)_type + _from + _to + (length & 0xFF) + ((length >> 8) & 0xFF);
            for (int i = 0; i < length; i++)
            {
                result += _payload![i];
            }

            return (byte)~(result + 1);
        }
    }

    /// <summary>
    /// Used as a base class for all packets moving from PC to the device
    /// </summary>
    public class Request : Packet
    {
        public Request(Type type, byte[]? payload = null) : base(type, payload)
        {
            _from = SENDER_PC;
            _to = SENDER_DEVICE;
            _checksum = CalcChecksum();
        }

        /// <summary>
        /// For debugging only
        /// </summary>
        /// <param name="buffer">Packet bytes</param>
        internal Request(byte[] buffer) : base(buffer) { }

        /// <summary>
        /// For debugging only
        /// </summary>
        /// <param name="buffer">Packet bytes</param>
        internal static Request From(byte[] buffer)
        {
            Type type = (Type)buffer[0];
            return type switch
            {
                Type.QueryVersion => new QueryVersion(buffer),
                Type.QueryDevices => new QueryDevices(buffer),
                Type.QueryCapabilities => new QueryCapabilities(buffer),
                Type.SetActuators => new SetActuators(buffer),
                Type.SetSystem => new SetSystem(buffer),
                Type.SetMeasurements => new SetMeasurements(buffer),
                _ => throw new Exception($"Unknown request type '{type}'")
            };
        }
    }

    /// <summary>
    /// Used as a base class for all packets moving from the device to PC
    /// </summary>
    public class Response : Packet
    {
        public byte[] Payload => _payload!;
        public Response(byte[] buffer) : base(buffer) { }
        protected Response(Type type, byte[] payload) : base(type, payload)
        {
            _from = SENDER_DEVICE;
            _to = SENDER_PC;
            _checksum = CalcChecksum();
        }
    }
}
