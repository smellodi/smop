using Smop.Common;
using Smop.MainApp.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Smop.MainApp.Controllers.HumanTests;

internal class ComparisonController(Settings settings) : IDisposable
{
    public class StageChangedEventArgs(Stage stage, int mixtureId = 0) : EventArgs
    {
        public Stage Stage { get; } = stage;
        public int MixtureId { get; } = mixtureId;
    }

    public event EventHandler<StageChangedEventArgs>? StageChanged;

    public int BlockID => _blockIndex + 1;
    public int ComparisonID => _comparisonIndex + 1;
    public int MixtureID => _mixtureIndex + 1;

    public int BlockCount => settings.Repetitions;

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

        RunNextBlock();
    }

    public void ForceToFinish()
    {
        if (_session.Blocks.Length > 0)
        {
            _blockIndex = _session.Blocks.Length - 1;
            _comparisonIndex = _session.Blocks[_blockIndex].Comparisons.Length - 1;
        }
    }

    public void Stop()
    {
        _odorDisplay.StopFlows(_session.UsedChannelIds.Select(id => (OdorDisplay.Device.ID)id).ToArray());
    }

    public void SetAnswer(bool areSame)
    {
        var block = _session.Blocks[_blockIndex];
        var comparison = block.Comparisons[_comparisonIndex];

        comparison.AreSame = areSame;

        _eventLogger.Add("answer", areSame.ToString());

        RunNextComparison();
    }

    // Internal

    //static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(HumanTestsComparisonController));

    readonly OdorDisplayController _odorDisplay = new();
    readonly Session _session = new(settings);

    readonly EventLogger _eventLogger = EventLogger.Instance;
    readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    readonly SmellInspLogger _smellInspLogger = SmellInspLogger.Instance;

    int _blockIndex = -1;
    int _comparisonIndex = -1;
    int _mixtureIndex = -1;

    Stage _currentStage = Stage.Initial;

    DispatchOnce? _delayedAction = null;

    bool _isDisposed = false;

    private void RunNextBlock()
    {
        _blockIndex += 1;

        if (_session.Blocks.Length > _blockIndex)
        {
            var block = _session.Blocks[_blockIndex];
            _eventLogger.Add("block", _blockIndex.ToString());

            _comparisonIndex = -1;
            _delayedAction = DispatchOnce.Do(0.1, RunNextComparison);
        }
        else
        {
            foreach (var block in _session.Blocks)
                foreach (var comp in block.Comparisons)
                    _eventLogger.Add("comparison", comp.ToString() ?? "");

            StageChanged?.Invoke(this, new StageChangedEventArgs(Stage.Finished));
        }
    }

    private void RunNextComparison()
    {
        var block = _session.Blocks[_blockIndex];

        _comparisonIndex += 1;
        _mixtureIndex = 0;

        if (block.Comparisons.Length > _comparisonIndex)
        {
            var comparison = block.Comparisons[_comparisonIndex];
            _eventLogger.Add("pair", _comparisonIndex.ToString());

            StartPulse(comparison.Mixtures[_mixtureIndex]);
        }
        else
        {
            _delayedAction = DispatchOnce.Do(0.1, RunNextBlock);
        }
    }

    private void StartPulse(Mixture mixture)
    {
        var block = _session.Blocks[_blockIndex];
        var comparison = block.Comparisons[_comparisonIndex];

        _eventLogger.Add("mixture", _mixtureIndex.ToString(), mixture.Name, mixture.ToString());

        _odorDisplay.SetFlows(mixture.Channels);

        PublishStage(Stage.WaitingMixture);

        _delayedAction = DispatchOnce.Do(Mixture.WaitingInterval, OpenParticipantValve);
    }

    private void OpenParticipantValve()
    {
        _odorDisplay.SetExternalValveState(true);

        _eventLogger.Add("valve", "open");

        PublishStage(Stage.SniffingMixture);

        _delayedAction = DispatchOnce.Do(Mixture.SniffingInterval, CloseParticipantValve);
    }

    private void CloseParticipantValve()
    {
        _odorDisplay.SetExternalValveState(false);

        _eventLogger.Add("valve", "close");

        var block = _session.Blocks[_blockIndex];
        var comparison = block.Comparisons[_comparisonIndex];

        _mixtureIndex += 1;

        if (_mixtureIndex < 2)
            StartPulse(comparison.Mixtures[_mixtureIndex]);
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