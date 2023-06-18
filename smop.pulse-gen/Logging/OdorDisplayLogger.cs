using Smop.OdorDisplay.Packets;
using System.Collections.Generic;

namespace Smop.PulseGen.Logging;

public class OdorDisplayLogger : Logger<OdorDisplayLogger.Record>
{
	public class Record
	{
		public static string DELIM => ",";

		public long Timestamp { get; }

		public Record(Data data)
		{
			Timestamp = Utils.Timestamp.Ms;

            var values = new List<float>
            {
                data.Timestamp
            };

            foreach (Measurement m in data.Measurements)
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
                "Timestamp"
            };
            foreach (Measurement m in data.Measurements)
            {
                foreach (var sv in m.SensorValues)
                {
                    foreach (string name in sv.ValueNames)
                    {
                        names.Add(name);
                    }
                }
            }

            return string.Join(DELIM, names);
        }
        
        public override string ToString()
		{
			return string.Join(DELIM, _fields);
		}

		// Internal

		readonly float[] _fields;
	}

	public static OdorDisplayLogger Instance => _instance ??= new();

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
