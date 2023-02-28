using System.Runtime.InteropServices;

namespace SMOP.Comm
{
    /// <summary>
    /// Convenient byte-level manipulation of 16b integers,
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct BtoW
    {
        [FieldOffset(0)]
        public byte B0;
        [FieldOffset(1)]
        public byte B1;
        [FieldOffset(0)]
        public ushort W;

        public BtoW(ushort value)
        {
            B0 = 0;
            B1 = 0;
            W = value;
        }

        public BtoW(byte[] bytes)
        {
            W = 0;
            B0 = bytes[0];
            B1 = bytes[1];
        }

        public byte[] ToArray()
        {
            return new byte[] { B0, B1 };
        }
    }

    /// <summary>
    /// Convenient byte-level manipulation of 32b integers and floats
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct BtoD
    {
        [FieldOffset(0)]
        public byte B0;
        [FieldOffset(1)]
        public byte B1;
        [FieldOffset(2)]
        public byte B2;
        [FieldOffset(3)]
        public byte B3;
        [FieldOffset(0)]
        public ushort W0;
        [FieldOffset(2)]
        public ushort W1;
        [FieldOffset(0)]
        public uint D;
        [FieldOffset(0)]
        public float F;

        public BtoD(double value) : this((float)value) { }

        public BtoD(float value)
        {
            B0 = 0;
            B1 = 0;
            B2 = 0;
            B3 = 0;
            W0 = 0;
            W1 = 0;
            D = 0;
            F = value;
        }

        public BtoD(uint value)
        {
            B0 = 0;
            B1 = 0;
            B2 = 0;
            B3 = 0;
            W0 = 0;
            W1 = 0;
            F = 0;
            D = value;
        }

        public BtoD(byte[] bytes)
        {
            W0 = 0;
            W1 = 0;
            D = 0;
            F = 0;
            B0 = bytes[0];
            B1 = bytes[1];
            B2 = bytes[2];
            B3 = bytes[3];
        }

        public byte[] ToArray()
        {
            return new byte[] { B0, B1, B2, B3 };
        }
    }
}
