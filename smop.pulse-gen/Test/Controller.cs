﻿using Smop.PulseGen.Logging;
using Smop.PulseGen.Utils;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Smop.PulseGen.Test;

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
    public event EventHandler<int>? DmsScanProgressChanged;
    public event EventHandler<OdorDisplay.Packets.Data>? OdorDisplayDataArrived;

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
        if (_isDisposed)
            return;

        _isDisposed = true;

        OdorDisplay.CommPort.Instance.Data -= OdorDisplay_Data;
        SmellInsp.CommPort.Instance.Data -= SmellInsp_Data;

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
        if (_delayedActionDmsScanProgress != null)
        {
            _delayedActionDmsScanProgress.Stop();
            _delayedActionDmsScanProgress = null;
        }

        GC.SuppressFinalize(this);
    }

    public void Start()
    {
        OdorDisplay.CommPort.Instance.Data += OdorDisplay_Data;
        SmellInsp.CommPort.Instance.Data += SmellInsp_Data;

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

    const double DMS_PROGRESS_CHECK_INTERVAL = 1;

    readonly OdorDisplayController _odorDisplay = new();
    readonly IonVision.Communicator _ionVision = App.IonVision!;

    readonly PulseSetup _setup;

    readonly EventLogger _eventLogger = EventLogger.Instance;
    readonly IonVisionLogger _ionVisionLogger = IonVisionLogger.Instance;
    readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    readonly SmellInspLogger _smellInspLogger = SmellInspLogger.Instance;

    int _sessionIndex = -1;
    int _pulseIndex = -1;

    Stage _currentStage = Stage.None;
    Stage _extraStages = Stage.None;

    DispatchOnce? _delayedAction = null;
    DispatchOnce? _delayedActionDms = null;
    DispatchOnce? _delayedActionDmsScanProgress = null;

    bool _waitForScanProgress = true;
    bool _isDisposed = false;

    private void RunNextSession()
    {
        _sessionIndex += 1;

        if (_setup.Sessions.Length > _sessionIndex)
        {
            var session = _setup.Sessions[_sessionIndex];
            _eventLogger.Add("session", _sessionIndex.ToString());

            _odorDisplay.SetHumidity(session.Humidity);

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
            var pulse = session.Pulses[_pulseIndex];
            _eventLogger.Add("pulse", _pulseIndex.ToString(), string.Join(' ', PulseSetup.PulseChannelsAsStrings(pulse)));

            _odorDisplay.SetFlows(pulse.Channels);

            _extraStages |= Stage.NewPulse;

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

    private async void StartPulse()
    {
        var session = _setup.Sessions[_sessionIndex];
        var pulse = session.Pulses[_pulseIndex];

        _eventLogger.Add("pulse", _pulseIndex.ToString(), "start");

        var pulseData = PulseSetup.PulseChannelsAsStrings(pulse);
        HandleIonVisionError(await _ionVision.SetScanResultComment(new { Pulse = pulseData }), "SetScanResultComment");

        _odorDisplay.OpenChannels(pulse.Channels, session.Intervals.Pulse);

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

    private async void StartDMS()
    {
        var session = _setup.Sessions[_sessionIndex];
        var pulse = session.Pulses[_pulseIndex];

        _eventLogger.Add("dms", "start");

        HandleIonVisionError(await _ionVision.StartScan(), "StartScan");

        _waitForScanProgress = true;

        _delayedActionDmsScanProgress = DispatchOnce.Do(DMS_PROGRESS_CHECK_INTERVAL, CheckDmsScanProgress);

        PublishStage(Stage.Pulse | Stage.DMS);
    }

    private async void FinishPulse()
    {
        var session = _setup.Sessions[_sessionIndex];
        var pulse = session.Pulses[_pulseIndex];

        _eventLogger.Add("pulse", _pulseIndex.ToString(), "stop");

        //_odorDisplay.CloseChannels(pulse.Channels);

        _delayedAction = DispatchOnce.Do(session.Intervals.FinalPause, RunNextPulse);

        if (session.Intervals.FinalPause > 0)
        {
            PublishStage(Stage.FinalPause);
        }

        var scan = HandleIonVisionError(await _ionVision.GetScanResult(), "GetScanResult");
        if (scan?.Success ?? false)
        {
            _ionVisionLogger.Add(scan.Value!);
        }
    }

    private void PublishStage(Stage stage)
    {
        var session = _setup.Sessions[_sessionIndex];
        var pulse = session.Pulses[_pulseIndex];

        _currentStage = stage | _extraStages;

        StageChanged?.Invoke(this, new StageChangedEventArgs(session.Intervals, pulse, _currentStage));
        _extraStages = Stage.None;
    }

    private async void CheckDmsScanProgress()
    {
        _delayedActionDmsScanProgress = null;

        if (!_currentStage.HasFlag(Stage.Pulse))
        {
            return;
        }

        var progress = HandleIonVisionError(await _ionVision.GetScanProgress(), "GetScanProgress");
        var value = progress?.Value?.Progress ?? -1;

        if (value >= 0)
        {
            _waitForScanProgress = false;
            _delayedActionDmsScanProgress = DispatchOnce.Do(DMS_PROGRESS_CHECK_INTERVAL, CheckDmsScanProgress);
            DmsScanProgressChanged?.Invoke(this, value);
        }
        else if (_waitForScanProgress)
        {
            _delayedActionDmsScanProgress = DispatchOnce.Do(DMS_PROGRESS_CHECK_INTERVAL, CheckDmsScanProgress);
        }
        else
        {
            _eventLogger.Add("dms", "stop");
            DmsScanProgressChanged?.Invoke(this, -1);
        }
    }

    private static IonVision.API.Response<T> HandleIonVisionError<T>(IonVision.API.Response<T> response, string action)
    {
        var error = !response.Success ? response.Error : "OK";
        Debug.WriteLine($"[IV] {action}: {error}");
        return response;
    }

    private async void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
    {
        await Task.Run(() =>
        {
            _odorDisplayLogger.Add(data);
            OdorDisplayDataArrived?.Invoke(this, data);
        });
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data data)
    {
        await Task.Run(() => _smellInspLogger.Add(data));
    }
}
