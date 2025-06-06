using System;

namespace Smop.ML.Search;

internal static class Extensions
{
    extension(Random rnd)
    {
        public double NextDouble(double min, double max) =>
            rnd.NextDouble() * (max - min) + min;

        public int[] ArrayInt32(int length, int maxValue)
        {
            int[] result = (int[])Array.CreateInstance(typeof(int), length);
            for (int i = 0; i < length; i++)
                result[i] = rnd.Next(maxValue);
            return result;
        }
    }

    extension(Array)
    {
        public static T[] RemoveValue<T>(T[] array, T value)
        {
            int index = Array.IndexOf(array, value);
            if (index >= 0)
            {
                var self = array;
                Array.Resize(ref array, array.Length - 1);

                for (int i = index; i < array.Length; i++)
                {
                    array[i] = self[i + 1];
                }
            }

            return array;
        }
    }
}
