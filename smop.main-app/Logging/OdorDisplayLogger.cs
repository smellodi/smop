using Smop.OdorDisplay.Packets;
using System.Collections.Generic;

namespace Smop.MainApp.Logging;

public class OdorDisplayLogger : Logger<OdorDisplayLogger.Record>, ILog
{
    public class Record : RecordBase
    {
        public Record(Data data) : base()
        {
            var values = new List<float>
            {
                Timestamp,
                data.Timestamp
            };

            foreach (Sensors m in data.Measurements)
            {
                foreach (var sv in m.SensorValues)
                {
                    foreach (float value in sv.Values)
                    {
                        values.Add(value);
                    }
                }
            }

            _fields = values.ToArray();
        }

        public override string ToString()
        {
            return string.Join(Delim, _fields);
        }

        // Internal

        readonly float[] _fields;
    }

    public static OdorDisplayLogger Instance => _instance ??= new();

    public string Name => "odor_display";
    public string Extension => "txt";

    public void Add(Data data)
    {
        if (!IsEnabled)
            return;

        if (_records.Count == 0)
        {
            Header = MakeHeader(data);
        }

        var record = new Record(data);
        _records.Add(record);
    }

    public void SetChannelNames(Dictionary<OdorDisplay.Device.ID, string> names)
    {
        _channelNames = names;
    }

    // Internal

    static OdorDisplayLogger? _instance = null;

    Dictionary<OdorDisplay.Device.ID, string> _channelNames = [];

    protected OdorDisplayLogger() : base() { }

    private string MakeHeader(Data data)
    {
        var names = new List<string>
            {
                "Timestamp",
                "DeviceTimestamp"
            };
        foreach (Sensors m in data.Measurements)
        {
            var channelName = _channelNames.GetValueOrDefault(m.Device, m.Device.ToString());
            if (string.IsNullOrEmpty(channelName))
                channelName = m.Device.ToString();

            foreach (var sv in m.SensorValues)
            {
                foreach (string valueName in sv.ValueNames)
                {
                    names.Add($"{channelName}_{valueName}");
                }
            }
        }

        return string.Join(RecordBase.Delim, names);
    }
}
