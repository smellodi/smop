﻿namespace Smop.MainApp.Logging;

public class EventLogger : Logger<EventLogger.Record>, ILog
{
    public class Record(string type, string[] data) : RecordBase()
    {
        public static string Header => $"ts{Delim}type{Delim}data";

        public string Type { get; } = type;
        public string[] Data { get; } = data;

        public override string ToString()
        {
            var result = $"{Timestamp}{Delim}{Type}";
            if (Data != null && Data.Length > 0)
            {
                result += Delim + string.Join(Delim, Data);
            }

            return result;
        }
    }

    public static EventLogger Instance => _instance ??= new();

    public string Name => "events";
    public string Extension => "txt";

    public void Add(string type, params string[] data)
    {
        if (IsEnabled)
        {
            var record = new Record(type, data);
            _records.Add(record);
            _nlog.Info(Utils.Timestamp.Ms.ToString(), record);
        }
    }


    // Internal

    static EventLogger? _instance = null;
    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(EventLogger));

    protected EventLogger() : base()
    {
        Header = Record.Header;
    }
}
