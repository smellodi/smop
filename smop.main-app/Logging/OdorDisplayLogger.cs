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

        public static string MakeHeader(Data data)
        {
            var names = new List<string>
            {
                "Timestamp",
                "DeviceTimestamp"
            };
            foreach (Sensors m in data.Measurements)
            {
                foreach (var sv in m.SensorValues)
                {
                    foreach (string name in sv.ValueNames)
                    {
                        names.Add(name);
                    }
                }
            }

            return string.Join(Delim, names);
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
        if (_records.Count == 0)
        {
            Header = Record.MakeHeader(data);
        }

        if (IsEnabled)
        {
            var record = new Record(data);
            _records.Add(record);
        }
    }

    // Internal

    static OdorDisplayLogger? _instance = null;

    protected OdorDisplayLogger() : base() { }
}
