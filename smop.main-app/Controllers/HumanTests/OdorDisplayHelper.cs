using System;
using System.Collections.Generic;
using System.Linq;

namespace Smop.MainApp.Controllers.HumanTests;

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
