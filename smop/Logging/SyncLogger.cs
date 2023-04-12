using System.Collections.Generic;
using System.Threading;
using SMOP.OdorDisplay;

namespace SMOP
{
    public class SyncLogger : Logger<SyncLogger.Record>
    {
        public class Record
        {
            public static string DELIM => ",";
            public static string HEADER;

            public long Time { get; }

            static Record()
            {
                HEADER = string.Join(DELIM, MessageSample.Fields) + $"{DELIM}MFC_cl{DELIM}MFC_od";
            }

            public Record(MessageSample sample, double clearnAirFlow, double odorFlow, string[] events)
            {
                Time = sample.Time;

                var smp = sample.ToStrings();
                var length = smp.Length + 3;

                _fields = new string[length];
                smp.CopyTo(_fields, 0);

                _fields[length - 3] = clearnAirFlow.ToString("F2");
                _fields[length - 2] = odorFlow.ToString("F2");
                _fields[length - 1] = string.Join(' ', events);
            }

            public override string ToString()
            {
                return string.Join(DELIM, _fields);
            }

            // Internal

            readonly string[] _fields;
        }

        public static SyncLogger Instance => _instance ??= new();

        public double ClearAirFlow { get; set; } = 0;
        public double OdorFlow { get; set; } = 0;

        public bool HasRecords => _records.Count > 0;

        /// <summary>
        /// Sets the interval and starts logging
        /// </summary>
        public void Start() { }

        public void Add(MessageSample sample)
        {
            lock (_mutex)
            {
                var record = new Record(sample, ClearAirFlow, OdorFlow, _events.ToArray());
                if (record.Time > 0)
                {
                    _records.Add(record);
                    _events.Clear();
                }
            }
        }

        public void Add(string evt)
        {
            lock (_mutex)
            {
                _events.Add(evt);
            }
        }

        public void Finilize() { }

        // Internal

        static SyncLogger? _instance = null;

        protected override string Header => Record.HEADER;

        readonly List<string> _events = new();
        readonly Mutex _mutex = new();

        protected SyncLogger() : base() { }
    }
}
