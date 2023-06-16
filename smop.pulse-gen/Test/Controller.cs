using System;

namespace Smop.PulseGen.Test
{
    internal enum Stage
    {
        None,
        InitialPause,
        Pulse,
        PulseWithDMS,
        FinalPause,
    }

    internal class Controller : IDisposable
    {
        public class StageChangedEventArgs
        {
            public PulseIntervals? Intervals { get; }
            public PulseProps? Pulse { get; }
            public Stage Stage { get; }
            public StageChangedEventArgs(PulseIntervals? intervals, PulseProps? pulse, Stage stage)
            {
                Intervals = intervals;
                Pulse = pulse;
                Stage = stage;
            }
        }


        public event EventHandler<StageChangedEventArgs>? StageChanged;

        public Controller(PulseSetup setup)
        {
            _setup = setup;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void Interrupt()
        {
        }

        public void ForceToFinish()
        {
        }

        // Internal

        PulseSetup _setup;
    }
}
