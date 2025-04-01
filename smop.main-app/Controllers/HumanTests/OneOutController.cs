using Smop.Common;
using Smop.MainApp.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Smop.MainApp.Controllers.HumanTests;

internal class OneOutController(Settings settings) : IDisposable
{
    public class StageChangedEventArgs(Stage stage, int mixtureId = 0) : EventArgs
    {
        public Stage Stage { get; } = stage;
        public int MixtureId { get; } = mixtureId;
    }

    public double PauseBetweenBlocks => 60; // seconds

    public int TripletID => _tripletIndex + 1;
    public int MixtureID => _mixtureIndex + 1;

    public event EventHandler<StageChangedEventArgs>? StageChanged;

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _delayedAction?.Stop();
        _delayedAction?.Dispose();
        _delayedAction = null;

        OdorDisplay.CommPort.Instance.Data -= OdorDisplay_Data;
        SmellInsp.CommPort.Instance.Data -= SmellInsp_Data;

        GC.SuppressFinalize(this);
    }

    public void Start()
    {
        OdorDisplay.CommPort.Instance.Data += OdorDisplay_Data;
        SmellInsp.CommPort.Instance.Data += SmellInsp_Data;

        if (!settings.IsPracticingProcedure)
        {
            _odorDisplay.OpenValves(_session.UsedChannelIds);
        }

        _eventLogger.Add("start", settings.IsPracticingProcedure ? "practice" : "comparison");

        RunNextTriplet();
    }

    public void Continue()
    {
        if (_currentStage == Stage.UserControlledPause)
        {
            _delayedAction?.Dispose();
            _delayedAction = null;

            RunNextTriplet();
        }
    }

    public void ForceToFinish()
    {
        if (_session.Triplets.Length > 0)
        {
            _tripletIndex = _session.Triplets.Length - 1;
        }
    }

    public void Stop()
    {
        _odorDisplay.StopFlows(_session.UsedChannelIds.Select(id => (OdorDisplay.Device.ID)id).ToArray());
    }

    public void SetAnswer(string answer)
    {
        var tripet = _session.Triplets[_tripletIndex];

        tripet.Answer = int.TryParse(answer, out int id) ? id : 0;
        System.Diagnostics.Debug.WriteLine(tripet);

        _eventLogger.Add("answer", answer);

        if (PauseBetweenTrials > 0)
        {
            PublishStage(Stage.TimedPause);
            _delayedAction = DispatchOnce.Do(PauseBetweenTrials, RunNextTriplet);
        }
        else
        {
            RunNextTriplet();
        }
    }

    // Internal

    //static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(HumanTestsComparisonController));

    readonly double PauseBetweenTrials = 4;  // seconds
    readonly int BlockSize = 4;

    readonly OdorDisplayController _odorDisplay = new();
    readonly OneOutSession _session = new(settings);

    readonly EventLogger _eventLogger = EventLogger.Instance;
    readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    readonly SmellInspLogger _smellInspLogger = SmellInspLogger.Instance;

    int _tripletIndex = -1;
    int _mixtureIndex = -1;

    Stage _currentStage = Stage.Initial;

    DispatchOnce? _delayedAction = null;

    bool _isDisposed = false;

    private void RunNextTriplet()
    {
        if (_currentStage != Stage.UserControlledPause)
        {
            _tripletIndex += 1;

            if (_tripletIndex > 0 && _tripletIndex < _session.Triplets.Length && (_tripletIndex % BlockSize) == 0)
            {
                PublishStage(Stage.UserControlledPause);
                _delayedAction = DispatchOnce.Do(PauseBetweenBlocks, RunNextTriplet);
                return;
            }
        }

        if (_tripletIndex < _session.Triplets.Length)
        {
            var triplet = _session.Triplets[_tripletIndex];
            _eventLogger.Add("triplet", _tripletIndex.ToString());

            _mixtureIndex = 0;
            StartPulse(triplet.Mixtures[_mixtureIndex]);
        }
        else
        {
            foreach (var triplet in _session.Triplets)
                _eventLogger.Add("one-out", triplet.ToString());

            PublishStage(Stage.Finished);
        }
    }

    private void StartPulse(Mixture mixture)
    {
        _eventLogger.Add("mixture", _mixtureIndex.ToString(), mixture.Name, mixture.ToString());

        _odorDisplay.SetFlows(mixture.Channels);

        PublishStage(Stage.WaitingMixture);

        _delayedAction = DispatchOnce.Do(settings.WaitingInterval, OpenParticipantValve);
    }

    private void OpenParticipantValve()
    {
        _odorDisplay.SetExternalValveState(true);

        _eventLogger.Add("valve", "open");

        PublishStage(Stage.SniffingMixture);

        _delayedAction = DispatchOnce.Do(settings.SniffingInterval, CloseParticipantValve);
    }

    private void CloseParticipantValve()
    {
        _odorDisplay.SetExternalValveState(false);

        _eventLogger.Add("valve", "close");

        var triplet = _session.Triplets[_tripletIndex];

        _mixtureIndex += 1;

        if (_mixtureIndex < 3)
            StartPulse(triplet.Mixtures[_mixtureIndex]);
        else
            PublishStage(Stage.Question);
    }

    private void PublishStage(Stage stage)
    {
        _currentStage = stage;
        StageChanged?.Invoke(this, new StageChangedEventArgs(_currentStage));
    }

    private async void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
    {
        await Task.Run(() => _odorDisplayLogger.Add(data));
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data data)
    {
        await Task.Run(() => _smellInspLogger.Add(data));
    }
}