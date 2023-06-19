namespace Smop.PulseGen.Logging;

public class EventLogger : Logger<EventLogger.Record>, ILog
{
	public class Record : RecordBase
	{
		public static string HEADER => $"ts{DELIM}type{DELIM}data";

		public string Type { get; }
		public string[] Data { get; }

		public Record(string type, string[] data) : base()
		{
			Type = type;
			Data = data;
		}

		public override string ToString()
		{
			var result = $"{Timestamp}{DELIM}{Type}";
			if (Data != null && Data.Length > 0)
			{
				result += DELIM + string.Join(DELIM, Data);
			}

			return result;
		}
	}

	public static EventLogger Instance => _instance ??= new();

    public string Name => "events";

    public void Add(string type, params string[] data)
	{
		if (IsEnabled)
		{
			var record = new Record(type, data);
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
