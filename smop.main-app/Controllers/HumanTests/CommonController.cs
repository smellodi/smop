using Smop.Common;
using Smop.MainApp.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Smop.MainApp.Controllers.HumanTests;

internal abstract class CommonController : IDisposable
{
    public class StageChangedEventArgs(Stage stage) : EventArgs
    {
        public Stage Stage { get; } = stage;
    }

    public static double PauseBetweenBlocks => 30; // seconds

    public string Name { get; }
    public int MixtureID => _mixtureIndex + 1;

    public event EventHandler<StageChangedEventArgs>? StageChanged;

    public CommonController(string name, Settings settings)
    {
        Name = name;
        _settings = settings;
        _usedChannelIds = OdorDisplayHelper.GetChannelIds(_settings.Channels);
    }

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

    public virtual void Start()
    {
        OdorDisplay.CommPort.Instance.Data += OdorDisplay_Data;
        SmellInsp.CommPort.Instance.Data += SmellInsp_Data;

        if (!_settings.IsPracticingProcedure)
        {
            _odorDisplay.OpenValves(_usedChannelIds);
        }

        _eventLogger.Add("participant", _settings.ParticipantID.ToString());
        _eventLogger.Add("start", _settings.IsPracticingProcedure ? $"practice_{Name}" : Name);
    }

    public virtual void Stop()
    {
        _odorDisplay.StopFlows(_usedChannelIds.Select(id => (OdorDisplay.Device.ID)id).ToArray());
    }

    // Internal

    protected readonly double PauseBetweenTrials = 4;  // seconds

    protected readonly Settings _settings;
    protected readonly OdorDisplayController _odorDisplay = new();

    protected readonly EventLogger _eventLogger = EventLogger.Instance;
    protected readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    protected readonly SmellInspLogger _smellInspLogger = SmellInspLogger.Instance;

    protected DispatchOnce? _delayedAction = null;
    protected bool _isDisposed = false;
    protected int _mixtureIndex = -1;

    protected bool IsPaused => _currentStage == Stage.UserControlledPause;

    private readonly int[] _usedChannelIds;

    private Stage _currentStage = Stage.Initial;

    protected virtual void StartPulse(Mixture mixture)
    {
        _eventLogger.Add("mixture", _mixtureIndex.ToString(), mixture.Name, mixture.ToString());

        _odorDisplay.SetFlows(mixture.Channels);

        PublishStage(Stage.WaitingMixture);

        _delayedAction = DispatchOnce.Do(_settings.WaitingInterval, OpenParticipantValve);
    }

    protected virtual void OpenParticipantValve()
    {
        _odorDisplay.SetExternalValveState(true);

        _eventLogger.Add("valve", "open");

        PublishStage(Stage.SniffingMixture);

        _delayedAction = DispatchOnce.Do(_settings.SniffingInterval, CloseParticipantValve);
    }

    protected virtual void CloseParticipantValve()
    {
        _odorDisplay.SetExternalValveState(false);

        _eventLogger.Add("valve", "close");

        Mixture? nextMixture = GetNextMixture();

        if (nextMixture != null)
            StartPulse(nextMixture);
        else
            PublishStage(GetStageAfterMixture());
    }

    protected void PublishStage(Stage stage)
    {
        _currentStage = stage;
        StageChanged?.Invoke(this, new StageChangedEventArgs(_currentStage));
    }

    /// <summary>
    /// If there are more than one mixture to present withing the same trial, then this 
    /// function should increase <see cref="_mixtureIndex"/> and return the mixture 
    /// to present, or null if no more trial mixtures exist
    /// </summary>
    /// <returns>Next mixture to present, or null</returns>
    protected abstract Mixture? GetNextMixture();

    /// <summary>
    /// This function returns the stage after all trial mixtures were presented
    /// </summary>
    /// <returns>stage after all trial mixtures were presented</returns>
    protected abstract Stage GetStageAfterMixture();

    private async void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
    {
        await Task.Run(() => _odorDisplayLogger.Add(data));
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data data)
    {
        await Task.Run(() => _smellInspLogger.Add(data));
    }
}
