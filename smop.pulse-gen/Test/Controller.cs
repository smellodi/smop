using Smop.PulseGen.Pages;
using Smop.PulseGen.Utils;
using System;

namespace Smop.PulseGen.Test
{
    [Flags]
    internal enum Stage
    {
        None = 0x00,
        InitialPause = 0x01,
        Pulse = 0x02,
        DMS = 0x04,
        FinalPause = 0x08,
        NewPulse = 0x10,
        NewSession = 0x20,
        Finished = 0x80,
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

        public int SessionId => _sessionIndex + 1;
        public int PulseId => _pulseIndex + 1;
        public int SessionCount => _setup.Sessions.Length;
        public int PulseCount => 0 <= _sessionIndex && _sessionIndex < _setup.Sessions.Length ? _setup.Sessions[_sessionIndex].Pulses.Length : 0;

        public Controller(PulseSetup setup)
        {
            _setup = setup;
        }

        public void Dispose()
        {
            if (_delayedAction != null)
            {
                _delayedAction.Stop();
                _delayedAction = null;
            }
            if (_delayedActionDms != null)
            {
                _delayedActionDms.Stop();
                _delayedActionDms = null;
            }
            GC.SuppressFinalize(this);
        }

        public void Start()
        {
            RunNextSession();
        }

        public void ForceToFinish()
        {
            if (_setup.Sessions.Length > 0)
            {
                _sessionIndex = _setup.Sessions.Length - 1;
                var session = _setup.Sessions[_sessionIndex];
                if (session.Pulses.Length > 0)
                {
                    _pulseIndex = session.Pulses.Length - 1;
                }
            }
        }

        // Internal

        PulseSetup _setup;
        int _sessionIndex = -1;
        int _pulseIndex = -1;
        Stage _extraStages = Stage.None;

        DispatchOnce? _delayedAction = null;
        DispatchOnce? _delayedActionDms = null;

        private void RunNextSession()
        {
            _sessionIndex += 1;

            if (_setup.Sessions.Length > _sessionIndex)
            {
                _extraStages |= Stage.NewSession;

                _pulseIndex = -1;
                _delayedAction = DispatchOnce.Do(0.1, RunNextPulse);
            }
            else
            {
                StageChanged?.Invoke(this, new StageChangedEventArgs(null, null, Stage.Finished));
            }
        }

        private void RunNextPulse()
        {
            var session = _setup.Sessions[_sessionIndex];

            _pulseIndex += 1;

            if (session.Pulses.Length > _pulseIndex)
            {
                _extraStages |= Stage.NewPulse;

                var pulse = session.Pulses[_pulseIndex];
                _delayedAction = DispatchOnce.Do(session.Intervals.InitialPause, StartPulse);

                if (session.Intervals.InitialPause > 0)
                {
                    PublishStage(Stage.InitialPause);
                }
            }
            else
            {
                _delayedAction = DispatchOnce.Do(0.1, RunNextSession);
            }
        }

        private void StartPulse()
        {
            var session = _setup.Sessions[_sessionIndex];

            if (session.Intervals.DmsDelay >= 0)
            {
                _delayedActionDms = DispatchOnce.Do(session.Intervals.DmsDelay, StartDMS);
            }
            
            if (session.Intervals.DmsDelay != 0)
            {
                PublishStage(Stage.Pulse);
            }

            _delayedAction = DispatchOnce.Do(session.Intervals.Pulse, FinishPulse);
        }

        private void StartDMS()
        {
            PublishStage(Stage.Pulse | Stage.DMS);
        }

        private void FinishPulse()
        {
            var session = _setup.Sessions[_sessionIndex];
            _delayedAction = DispatchOnce.Do(session.Intervals.FinalPause, RunNextPulse);

            if (session.Intervals.FinalPause > 0)
            {
                PublishStage(Stage.FinalPause);
            }
        }

        private void PublishStage(Stage stage)
        {
            var session = _setup.Sessions[_sessionIndex];
            var pulse = session.Pulses[_pulseIndex];

            StageChanged?.Invoke(this, new StageChangedEventArgs(session.Intervals, pulse, stage | _extraStages));
            _extraStages = Stage.None;
        }
    }
}
