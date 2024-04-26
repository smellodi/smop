using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Smop.MainApp.Controllers;

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
    public Dictionary<string, object> Propeties { get; } = new();


    public event PropertyChangedEventHandler? PropertyChanged;

    public OdorChannel(OdorDisplay.Device.ID id, string name, params KeyValuePair<string, object>[] props)
    {
        ID = id;
        _name = name;

        foreach (var prop in props)
        {
            Propeties.Add(prop.Key, prop.Value);
        }
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
        var basePID = (float)Propeties["pidCheckLevel"];
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
                var channel = new OdorChannel(id, p[0]) { Flow = p.Length > 1 ? float.Parse(p[1]) : 0 };
                SetProperties(channel);
                _items.Add(channel);
            }
        }
        else
        {
            foreach (var odor in odors)
            {
                var p = odor.Value.Split(SEPARATOR_VALUES);
                var channel = new OdorChannel(odor.Key, p[0]) { Flow = p.Length > 1 ? float.Parse(p[1]) : 0 };
                SetProperties(channel);
                _items.Add(channel);
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

    readonly List<OdorChannel> _items = new();

    IEnumerator IEnumerable.GetEnumerator() => new EnumOdorChannels(_items);

    private static void SetProperties(OdorChannel channel)
    {
        var channelName = channel.Name.ToLower();
        channel.Propeties.Add("maxFlow", channelName switch
        {
            _ => 50,
        });

        channel.Propeties.Add("criticalFlow", channelName switch
        {
            "ipa" => 55,
            "ethanol" => 65,
            "nbutanol" => 70,
            _ => 100,
        });

        channel.Propeties.Add($"pidCheckLevel", channelName switch  // as of carrier gas T = OdorChannel.BASE_TEMP
        {
            "ipa" => 1.730f,
            "ethanol" => 1.200f,
            "nbutanol" => 0.600f,
            _ => 0.100f,
        });
    }
}

public record class ChemicalLevel(string OdorName, float Level)
{
    public static float TestFlow => 40; // nccm
    public static float Threshold => 95; // %
}
