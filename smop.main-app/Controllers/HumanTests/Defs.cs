using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
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

public enum Language
{
    Finnish,
    English,
    German,
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
    public string Name { get; }
    public PulseChannelProps[] Channels { get; }
    public bool IsStress { get; }

    public Mixture(string name, KeyValuePair<string, float>[] flows, Dictionary<OdorDisplay.Device.ID, string> channels)
    {
        Name = name;
        IsStress = name.ToLower() == "stress";

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
            var comparisons = new List<Comparison>();

            for (int i = 0; i < settings.PracticingTrialCount; i++)
                comparisons.Add(new Comparison(empty, empty));

            blocks.Add(new Block(false, comparisons.ToArray()));
        }
        else
        {
            var allMixtures = OdorDisplayHelper.GetAllMixtures(settings.Channels);
            var stress = allMixtures.First(mix => mix.IsStress);
            var theRest = allMixtures.Where(mix => !mix.IsStress);

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
            ch.Value.Contains(CYCLOHEX, StringComparison.CurrentCultureIgnoreCase) ||
            ch.Value.Contains(CITRONEL, StringComparison.CurrentCultureIgnoreCase))
        .Select(ch => (int)ch.Key)
        .ToArray();

    public static Mixture[] GetAllMixtures(Dictionary<OdorDisplay.Device.ID, string> channels)
    {
        static KeyValuePair<string, float>[] ToKeyValue(MixtureComponents comp) => 
        [
            new(LIMONENE, comp.Limonene),
            new(CYCLOHEX, comp.Cyclohexanone),
            new(CITRONEL, comp.CitronellylAcetate),
        ];

        var settings = new Settings();

        return settings.Mixtures.Select(mix => new Mixture(mix.Name, ToKeyValue(mix), channels)).ToArray();
    }

    // Internal

    const string LIMONENE = "lim";
    const string CYCLOHEX = "hex";
    const string CITRONEL = "citron";
}

public static class RatingWords
{
    public static string[] Get(Language lang) =>
        (string[]?)typeof(RatingWords).GetProperty(lang.ToString())?.GetValue(null) ??
        throw new NotImplementedException($"Language {lang} is not supported yet");

    public static string[] Finnish => [
        "hikinen", "pistävä", "neutraali",
        "tunkkainen", "raikas", "voimakas",
        "puhdas", "epämiellyttävä", "mieto",
        "paha", "ummehtunut", "likainen",
        "makea", "hapan", "suolainen",
        "deodoranttinen", "miellyttävä", "imelä",
        "mätä", "ruokainen", "ominaistuoksuinen",
        "kostea"
    ];
    public static string[] English => [
        "biting", "flowery", "deodorized",
        "subtle", "foul", "fresh",
        "damp", "individual", "cold",
        "musty", "natural", "neutral",
        "salty", "clean", "sour",
        "sweaty", "strong", "pungent",
        "smelly", "sweet", "unpleasant",
        "warm"
    ];
    public static string[] German => [
        "schweißig", "sauer", "NEG-angenehm",
        "neutral", "intensiv", "stinkend",
        "käsig", "süß", "frisch",
        "angenehm", "muffig", "faulig",
        "stechend", "beißend", "salzig",
        "stark", "fischig", "streng",
        "warm", "feucht", "eklig",
        "herb"
    ];
}