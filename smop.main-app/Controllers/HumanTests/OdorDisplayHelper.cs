using System;
using System.Collections.Generic;
using System.Linq;

namespace Smop.MainApp.Controllers.HumanTests;

internal static class OdorDisplayHelper
{
    public static int[] GetChannelIds(Dictionary<OdorDisplay.Device.ID, string> channels, HumanTestsMode mode)
    {
        var odorShortNames = new MixtureComponents().GetPairs(mode).Select(kv => kv.Key);
        return channels
            .Where(ch => odorShortNames.Any(shortName => ch.Value.Contains(shortName, StringComparison.CurrentCultureIgnoreCase)))
            .Select(ch => (int)ch.Key)
            .ToArray();
    }

    public static StressControlMixture[] GetStressControlMixtures(Dictionary<OdorDisplay.Device.ID, string> channels)
    {
        var settings = new Settings();
        return settings.MixtureComponents
            .Select(mixComp => new StressControlMixture(mixComp.Name, mixComp.GetPairs(Settings.Mode), channels))
            .ToArray();
    }

    public static Mixture[] GetDemoMixtures(Dictionary<OdorDisplay.Device.ID, string> channels, bool includeOriginal, bool includeRecreated)
    {
        var settings = new Settings();

        var mc = settings.MixtureComponents;
        if (!includeOriginal)
            mc = mc.Where(mixComp => !mixComp.Name.Contains("OM")).ToArray();
        if (!includeRecreated)
            mc = mc.Where(mixComp => !mixComp.Name.Contains("RM")).ToArray();

        return mc
            .Select(mixComp => new Mixture(mixComp.Name, mixComp.GetPairs(Settings.Mode), channels))
            .ToArray();
    }

    public static string[] GetOdorAbbreviations(string[] odorShortNames)
    {
        var result = new List<string>();

        var knownOdors = new KnownOdors();
        foreach (var odorShortName in odorShortNames)
        {
            var knownOdor = knownOdors.FirstOrDefault(o => o.FullKnownName.Contains(odorShortName, StringComparison.CurrentCultureIgnoreCase));
            if (knownOdor != null)
            {
                result.Add(knownOdor.Abbreviation);
            }
        }

        return result.ToArray();
    }
}
