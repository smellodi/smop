using System;

namespace SMOP.Utils
{
    public static class MathExt
    {
        public static sbyte Limit(sbyte value, long min, long max) => (sbyte)Math.Max(min, Math.Min(max, value));
        public static byte Limit(byte value, long min, long max) => (byte)Math.Max(min, Math.Min(max, value));
        public static short Limit(short value, long min, long max) => (short)Math.Max(min, Math.Min(max, value));
        public static ushort Limit(ushort value, ulong min, ulong max) => (ushort)Math.Max(min, Math.Min(max, value));
        public static int Limit(int value, long min, long max) => (int)Math.Max(min, Math.Min(max, value));
        public static uint Limit(uint value, ulong min, ulong max) => (uint)Math.Max(min, Math.Min(max, value));
        public static long Limit(long value, long min, long max) => Math.Max(min, Math.Min(max, value));
        public static ulong Limit(ulong value, ulong min, ulong max) => Math.Max(min, Math.Min(max, value));
        public static nint Limit(nint value, decimal min, decimal max) => (nint)Math.Max(min, Math.Min(max, value));
        public static nuint Limit(nuint value, decimal min, decimal max) => (nuint)Math.Max(min, Math.Min(max, value));
        public static float Limit(float value, double min, double max) => (float)Math.Max(min, Math.Min(max, value));
        public static double Limit(double value, double min, double max) => (double)Math.Max(min, Math.Min(max, value));
        public static decimal Limit(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));
    }
}
