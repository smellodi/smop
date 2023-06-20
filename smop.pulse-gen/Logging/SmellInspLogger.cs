using System.Collections.Generic;

namespace Smop.PulseGen.Logging;

public class SmellInspLogger : Logger<SmellInspLogger.Record>, ILog
{
	public class Record : RecordBase
	{
		public Record(SmellInsp.Data data) : base()
		{
            var values = new List<float>
            {
                Timestamp,
                data.Temperature,
                data.Humidity
            };

            foreach (var res in data.Resistances)
			{
                values.Add(res);
            }

            _fields = values.ToArray();
        }

        public static string MakeHeader(SmellInsp.Data data)
        {
            var names = new List<string>
            {
                "Timestamp",
                "Temperature",
                "Humidity"
            };
            for (int i = 0; i < data.Resistances.Length; i++)
            {
                names.Add($"Resistance{i + 1}");
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

	public static SmellInspLogger Instance => _instance ??= new();

    public string Name => "snt";

    public void Add(SmellInsp.Data data)
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

	static SmellInspLogger? _instance = null;

	protected SmellInspLogger() : base() { }
}
