using System.Linq;
using System.Text.Json;

namespace Smop.PulseGen.Logging;

public class IonVisionLogger : Logger<IonVisionLogger.Record>
{
	public class Record
	{
		public long Timestamp { get; }
        public string Json { get; }

        public Record(IonVision.ScanResult data)
		{
			Timestamp = Utils.Timestamp.Ms;

            Json = JsonSerializer.Serialize(data, serializationOptions);
        }

        public override string ToString()
        {
            return Json;
        }

        // Internal

        readonly JsonSerializerOptions serializationOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    public static IonVisionLogger Instance => _instance ??= new();

	public void Add(IonVision.ScanResult data)
	{
		if (IsEnabled)
		{
			var record = new Record(data);
			_records.Add(record);
		}
	}

    // Internal

    static IonVisionLogger? _instance = null;

	protected IonVisionLogger() : base() { }
}
