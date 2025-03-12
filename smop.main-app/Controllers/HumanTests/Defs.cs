using ScottPlot.Drawing.Colormaps;
using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Joins;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Smop.MainApp.Controllers.HumanTests;

internal enum Stage
{
    Initial,
    WaitingMixture,
    Ready,
    SniffingMixture,
    Question,
    Finished,
}

internal static class Brushes
{
    public static Brush Foreground => System.Windows.Media.Brushes.White;
    public static Brush Inactive => System.Windows.Media.Brushes.LightGray;
    public static Brush Active => new SolidColorBrush(Color.FromRgb(0, 0xA0, 0));
    public static Brush Focused => new SolidColorBrush(Color.FromRgb(0x40, 0xB8, 0x40));
    public static Brush Selected => new SolidColorBrush(Color.FromRgb(0, 0x40, 0));
}

internal class Mixture
{
    /// <summary>
    /// Mixture preparation interval, in seconds
    /// </summary>
    public static double WaitingInterval => 9;
    /// <summary>
    /// Mixture sniffing interval, in seconds
    /// </summary>
    public static double SniffingInterval => 4;

    public string Name { get; }
    public PulseChannelProps[] Channels { get; }

    public Mixture(string name, KeyValuePair<string, float>[] flows, Dictionary<OdorDisplay.Device.ID, string> channels)
    {
        Name = name;

        var pulse = new List<PulseChannelProps>();
        foreach (var kv in flows)
        {
            if (kv.Value > 0)
            {
                var channel = channels.First(ch => ch.Value.Contains(kv.Key, StringComparison.CurrentCultureIgnoreCase));
                var pulseChannelProps = new PulseChannelProps((int)channel.Key, kv.Value, true);
                pulse.Add(pulseChannelProps);
            }
        }

        Channels = pulse.ToArray();
    }

    public override string ToString() => string.Join(",", Channels.Select(ch => $"{ch.Id}={ch.Flow}"));
}

internal class Comparison(Mixture mixture1, Mixture mixture2)
{
    public Mixture[] Mixtures { get; } = [mixture1, mixture2];
    public bool AreSame { get; set; } = false;

    public override string ToString() => $"{mixture1.Name}\t{mixture2.Name}\t{AreSame}";
}

internal class Block
{
    public Comparison[] Comparisons { get; }

    public Block(bool isRandomized, Comparison[] comparisons)
    {
        Comparisons = comparisons;

        if (isRandomized)
        {
            var r = new Random();
            r.Shuffle(Comparisons);
        }
    }
}

internal class Session
{
    public int[] UsedChannelIds { get; }
    public Block[] Blocks { get; private set; } = [];

    public Session(Settings settings)
    {
        UsedChannelIds = OdorDisplayHelper.GetChannelIds(settings.Channels);

        var blocks = new List<Block>();
        if (settings.IsPracticingProcedure)
        {
            var empty = new Mixture("empty", [], settings.Channels);
            blocks.Add(new Block(false, [
                new Comparison(empty, empty),
                new Comparison(empty, empty),
                new Comparison(empty, empty),
            ]));
        }
        else
        {
            var allMixtures = OdorDisplayHelper.GetAllMixtures(settings.Channels);
            var stress = allMixtures.First();
            var theRest = allMixtures.Skip(1);

            for (int i = 0; i < settings.Repetitions; i++)
            {
                blocks.Add(new Block(settings.IsRandomized, theRest.Select(mix => new Comparison(stress, mix)).ToArray()));
            }
        }

        Blocks = blocks.ToArray();
    }
}

internal static class OdorDisplayHelper
{
    public static int[] GetChannelIds(Dictionary<OdorDisplay.Device.ID, string> channels) => channels
        .Where(ch =>
            ch.Value.Contains(LIMONENE, StringComparison.CurrentCultureIgnoreCase) ||
            ch.Value.Contains(CYCLOHEXANONE, StringComparison.CurrentCultureIgnoreCase) ||
            ch.Value.Contains(CITRONELLYL_ACETATE, StringComparison.CurrentCultureIgnoreCase))
        .Select(ch => (int)ch.Key)
        .ToArray();

    public static Mixture[] GetAllMixtures(Dictionary<OdorDisplay.Device.ID, string> channels) => [
        new Mixture("stress", STRESS, channels),    // keep this the first
        new Mixture("control", CONTROL, channels),
        new Mixture("far", STRESS_FAR, channels),
        new Mixture("medium", STRESS_MEDIUM, channels),
        new Mixture("close", STRESS_CLOSE, channels),
        new Mixture("dissimilar", DISSIMILAR, channels),
    ];

    // Internal

    const string LIMONENE = "lim";
    const string CYCLOHEXANONE = "hex";
    const string CITRONELLYL_ACETATE = "citron";

    static KeyValuePair<string, float>[] CONTROL = [new(LIMONENE, 8.3f), new(CYCLOHEXANONE, 2.0f), new(CITRONELLYL_ACETATE, 100f)];
    static KeyValuePair<string, float>[] STRESS = [new(LIMONENE, 16.1f), new(CYCLOHEXANONE, 4.8f), new(CITRONELLYL_ACETATE, 100f)];
    static KeyValuePair<string, float>[] STRESS_CLOSE = [new(LIMONENE, 19.0f), new(CYCLOHEXANONE, 5.3f), new(CITRONELLYL_ACETATE, 100f)];
    static KeyValuePair<string, float>[] STRESS_MEDIUM = [new(LIMONENE, 11.1f), new(CYCLOHEXANONE, 4.6f), new(CITRONELLYL_ACETATE, 100f)];
    static KeyValuePair<string, float>[] STRESS_FAR = [new(LIMONENE, 21.3f), new(CYCLOHEXANONE, 3.8f), new(CITRONELLYL_ACETATE, 62f)];
    static KeyValuePair<string, float>[] DISSIMILAR = [new(LIMONENE, 5.3f), new(CYCLOHEXANONE, 2.9f), new(CITRONELLYL_ACETATE, 77f)];
}

public static class RatingWords
{
    public static string[] English => [
        "biting", "flowery", "deodorized", "subtle", "foul", "fresh",
        "damp", "individual", "cold", "musty", "natural", "neutral",
        "salty", "clean", "sour", "sweaty", "strong", "pungent",
        "smelly", "sweet", "unpleasant", "warm"
    ];
    public static string[] Finnish => [];
}