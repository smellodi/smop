using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Smop.MainApp.Controllers;


public record class OdorChannelProperties(float MinFlow, float MaxFlow,
    float CriticalFlow,
    float PidCheckLevel, 
    string ShortKnownName = "",
    string FullKnownName = "",
    string Abbreviation = "")
{
    public bool IsKnownOdor => !string.IsNullOrEmpty(FullKnownName);
    public Dictionary<string, object> ToDict()
    {
        var result = new Dictionary<string, object>();
        var props = typeof(OdorChannelProperties).GetProperties();
        foreach (var p in props)
        {
            result.Add(p.Name.ToCamelCase(), p.GetValue(this)!);
        }
        return result;
    }
}

public class KnownOdors : IEnumerable<OdorChannelProperties>
{
    public IEnumerator<OdorChannelProperties> GetEnumerator() => new EnumOdorChannels(_items);

    public string[] FullNames => _items.Select(item => item.FullKnownName).ToArray();
    public string[] ShortNames => _items.Select(item => item.ShortKnownName).ToArray();
    public string[] Abbreviations => _items.Select(item => item.Abbreviation).ToArray();

    public KnownOdors()
    {
        ReloadRequested += (s, e) =>
        {
            if (s != this)
                Load();
        };

        Load();
    }

    public KnownOdors(IEnumerable<OdorChannelProperties> coll) : this()
    {
        _items.Clear();
        _items.AddRange(coll);
    }

    public void Save()
    {
        var knownOdorsStr = JsonSerializer.Serialize(_items);
        Properties.Settings.Default.KnownOdors = knownOdorsStr;
        Properties.Settings.Default.Save();

        ReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    public OdorChannelProperties? GetProps(string name)
    {
        var channelName = name.ToLower();
        return _items.FirstOrDefault(props => channelName.StartsWith(props.ShortKnownName));
    }

    public string? GetFullName(string text) => _items.Where(item => item.FullKnownName.ToLower().StartsWith(text.ToLower())).FirstOrDefault()?.FullKnownName;

    // Internal

    static event EventHandler? ReloadRequested; // used to sync all instances

    readonly List<OdorChannelProperties> _items = new()
    {
        new OdorChannelProperties(0, 50, 55, 1.730f, "ipa", "Isopropanol", "Ipa"),
        new OdorChannelProperties(0, 50, 65, 1.200f, "eth", "Ethanol", "Eth"),
        new OdorChannelProperties(0, 50, 70, 0.600f, "nbut", "nButanol", "But"),
        new OdorChannelProperties(0, 50, 70, 1.18f, "hex", "Cyclohexanone", "Hex"),
        new OdorChannelProperties(0, 50, 120, 0.084f, "citron", "Citronellol", "Cit"),
        new OdorChannelProperties(0, 50, 75, 0.990f, "limon", "Limonene", "Lim"),
        new OdorChannelProperties(0, 50, 75, 1.0f, "caren", "3-Carene", "3Cr"),
    };

    class EnumOdorChannels(List<OdorChannelProperties> odorChannels) : IEnumerator<OdorChannelProperties>
    {
        public OdorChannelProperties Current => _odorChannels[_position];

        public bool MoveNext() => ++_position < _odorChannels.Count;

        public void Reset() => _position = -1;

        public void Dispose() { }


        // Internal 

        readonly List<OdorChannelProperties> _odorChannels = odorChannels;

        int _position = -1;

        object IEnumerator.Current => Current;
    }

    IEnumerator IEnumerable.GetEnumerator() => new EnumOdorChannels(_items);

    private void Load()
    {
        var knownOdorsStr = Properties.Settings.Default.KnownOdors;
        if (!string.IsNullOrEmpty(knownOdorsStr))
        {
            var items = JsonSerializer.Deserialize<List<OdorChannelProperties>>(knownOdorsStr);
            if (items != null)
            {
                _items.Clear();
                _items.AddRange(items);
            }
        }
    }
}
