using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Smop.MainApp.Controllers.HumanTests;

public enum Stage
{
    Initial,
    WaitingMixture,
    Ready,
    SniffingMixture,
    Question,
    TimedPause,
    UserControlledPause,
    Finished,
}

public class TrialStage(Stage stage, int mixtureId)
{
    public Stage Stage { get; set; } = stage;
    public int MixtureId { get; set; } = mixtureId;
}

internal static class Brushes
{
    public static Brush Inactive { get; } = (Brush)Application.Current.FindResource("BrushHTButtonInactive");
    public static Brush Active { get; } = (Brush)Application.Current.FindResource("BrushHTButtonActive");
    public static Brush Done { get; } = (Brush)Application.Current.FindResource("BrushHTButtonDone");
    public static Brush Clickable { get; } = (Brush)Application.Current.FindResource("BrushHTButtonClickable");
}

internal class Mixture
{
    public string Name { get; }
    public PulseChannelProps[] Channels { get; }
    public bool IsStress { get; }
    public bool IsControl { get; }
    public bool IsTarget => IsStress || IsControl;
    public bool IsMLProduced => !IsStress && !IsControl;

    public Mixture(string name, KeyValuePair<string, float>[] flows, Dictionary<OdorDisplay.Device.ID, string> channels)
    {
        Name = name;
        IsStress = name.ToLower() == "stress";
        IsControl = name.ToLower() == "control";

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
    public string Answer => AreSame ? "same" : "different";

    public override string ToString() => $"{mixture1.Name}\t{mixture2.Name}\t{Answer}";
}

internal class ComparisonBlock
{
    public Comparison[] Comparisons { get; }

    public ComparisonBlock(bool isRandomized, Comparison[] comparisons)
    {
        Comparisons = comparisons;

        if (isRandomized)
        {
            var r = new Random();
            r.Shuffle(Comparisons);
        }
    }
}

internal class ComparisonSession
{
    public ComparisonBlock[] Blocks { get; private set; } = [];

    public ComparisonSession(Settings settings)
    {
        var blocks = new List<ComparisonBlock>();
        if (settings.IsPracticingProcedure)
        {
            var empty = new Mixture("empty", [], settings.Channels);
            var comparisons = new List<Comparison>();

            for (int i = 0; i < settings.PracticingTrialCount; i++)
                comparisons.Add(new Comparison(empty, empty));

            blocks.Add(new ComparisonBlock(false, comparisons.ToArray()));
        }
        else
        {
            var allMixtures = OdorDisplayHelper.GetAllMixtures(settings.Channels);
            var stress = allMixtures.First(mix => mix.IsStress);
            var control = allMixtures.First(mix => mix.IsControl);
            var againstStress = allMixtures.Where(mix => !mix.IsStress);
            var againstControl = allMixtures.Where(mix => !mix.IsControl);

            var stressSet = new BlockSet(stress, againstStress);
            var controlSet = new BlockSet(control, againstControl);

            BlockSet[] sets = settings.ParticipantID % 2 == 0
                ? [stressSet, controlSet]
                : [controlSet, stressSet];

            for (int i = 0; i < settings.ComparisonBlockCount; i++)
                foreach (var set in sets)
                    blocks.Add(new ComparisonBlock(settings.IsRandomized, set.Other.Select(mix => new Comparison(set.Target, mix)).ToArray()));
        }

        Blocks = blocks.ToArray();
    }

    // Internal

    record class BlockSet(Mixture Target, IEnumerable<Mixture> Other);
}

internal class Triplet(Mixture mixture1, Mixture mixture2, Mixture mixture3)
{
    public Mixture[] Mixtures { get; } = [mixture1, mixture2, mixture3];
    public int Answer { get; set; } = 0;
    public int OneOutID =>
        mixture1.Name == mixture2.Name ? 3 :
        mixture1.Name == mixture3.Name ? 2 : 1;
    public bool IsCorrect => Answer == OneOutID;

    public override string ToString() => $"{mixture1.Name}\t{mixture2.Name}\t{mixture3.Name}\t{OneOutID}\t{Answer}\t{IsCorrect}";
}

internal class OneOutSession
{
    public Triplet[] Triplets { get; }

    public OneOutSession(Settings settings)
    {
        var r = new Random();

        var triplets = new List<Triplet>();
        if (settings.IsPracticingProcedure)
        {
            var empty = new Mixture("empty", [], settings.Channels);

            for (int i = 0; i < settings.PracticingTrialCount; i++)
                triplets.Add(new Triplet(empty, empty, empty));
        }
        else
        {
            var allMixtures = OdorDisplayHelper.GetAllMixtures(settings.Channels);

            for (int i = 0; i < allMixtures.Length; i++)
            {
                var mixSame = allMixtures[i];
                for (int j = 0; j < allMixtures.Length; j++)
                {
                    if (i != j)
                    {
                        var mixDiff = allMixtures[j];
                        Mixture[] arr = [mixSame, mixSame, mixDiff];
                        if (settings.IsRandomized)
                        {
                            r.Shuffle(arr);
                        }
                        triplets.Add(new Triplet(arr[0], arr[1], arr[2]));
                    }
                }
            }
        }

        Triplets = triplets.ToArray();

        if (settings.IsRandomized)
        {
            r.Shuffle(Triplets);
        }
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
