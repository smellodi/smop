﻿using System.Collections.Generic;

namespace Smop.PulseGen.Logging;

public class SmellInspLogger : Logger<SmellInspLogger.Record>
{
	public class Record
	{
		public static string DELIM => ",";

		public long Timestamp { get; }

		public Record(SmellInsp.Data data)
		{
			Timestamp = Utils.Timestamp.Ms;

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

            return string.Join(DELIM, names);
        }
        
        public override string ToString()
		{
			return string.Join(DELIM, _fields);
		}

		// Internal

		readonly float[] _fields;
	}

	public static SmellInspLogger Instance => _instance ??= new();

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
