using System;
using System.Collections.Generic;

namespace Smop.MainApp.Utils.Extensions;

internal static class RandomExt
{
    public static Random Shuffle<T>(this Random rng, IList<T> array)
    {
        void Shuffle()
        {
            int n = array.Count;
            while (n > 1)
            {
                int k = rng.Next(n--);
                (array[k], array[n]) = (array[n], array[k]);
            }
        }

        int repetitions = rng.Next(8) + 3;  // 3..10 repetitions
        for (int i = 0; i < repetitions; i++)
        {
            Shuffle();
        }

        return rng;
    }
}

