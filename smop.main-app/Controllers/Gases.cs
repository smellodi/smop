using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Smop.MainApp.Controllers;

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
    public Dictionary<string, string> Propeties { get; } = new() { { "maxFlow", "50" } };


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

public class Gases : IEnumerable<Gas>
{
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
        foreach (var gas in _items)
        {
            defs.Add($"{gas.ChannelID}{SEPARATOR_KV}{gas.Name}{SEPARATOR_VALUES}{gas.Flow}");
        }

        var settings = Properties.Settings.Default;
        settings.Reproduction_Odors = string.Join(SEPARATOR_GAS, defs);
        settings.Save();
    }

    public string NameFromID(OdorDisplay.Device.ID id) => _items.FirstOrDefault(gas => gas.ChannelID == id)?.Name ?? id.ToString();

    public IEnumerator<Gas> GetEnumerator() => new EnumGases(_items);


    // Internal

    readonly char SEPARATOR_GAS = ';';
    readonly char SEPARATOR_KV = '=';
    readonly char SEPARATOR_VALUES = ',';

    readonly List<Gas> _items = new();

    IEnumerator IEnumerable.GetEnumerator() => new EnumGases(_items);
}

internal class EnumGases(List<Gas> gases) : IEnumerator<Gas>
{
    public Gas Current => _gases[_position];

    public bool MoveNext() => ++_position < _gases.Count;

    public void Reset() => _position = -1;

    public void Dispose() { }


    // Internal 

    readonly List<Gas> _gases = gases;

    int _position = -1;

    object IEnumerator.Current => Current;
}
