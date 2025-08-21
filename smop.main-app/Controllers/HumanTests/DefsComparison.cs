using System;
using System.Collections.Generic;
using System.Linq;

namespace Smop.MainApp.Controllers.HumanTests;

internal class Mixture
{
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

internal class StressControlMixture : Mixture
{
    public bool IsStress { get; }
    public bool IsControl { get; }
    //public bool IsTarget => IsStress || IsControl;
    //public bool IsMLProduced => !IsStress && !IsControl;

    public StressControlMixture(string name, KeyValuePair<string, float>[] flows, Dictionary<OdorDisplay.Device.ID, string> channels) :
        base(name, flows, channels)
    {
        IsStress = name.ToLower() == "stress";
        IsControl = name.ToLower() == "control";
    }
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

interface IComparisonSession
{
    public ComparisonBlock[] Blocks { get; }
}

internal class StressControlComparisonSession : IComparisonSession
{
    public ComparisonBlock[] Blocks { get; private set; } = [];

    public StressControlComparisonSession(Settings settings)
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
            var allMixtures = OdorDisplayHelper.GetStressControlMixtures(settings.Channels);
            var stress = allMixtures.First(mix => mix.IsStress);
            var control = allMixtures.First(mix => mix.IsControl);
            //var againstStress = allMixtures.Where(mix => !mix.IsStress);
            //var againstControl = allMixtures.Where(mix => !mix.IsControl);

            var stressSet = new BlockSet(stress, allMixtures); //againstStress);
            var controlSet = new BlockSet(control, allMixtures); //againstControl);

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

internal class DemoComparisonSession : IComparisonSession
{
    public ComparisonBlock[] Blocks { get; private set; } = [];

    public DemoComparisonSession(Settings settings)
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
            var allMixtures = OdorDisplayHelper.GetDemoMixtures(settings.Channels);
            Comparison[] pairs = [
                new Comparison(allMixtures[0], allMixtures[3]),
                new Comparison(allMixtures[1], allMixtures[4]),
                new Comparison(allMixtures[2], allMixtures[5]),
            ];
            blocks.Add(new ComparisonBlock(settings.IsRandomized, pairs));
        }

        Blocks = blocks.ToArray();
    }
}