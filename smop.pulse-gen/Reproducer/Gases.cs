using System;
using System.Collections.Generic;
using System.Linq;

namespace Smop.PulseGen.Reproducer;

public class Gas
{
    public string Name { get; set; }
    public OdorDisplay.Device.ID ChannelID { get; }
    public Dictionary<string, string> Propeties { get; } = new() { { "maxFlow", "80" } };

    public Gas(OdorDisplay.Device.ID channelID, string name, params KeyValuePair<string, string>[] props)
    {
        ChannelID = channelID;
        Name = name;
        foreach (var prop in props)
        {
            Propeties.Add(prop.Key, prop.Value);
        }
    }
}

public class Gases
{
    public Gas[] Items => _items.ToArray();

    public Gases(OdorDisplay.Device.ID[]? ids = null)
    {
        var odorDefs = Properties.Settings.Default.Reproduction_Odors;
        var odors = string.IsNullOrEmpty(odorDefs) ?
            new Dictionary<OdorDisplay.Device.ID, string>() :
            new Dictionary<OdorDisplay.Device.ID, string>(odorDefs.Split(SEPARATOR_GAS).Select(str =>
            {
                var p = str.Split(SEPARATOR_KV);
                return KeyValuePair.Create(Enum.Parse<OdorDisplay.Device.ID>(p[0]), p[1]);
            }));


        if (ids != null)
        {
            foreach (var id in ids)
            {
                var name = odors.ContainsKey(id) ? odors[id] : $"{id}";
                _items.Add(new Gas(id, name));
            }
        }
        else
        {
            foreach (var odor in odors)
            {
                _items.Add(new Gas(odor.Key, odor.Value));
            }
        }
    }

    public Gas? Get(OdorDisplay.Device.ID id) => Items.FirstOrDefault(gas => gas.ChannelID == id);

    public void Save()
    {
        var defs = new List<string>();
        foreach (var gas in Items)
        {
            defs.Add($"{gas.ChannelID}{SEPARATOR_KV}{gas.Name}");
        }

        var settings = Properties.Settings.Default;
        settings.Reproduction_Odors = string.Join(SEPARATOR_GAS, defs);
        settings.Save();
    }

    // Internal

    readonly char SEPARATOR_GAS = ';';
    readonly char SEPARATOR_KV = '=';

    readonly List<Gas> _items = new();
}
