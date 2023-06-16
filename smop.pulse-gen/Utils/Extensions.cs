using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text.RegularExpressions;

namespace Smop.PulseGen.Utils;

internal static class RandomExtensions
{
	public static Random Shuffle<T>(this Random rng, T[] array)
	{
		void Shuffle()
		{
			int n = array.Length;
			while (n > 1)
			{
				int k = rng.Next(n--);
				T temp = array[n];
				array[n] = array[k];
				array[k] = temp;
			}
		}

		int repetitions = rng.Next(8) + 3;  // 3..10 repetitions
		for (int i = 0; i < repetitions; i++)
		{
			Shuffle();
		}

		return rng;
	}

	public static Random Shuffle<T>(this Random rng, List<T> array)
	{
		void Shuffle()
		{
			int n = array.Count;
			while (n > 1)
			{
				int k = rng.Next(n--);
				T temp = array[n];
				array[n] = array[k];
				array[k] = temp;
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

internal static class StringExtensions
{
	public static string ToPath(this string s, string replacement = "-")
	{
		var invalidChars = System.IO.Path.GetInvalidFileNameChars();
		string[] temp = s.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries);
		return string.Join(replacement, temp);
	}

	/*public static string SeparateWords(this string s)
	{
		var r = new Regex(@"[A-Z][a-z]*");
		var words = r.Matches(s).Select(m => m.Value.ToLower());
		return string.Join(' ', words);
	}*/
}
/*
internal enum RangeRelation
{
	Less,
	Inside,
	Greater
}

internal static class NumberExtensions
{
	public static RangeRelation IsInRange(this double number, double min, double max)
	{
		if (number < min) return RangeRelation.Less;
		if (number > max) return RangeRelation.Greater;
		return RangeRelation.Inside;
	}
}
*/