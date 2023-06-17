using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Smop.PulseGen.Controls;
using Smop.PulseGen.Logging;
using Smop.PulseGen.Test;
using Smop.PulseGen.Utils;

namespace Smop.PulseGen.Pages;

public partial class Pulse : Page, IPage<Navigation>, ITest, INotifyPropertyChanged
{
	public class RequestSavingArgs : EventArgs
	{
		public SavingResult Result { get; set; }
		public RequestSavingArgs(SavingResult result)
		{
			Result = result;
		}
	}

    public bool IsInitialPause { get; private set; } = false;
    public bool IsFinalPause { get; private set; } = false;

    /// <summary>
    /// true: finished all trials, false: interrupted
    /// </summary>
    public event EventHandler<Navigation>? Next;
	public event PropertyChangedEventHandler? PropertyChanged;

	public Pulse()
	{
		InitializeComponent();

		DataContext = this;

        Application.Current.Exit += (s, e) => CleanUp();
    }

    public void Start(PulseSetup setup)
    {
        var maxChannelId = OdorDisplay.Packets.Devices.MaxOdorModuleCount;
        var channelsExist = new bool[maxChannelId];

        foreach (var session in setup.Sessions)
            foreach (var pulse in session.Pulses)
                foreach (var channel in pulse.Channels)
                    channelsExist[channel.Id - 1] = true;

        stpStageDisplays.Children.Clear();
        _stageDisplays.Clear();

        CreateChannelStageIndicators(channelsExist);
        CreateAdditionalStageIndicators();

        _controller = new Controller(setup);
        _controller.StageChanged += (s, e) => Dispatcher.Invoke(() => SetStage(e.Intervals, e.Pulse, e.Stage));

        _delayedAction = DispatchOnce.Do(0.5, () => _controller?.Start());
    }

    public void Dispose()
    {
        CleanUp();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly Dictionary<int, StageDisplay> _stageDisplays = new();

    Controller? _controller = null;

    Stage _stage = Stage.None;

    StageDisplay? _preStageDisplay;
    StageDisplay? _postStageDisplay;

    DispatchOnce? _delayedAction = null;

    private void CleanUp()
    {
        _delayedAction?.Close();
        _delayedAction = null;

        _controller?.Dispose();
        _controller = null;
    }

    private void CreateChannelStageIndicators(bool[] channelsExist)
    {
        for (int i = 0; i < channelsExist.Length; i++)
        {
            if (channelsExist[i])
            {
                var stageDisplay = new StageDisplay() { Text = $"Channel #{i + 1}" };
                stpStageDisplays.Children.Add(stageDisplay);

                _stageDisplays.Add(i + 1, stageDisplay);
            }
        }
    }

    private void CreateAdditionalStageIndicators()
    {
        _preStageDisplay = new StageDisplay() { Width = 24 };
        stpStageDisplays.Children.Insert(0, _preStageDisplay);

        _postStageDisplay = new StageDisplay() { Width = 24 };
        stpStageDisplays.Children.Add(_postStageDisplay);
    }

    private void SetStage(PulseIntervals? intervals, PulseProps? pulse, Stage stage)
    {
        if (_stage == stage) return;
        _stage = stage;

        if (stage.HasFlag(Stage.NewSession) && intervals != null)
        {
            pdsInitialPause.Duration = (int)(intervals.InitialPause * 1000);
            pdsFinalPause.Duration = (int)(intervals.FinalPause * 1000);

            runSession.Text = _controller?.SessionId.ToString() ?? "0";
            runSessionCount.Text = _controller?.SessionCount.ToString() ?? "0";
        }

        if (stage.HasFlag(Stage.NewPulse) && pulse != null)
        {
            foreach (var stageDisplay in _stageDisplays)
            {
                stageDisplay.Value.Flow = pulse.Channels.First(ch => ch.Id == stageDisplay.Key).Flow;
            }

            runPulse.Text = _controller?.PulseId.ToString() ?? "0";
            runPulseCount.Text = _controller?.PulseCount.ToString() ?? "0";
        }

        IsInitialPause = stage.HasFlag(Stage.InitialPause);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInitialPause)));

        IsFinalPause = stage.HasFlag(Stage.FinalPause);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFinalPause)));

        stpDMS.Visibility = stage.HasFlag(Stage.DMS) ? Visibility.Visible : Visibility.Hidden;

        var isPulse = stage.HasFlag(Stage.Pulse);
        foreach (var stageDisplay in _stageDisplays)
        {
            stageDisplay.Value.IsCurrent = isPulse && (pulse?.Channels.Any(ch => ch.Id == stageDisplay.Key && ch.Active) ?? false);
        }

        if (_preStageDisplay != null)
            _preStageDisplay.IsCurrent = isPulse;
        if (_postStageDisplay != null)
            _postStageDisplay.IsCurrent = isPulse;

        stage = stage & ~Stage.NewPulse & ~Stage.NewSession;
        var pause = stage switch
        {
            Stage.InitialPause => intervals?.InitialPause ?? 0,
            Stage.Pulse => intervals?.Pulse ?? 0,
            (Stage.Pulse | Stage.DMS) => -1,
            Stage.FinalPause => intervals?.FinalPause ?? 0,
            Stage.None or Stage.Finished => 0,
            _ => throw new NotImplementedException($"Stage '{_stage}' of does not exist")
        };

        if (pause > 1)
        {
            wtiWaiting.Start(pause);
        }
        else if (pause == 0)
        {
            wtiWaiting.Reset();
        }

        if (stage == Stage.Finished)
        {
            CleanUp();
            Next?.Invoke(this, Navigation.Finished);
        }
    }

    // UI events

    private void Page_Loaded(object sender, RoutedEventArgs e)
	{
		Storage.Instance
			.BindScaleToZoomLevel(sctScale)
			.BindVisibilityToDebug(lblDebug);

        SetStage(null, null, Stage.None);
	}

	private void Page_Unloaded(object sender, RoutedEventArgs e)
	{
		Storage.Instance
			.UnbindScaleToZoomLevel(sctScale)
			.UnbindVisibilityToDebug(lblDebug);
	}

    private void Page_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            _controller?.ForceToFinish();
        }
    }

    private void Interrupt_Click(object sender, RoutedEventArgs e)
	{
        CleanUp();
		Next?.Invoke(this, Navigation.Setup);
	}
}
