namespace Smop.PulseGen.Logging;

public class EventLogger : Logger<EventLogger.Record>
{
	public class Record
	{
		public static string DELIM => "\t";
		public static string HEADER => $"ts{DELIM}source{DELIM}type{DELIM}data";

		public long Timestamp { get; private set; }
		public LogSource Source { get; private set; }
		public string Type { get; private set; }
		public string[] Data { get; private set; }

		public Record(LogSource source, string type, string[] data)
		{
			Timestamp = Utils.Timestamp.Ms;
			Source = source;
			Type = type;
			Data = data;
		}

		public override string ToString()
		{
			var result = $"{Timestamp}{DELIM}{Source}{DELIM}{Type}";
			if (Data != null && Data.Length > 0)
			{
				result += DELIM + string.Join(DELIM, Data);
			}

			return result;
		}
	}

	public static EventLogger Instance => _instance ??= new();

	public void Add(LogSource source, string type, params string[] data)
	{
		if (IsEnabled)
		{
			var record = new Record(source, type, data);
			_records.Add(record);
		}
	}


	// Internal

	static EventLogger? _instance = null;

	protected EventLogger() : base()
	{
		Header = Record.HEADER;
    }
}
