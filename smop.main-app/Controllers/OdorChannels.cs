using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace Smop.MainApp.Controllers;

public record class OdorChannelProperties(float MaxFlow, float CriticalFlow, float PidCheckLevel, string ShortKnownName = "", string FullKnownName = "")
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

    public KnownOdors()
    {
        ReloadRequested += (s, e) =>
        {
            if (s != this)
                Load();
        };

        Load();
    }

    public void Save()
    {
        var knownOdorsStr = JsonSerializer.Serialize(_items);
        Properties.Settings.Default.KnownOdors = knownOdorsStr;
        Properties.Settings.Default.Save();

        ReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    public OdorChannelProperties GetProps(string name)
    {
        var channelName = name.ToLower();
        var knownOdorProps = _items.FirstOrDefault(props => channelName.StartsWith(props.ShortKnownName));
        return knownOdorProps == null ? _default : knownOdorProps;
    }

    public string? GetFullName(string text) => _items.Where(item => item.FullKnownName.ToLower().StartsWith(text.ToLower())).FirstOrDefault()?.FullKnownName;

    // Internal

    static event EventHandler? ReloadRequested; // used to sync all instances

    readonly OdorChannelProperties _default = new OdorChannelProperties(50, 100, 0.100f);
    readonly List<OdorChannelProperties> _items = new()
    {
        new OdorChannelProperties(50, 55, 1.730f, "ipa", "Isopropanol"),
        new OdorChannelProperties(50, 65, 1.200f, "eth", "Ethanol"),
        new OdorChannelProperties(50, 70, 0.600f, "nbut", "nButanol"),
        new OdorChannelProperties(50, 70, 1.18f, "cyclohex", "Cyclohexanone"),
        new OdorChannelProperties(50, 70, 1.18f, "hex", "Cyclohexanone"),
        new OdorChannelProperties(50, 60, 0.084f, "citron", "Citronellol"),
        new OdorChannelProperties(50, 70, 0.990f, "limon", "Limonene"),
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

public class OdorChannel : INotifyPropertyChanged
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

    public OdorDisplay.Device.ID ID { get; }
    public OdorChannelProperties Properties { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public OdorChannel(OdorDisplay.Device.ID id, string name, OdorChannelProperties props)
    {
        ID = id;
        _name = name;
        Properties = props;
    }

    /// <summary>
    /// Computes the PID level as a percentage to the expected level given the reference flow <see cref="ChemicalLevel.TestFlow"/>.
    /// The reference PID level is hardcoded in its "pidCheckLevel" property.
    /// The correction formula was found empirically and may be wrong!
    /// </summary>
    /// <param name="pid">PID, Volts</param>
    /// <returns>Percentage</returns>
    public float ComputePidLevel(float pid, float temp)
    {
        var basePID = Properties.PidCheckLevel;
        var dt = temp - BASE_TEMP;
        var correction = dt > 0 ? 
            CORRECTION_GAIN * Math.Pow(dt, CORRECTION_POW) : 
            -CORRECTION_GAIN * Math.Pow(-dt, CORRECTION_POW);
        return 100 * pid / (basePID + (float)correction);
    }

    // Internal

    // Constants for ComputePidLevel, found empirically.. may be incorrect!
    const double BASE_TEMP = 20.5;
    const double CORRECTION_POW = 2.3;
    const double CORRECTION_GAIN = 0.014;

    string _name;
    float _flow = 0;
}

public class OdorChannels : IEnumerable<OdorChannel>
{
    //public static string[] KnownOdorNames => _channelProps.Keys.ToArray();

    public OdorChannels(OdorDisplay.Device.ID[]? ids = null)
    {
        var odorDefs = Properties.Settings.Default.Reproduction_Odors;
        var odors = string.IsNullOrEmpty(odorDefs) ?
            new Dictionary<OdorDisplay.Device.ID, string>() :
            new Dictionary<OdorDisplay.Device.ID, string>(odorDefs.Split(SEPARATOR_CHANNEL).Select(str =>
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
                AddChannel(id, p[0], p.Length > 1 ? float.Parse(p[1]) : 0);
            }
        }
        else
        {
            foreach (var odor in odors)
            {
                var p = odor.Value.Split(SEPARATOR_VALUES);
                AddChannel(odor.Key, p[0], p.Length > 1 ? float.Parse(p[1]) : 0);
            }
        }
    }

    public void Save()
    {
        var defs = new List<string>();
        foreach (var odorChannel in _items)
        {
            defs.Add($"{odorChannel.ID}{SEPARATOR_KV}{odorChannel.Name}{SEPARATOR_VALUES}{odorChannel.Flow}");
        }

        var settings = Properties.Settings.Default;
        settings.Reproduction_Odors = string.Join(SEPARATOR_CHANNEL, defs);
        settings.Save();
    }

    public string ToDmsComment()
    {
        IEnumerable<string> result = _items
                .Where(odorChannel => !string.IsNullOrEmpty(odorChannel.Name))
                .Select(odorChannel => string.Format(DMS_COMMENT, odorChannel.Name, odorChannel.Flow));
        return string.Join(",", result);
    }

    public string ToDmsComment(ML.Recipe recipe)
    {
        IEnumerable<string> result = recipe.Channels?.Select(ch => string.Format(DMS_COMMENT, NameFromID((OdorDisplay.Device.ID)ch.Id), ch.Flow)) ?? Array.Empty<string>();
        return string.Join(",", result);
    }

    public static OdorChannels From(IEnumerable<OdorChannel> channels) => new(channels.Select(ch => ch.ID).ToArray());

    public string NameFromID(OdorDisplay.Device.ID id) => _items.FirstOrDefault(odorChannel => odorChannel.ID == id)?.Name ?? id.ToString();

    public IEnumerator<OdorChannel> GetEnumerator() => new EnumOdorChannels(_items);


    // Internal

    class EnumOdorChannels(List<OdorChannel> odorChannels) : IEnumerator<OdorChannel>
    {
        public OdorChannel Current => _odorChannels[_position];

        public bool MoveNext() => ++_position < _odorChannels.Count;

        public void Reset() => _position = -1;

        public void Dispose() { }


        // Internal 

        readonly List<OdorChannel> _odorChannels = odorChannels;

        int _position = -1;

        object IEnumerator.Current => Current;
    }

    readonly char SEPARATOR_CHANNEL = ';';
    readonly char SEPARATOR_KV = '=';
    readonly char SEPARATOR_VALUES = ',';

    readonly string DMS_COMMENT = "{0}={1}";

    readonly List<OdorChannel> _items = new();

    KnownOdors _knownOdors = new();

    IEnumerator IEnumerable.GetEnumerator() => new EnumOdorChannels(_items);

    private void AddChannel(OdorDisplay.Device.ID id, string name, float flow)
    {
        var props = _knownOdors.GetProps(name);
        var channel = new OdorChannel(id, name, props) { Flow = flow };
        _items.Add(channel);
    }
}

public record class ChemicalLevel(string OdorName, float Level)
{
    public static float TestFlow => 40; // nccm
    public static float Threshold => 95; // %
}
