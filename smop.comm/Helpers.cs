using System.Runtime.InteropServices;

namespace SMOP.Comm
{
    /// <summary>
    /// Convenient byte-level manipulation of 16b integers,
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct TwoBytes
    {
        [FieldOffset(0)]
        public byte Byte0;
        [FieldOffset(1)]
        public byte Byte1;
        [FieldOffset(0)]
        public ushort Short;

        public TwoBytes(int value) : this((ushort)value) { }

        public TwoBytes(ushort value)
        {
            Byte0 = 0;
            Byte1 = 0;
            Short = value;
        }

        public TwoBytes(byte[] bytes)
        {
            Short = 0;
            Byte0 = bytes[0];
            Byte1 = bytes[1];
        }

        public byte[] ToArray() => new byte[] { Byte0, Byte1 };
        public static byte[] ToArray(int value) => ToArray((ushort)value);
        public static byte[] ToArray(ushort value) => new TwoBytes(value).ToArray();
        public static ushort ToShort(byte[] array) => new TwoBytes(array).Short;
    }

    /// <summary>
    /// Convenient byte-level manipulation of 32b integers and floats
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct FourBytes
    {
        [FieldOffset(0)]
        public byte Byte0;
        [FieldOffset(1)]
        public byte Byte1;
        [FieldOffset(2)]
        public byte Byte2;
        [FieldOffset(3)]
        public byte Byte3;
        [FieldOffset(0)]
        public ushort Short0;
        [FieldOffset(2)]
        public ushort Short1;
        [FieldOffset(0)]
        public uint Int;
        [FieldOffset(0)]
        public float Float;

        public FourBytes(double value) : this((float)value) { }

        public FourBytes(float value)
        {
            Byte0 = 0;
            Byte1 = 0;
            Byte2 = 0;
            Byte3 = 0;
            Short0 = 0;
            Short1 = 0;
            Int = 0;
            Float = value;
        }

        public FourBytes(uint value)
        {
            Byte0 = 0;
            Byte1 = 0;
            Byte2 = 0;
            Byte3 = 0;
            Short0 = 0;
            Short1 = 0;
            Float = 0;
            Int = value;
        }

        public FourBytes(byte[] bytes)
        {
            Short0 = 0;
            Short1 = 0;
            Int = 0;
            Float = 0;
            Byte0 = bytes[0];
            Byte1 = bytes[1];
            Byte2 = bytes[2];
            Byte3 = bytes[3];
        }

        public byte[] ToArray() => new byte[] { Byte0, Byte1, Byte2, Byte3 };
        public static byte[] ToArray(double value) => ToArray((float)value);
        public static byte[] ToArray(float value) => new FourBytes(value).ToArray();
        public static byte[] ToArray(int value) => new FourBytes((uint)value).ToArray();
        public static byte[] ToArray(uint value) => new FourBytes(value).ToArray();
        public static float ToFloat(byte[] array) => new FourBytes(array).Float;
        public static uint ToInt(byte[] array) => new FourBytes(array).Int;
    }

    public static class BoolExt
    {
        public static string AsFlag(this bool self) => self ? "ON" : "OFF";
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
    }
    
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
     */


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
}
