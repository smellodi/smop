using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;

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

    public Gases(OdorDisplay.Device.ID[] ids)
    {
        var odorDefs = Properties.Settings.Default.Reproduction_Odors;
        var odors = string.IsNullOrEmpty(odorDefs) ?
            new Dictionary<OdorDisplay.Device.ID, string>() :
            new Dictionary<OdorDisplay.Device.ID, string>(odorDefs.Split(';').Select(str =>
            {
                var p = str.Split('=');
                return KeyValuePair.Create(Enum.Parse<OdorDisplay.Device.ID>(p[0]), p[1]);
            }));


        foreach (var id in ids)
        {
            var name = odors.ContainsKey(id) ? odors[id] : $"{id}";
            _items.Add(new Gas(id, name));
        }
    }

    public void Save()
    {
        var defs = new List<string>();
        foreach (var gas in Items)
        {
            defs.Add($"{gas.ChannelID}={gas.Name}");
        }

        var settings = Properties.Settings.Default;
        settings.Reproduction_Odors = string.Join(";", defs);
        settings.Save();
    }

    // Internal

    readonly List<Gas> _items = new();
}
