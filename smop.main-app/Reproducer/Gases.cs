using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Smop.MainApp.Reproducer;

public class Gas : INotifyPropertyChanged
{
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public float Flow
    {
        get => _flow;
        set
        {
            _flow = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Flow)));
        }
    }

    public OdorDisplay.Device.ID ChannelID { get; }
    public Dictionary<string, string> Propeties { get; } = new() { { "maxFlow", "80" } };


    public event PropertyChangedEventHandler? PropertyChanged;

    public Gas(OdorDisplay.Device.ID channelID, string name, params KeyValuePair<string, string>[] props)
    {
        ChannelID = channelID;
        _name = name;

        foreach (var prop in props)
        {
            Propeties.Add(prop.Key, prop.Value);
        }
    }

    // Internal

    string _name;
    float _flow = 0;
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
                var props = odors.ContainsKey(id) ? odors[id] : $"{id}";
                var p = props.Split(SEPARATOR_VALUES);
                _items.Add(new Gas(id, p[0]) { Flow = p.Length > 1 ? float.Parse(p[1]) : 0 });
            }
        }
        else
        {
            foreach (var odor in odors)
            {
                var p = odor.Value.Split(SEPARATOR_VALUES);
                _items.Add(new Gas(odor.Key, p[0]) { Flow = p.Length > 1 ? float.Parse(p[1]) : 0 });
            }
        }
    }

    public void Save()
    {
        var defs = new List<string>();
        foreach (var gas in Items)
        {
            defs.Add($"{gas.ChannelID}{SEPARATOR_KV}{gas.Name}{SEPARATOR_VALUES}{gas.Flow}");
        }

        var settings = Properties.Settings.Default;
        settings.Reproduction_Odors = string.Join(SEPARATOR_GAS, defs);
        settings.Save();
    }

    // Internal

    readonly char SEPARATOR_GAS = ';';
    readonly char SEPARATOR_KV = '=';
    readonly char SEPARATOR_VALUES = ',';

    readonly List<Gas> _items = new();
}
