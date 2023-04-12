using System;
using System.Linq;
using SMOP.Utils;

namespace SMOP
{
    public class FlowLogger : Logger<FlowLogger.Record>
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

        public static FlowLogger Instance => _instance ??= new();

        public bool IsEnabled { get; set; } = true;
        public bool HasAnyRecord => _records.Count > 0;
        public bool HasTestRecords => _records.Any(r => (int)r.Source >= (int)LogSource.__MIN_TEST_ID);

        public bool HasMeasurements(LogSource source) => _records.Any(r => r.Source == source && r.Type == "data");

        public void Add(LogSource source, string type, params string[] data)
        {
            if (IsEnabled)
            {
                var record = new Record(source, type, data);
                _records.Add(record);
                //System.Diagnostics.Debug.WriteLine(record.ToString());
            }
        }

        public void SaveMeasurements(string type, string subname, bool canCancel)
        {
            var filename = $"{NAME}{LoggerStorage.DELIM}{DateTime.Now:u}{LoggerStorage.DELIM}{subname}.{LoggerStorage.FILE_EXT}".ToPath();

            (var result, _) = PromptToSave(ref filename, canCancel);
            if (result == SavingResult.Save)
            {
                var header = string.Join('\t', OdorDisplay.DeviceSample.Header);
                var data = _records
                    .Where(record => record.Source == LogSource.PID && (string.IsNullOrEmpty(type) || record.Type == type))
                    .Select(record => string.Join('\t', record.Data));

                Save(filename, data, header, false);
            }
        }


        // Internal methods

        static FlowLogger? _instance = null;

        protected override string Header => Record.HEADER;

        protected FlowLogger() : base() { }
    }
}
