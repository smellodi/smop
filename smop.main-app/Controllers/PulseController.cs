using Smop.Common;
using Smop.MainApp.Dialogs;
using Smop.MainApp.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smop.MainApp.Controllers;

[Flags]
internal enum PulseStage
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

internal class PulseController(PulseSetup setup, IonVision.Communicator? ionVision) : IDisposable
{
    public class StageChangedEventArgs(PulseIntervals? intervals, PulseProps? pulse, PulseStage stage, int sessionID, int pulseID) : EventArgs
    {
        public PulseIntervals? Intervals { get; } = intervals;
        public PulseProps? Pulse { get; } = pulse;
        public PulseStage Stage { get; } = stage;
        public int SessionID { get; } = sessionID;
        public int PulseID { get; } = pulseID;
    }

    public event EventHandler<StageChangedEventArgs>? StageChanged;
    public event EventHandler<int>? DmsScanProgressChanged;
    public event EventHandler<OdorDisplay.Packets.Data>? OdorDisplayDataArrived;

    public int SessionId => _sessionIndex + 1;
    public int PulseId => _pulseIndex + 1;
    public int SessionCount => _setup.Sessions.Length;
    public int PulseCount => 0 <= _sessionIndex && _sessionIndex < _setup.Sessions.Length ? _setup.Sessions[_sessionIndex].Pulses.Count : 0;

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
            _delayedAction.Dispose();
            _delayedAction = null;
        }
        if (_delayedActionDms != null)
        {
            _delayedActionDms.Stop();
            _delayedActionDms.Dispose();
            _delayedActionDms = null;
        }
        if (_delayedActionDmsScanProgress != null)
        {
            _delayedActionDmsScanProgress.Stop();
            _delayedActionDmsScanProgress.Dispose();
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
            if (session.Pulses.Count > 0)
            {
                _pulseIndex = session.Pulses.Count - 1;
            }
        }
    }

    public void Stop()
    {
        var channelIDs = new List<OdorDisplay.Device.ID>();
        for (int i = 1; i <= PulseChannels.MaxCount; i++)
            channelIDs.Add((OdorDisplay.Device.ID)i);

        _odorDisplay.StopFlows(channelIDs.ToArray());
    }

    public async Task SaveDmsToServer()
    {
        if (_dmses.Count == 0)
            return;

        var dmsSaveDialog = new Dialogs.DmsSaveDialog(_dmses);
        if (dmsSaveDialog.ShowDialog() == false)
            return;

        var dmsDataList = dmsSaveDialog.Items;

        try
        {
            var gdrive = GoogleDriveService.Instance;

            bool isSuccess = true;
            foreach (var dmsData in dmsDataList)
            {
                if (dmsData.IsEnabled)
                {
                    var file = await gdrive.Create($"{dmsData.Name}.json", System.Text.Json.JsonSerializer.Serialize(dmsData.Data));
                    if (string.IsNullOrEmpty(file?.Id))
                    {
                        isSuccess = false;
                        MsgBox.Error(App.Name, "Cannot save DMS data to Google Drive.");
                        break;
                    }
                }
            }

            if (isSuccess)
            {
                MsgBox.Notify(App.Name, "DMS data saved to Google Drive.");
            }
        }
        catch (ApplicationException ex)
        {
            _nlog.Error(ex, "Failed to access Google Drive service.");
            MsgBox.Error(App.Name, "Failed to access Google Drive service. Please check your credentials and try again.");
        }
        catch (Exception ex)
        {
            _nlog.Error(ex, $"Unexpected error while accessing Google Drive service: {ex.Message}");
            MsgBox.Error(App.Name, $"Unexpected error while accessing Google Drive service: {ex.Message}");
        }
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(PulseController));

    const double DMS_PROGRESS_CHECK_INTERVAL = 1;

    readonly OdorDisplayController _odorDisplay = new();
    readonly IonVision.Communicator? _ionVision = ionVision;
    readonly List<IonVision.Defs.ScanResult> _dmses = [];

    readonly PulseSetup _setup = setup;

    readonly EventLogger _eventLogger = EventLogger.Instance;
    readonly IonVisionLogger _ionVisionLogger = IonVisionLogger.Instance;
    readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    readonly SmellInspLogger _smellInspLogger = SmellInspLogger.Instance;

    int _sessionIndex = -1;
    int _pulseIndex = -1;

    PulseStage _currentStage = PulseStage.None;
    PulseStage _extraStages = PulseStage.None;

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

            if (session.Humidity >= 0)
            {
                HumidityController.Instance.TargetHumidity = session.Humidity;
                _odorDisplay.SetHumidity(session.Humidity);
            }

            _extraStages |= PulseStage.NewSession;

            _pulseIndex = -1;
            _delayedAction = DispatchOnce.Do(0.1, RunNextPulse);
        }
        else
        {
            StageChanged?.Invoke(this, new StageChangedEventArgs(null, null, PulseStage.Finished, 0, 0));
        }
    }

    private void RunNextPulse()
    {
        var session = _setup.Sessions[_sessionIndex];

        _pulseIndex += 1;

        if (session.Pulses.Count > _pulseIndex)
        {
            var pulse = session.Pulses[_pulseIndex];
            _eventLogger.Add("pulse", _pulseIndex.ToString(), string.Join(' ', PulseSetup.PulseChannelsAsStrings(pulse)));

            _odorDisplay.SetFlows(pulse.Channels);

            _extraStages |= PulseStage.NewPulse;

            _delayedAction = DispatchOnce.Do(session.Intervals.InitialPause, StartPulse);

            if (session.Intervals.InitialPause > 0)
            {
                PublishStage(PulseStage.InitialPause);
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

        if (_ionVision != null)
        {
            HandleIonVisionError(await _ionVision.SetScanResultComment(new { Pulses = pulseData }), "SetScanResultComment");
        }

        if (session.Intervals.InitialPause == 0 && session.Intervals.FinalPause == 0)
        {
            if (_pulseIndex == 0)
            {
                await Task.Delay(100);
                _odorDisplay.OpenValves(session.GetActiveChannelIds());
            }
        }
        else
        {
            var ids = pulse.Channels.Where(c => c.Active).Select(c => c.Id);
            _odorDisplay.OpenValves(ids.ToArray(), session.Intervals.Pulse);
        }

        if (session.Intervals.HasDms)
        {
            _delayedActionDms = DispatchOnce.Do(session.Intervals.DmsDelay, StartDMS);
        }

        if (!session.Intervals.HasDms || session.Intervals.DmsDelay > 0 || _ionVision == null)  /// otherwise <see cref="PublishStage"/> will be called by <see cref="StartDMS"/>
        {
            PublishStage(PulseStage.Pulse);
        }

        _delayedAction = DispatchOnce.Do(session.Intervals.Pulse, FinishPulse);
    }

    private async void StartDMS()
    {
        _delayedActionDms = null;

        if (_ionVision == null)
        {
            return;
        }

        var session = _setup.Sessions[_sessionIndex];
        var pulse = session.Pulses[_pulseIndex];

        _eventLogger.Add("dms", "start");

        HandleIonVisionError(await _ionVision.StartScan(), "StartScan");

        _waitForScanProgress = true;

        _delayedActionDmsScanProgress = DispatchOnce.Do(DMS_PROGRESS_CHECK_INTERVAL, CheckDmsScanProgress);

        PublishStage(PulseStage.Pulse | PulseStage.DMS);
    }

    private async void FinishPulse()
    {
        var session = _setup.Sessions[_sessionIndex];
        var pulse = session.Pulses[_pulseIndex];

        _eventLogger.Add("pulse", _pulseIndex.ToString(), "stop");

        if (session.Intervals.InitialPause == 0 && session.Intervals.FinalPause == 0)
        {
            if (_pulseIndex == session.Pulses.Count - 1)
            {
                _odorDisplay.CloseValves(session.GetActiveChannelIds());
            }
        }
        //else
        //_odorDisplay.CloseChannels(pulse.Channels);

        _delayedAction = DispatchOnce.Do(session.Intervals.FinalPause, RunNextPulse);

        if (session.Intervals.FinalPause > 0)
        {
            PublishStage(PulseStage.FinalPause);
        }

        if (_ionVision != null && session.Intervals.HasDms)
        {
            var scan = HandleIonVisionError(await _ionVision.GetScanResult(), "GetScanResult");
            if ((scan?.Success ?? false) && (scan.Value != null))
            {
                _ionVisionLogger.Add(scan.Value);
                _dmses.Add(scan.Value);
            }
        }
    }

    private void PublishStage(PulseStage stage)
    {
        var session = _setup.Sessions[_sessionIndex];
        var pulse = session.Pulses[_pulseIndex];

        _currentStage = stage | _extraStages;

        StageChanged?.Invoke(this, new StageChangedEventArgs(session.Intervals, pulse, _currentStage, _sessionIndex + 1, _pulseIndex + 1));
        _extraStages = PulseStage.None;
    }

    private async void CheckDmsScanProgress()
    {
        _delayedActionDmsScanProgress = null;

        if (_ionVision == null || !_currentStage.HasFlag(PulseStage.Pulse))
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

    private static IonVision.Response<T> HandleIonVisionError<T>(IonVision.Response<T> response, string action)
    {
        var error = !response.Success ? response.Error : "OK";
        _nlog.Info(Logging.LogIO.Text(action, "Error", error));
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
