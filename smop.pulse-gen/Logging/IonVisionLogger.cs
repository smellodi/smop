using System.Text;
using System.Text.Json;

namespace Smop.PulseGen.Logging;

public class IonVisionLogger : Logger<IonVisionLogger.Record>, ILog
{
	public class Record : RecordBase
    {
        public string Json { get; }

        public Record(IonVision.ScanResult data) : base()
		{
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

    public string Name => "dms";

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

    protected override string RecordsToText()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("[");
        stringBuilder.AppendLine(string.Join(",\n", _records));
        stringBuilder.Append(']');

        return stringBuilder.ToString();
    }
}
