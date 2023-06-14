namespace Smop.OdorDisplay
{
    public static class EmulatorTimestamp
    {
        public static readonly double SAMPLING_INTERVAL = 0.1;

        public static double Value => _isEmulating ? _emulated : Utils.Timestamp.Sec;

        public static double Next => _emulated += SAMPLING_INTERVAL;

        public static double Start()
        {
            _isEmulating = true;
            _emulated = 0;

            return _emulated;
        }

        public static void Stop()
        {
            _isEmulating = true;
        }

        private static bool _isEmulating = false;
        private static double _emulated = 0;
    }
}
