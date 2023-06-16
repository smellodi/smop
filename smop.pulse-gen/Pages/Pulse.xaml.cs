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

namespace Smop.PulseGen.Pages;

public partial class Pulse : Page, IPage<bool>, ITest, INotifyPropertyChanged
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
    public event EventHandler<bool>? Next;
	public event PropertyChangedEventHandler? PropertyChanged;

	public Pulse()
	{
		InitializeComponent();

		DataContext = this;

        Application.Current.Exit += (s, e) => Interrupt();
    }

    public void Start(PulseSetup setup)
    {
        var maxChannelId = OdorDisplay.Packets.Devices.MaxOdorModuleCount;
        var channelsExist = new bool[maxChannelId];

        foreach (var session in setup.Sessions)
            foreach (var pulse in session.Pulses)
                foreach (var channel in pulse.Channels)
                    channelsExist[channel.Id - 1] = true;

        for (int i = 0; i < maxChannelId; i++)
        {
            if (channelsExist[i])
            {
                //var isCurrentBinding = new Binding("IsCurrent");
                //isCurrentBinding.Source = IsOdorFlow[i];

                var stageDisplay = new StageDisplay() { Text = $"Channel #{i + 1}" };
                stpStageDisplays.Children.Add(stageDisplay);

                _stageDisplays.Add(i + 1, stageDisplay);

                //stageDisplay.SetBinding(StageDisplay.IsCurrentProperty, isCurrentBinding);
            }
        }

        _controller = new Controller(setup);
        _controller.StageChanged += (s, e) => Dispatcher.Invoke(() => SetStage(e.Intervals, e.Pulse, e.Stage));
    }

    public void Dispose()
    {
        _controller?.Dispose();
        _controller = null;

        GC.SuppressFinalize(this);
    }

    // Internal

    Controller? _controller = null;
    Stage _stage = Stage.None;

    Dictionary<int, StageDisplay> _stageDisplays = new();

    private void Interrupt()
    {
        _controller?.Interrupt();
    }

    private void ForceToFinish()
    {
        _controller?.ForceToFinish();
    }

    private void SetStage(PulseIntervals? intervals, PulseProps? pulse, Stage stage)
    {
        if (_stage == stage) return;
        _stage = stage;

        IsInitialPause = stage == Stage.InitialPause;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInitialPause)));

        IsFinalPause = stage == Stage.FinalPause;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFinalPause)));

        var isPulse = stage == Stage.Pulse || stage == Stage.PulseWithDMS;
        foreach (var stageDisplay in _stageDisplays)
        {
            stageDisplay.Value.IsCurrent = pulse?.Channels.Any(ch => ch.Id == stageDisplay.Key && ch.Active) ?? false && isPulse;
        }

        var pause = stage switch
        {
            Stage.InitialPause => intervals?.Delay ?? 0,
            Stage.Pulse or Stage.PulseWithDMS => intervals?.Duration ?? 0,
            Stage.FinalPause => intervals?.FinalPause ?? 0,
            Stage.None => 0,
            _ => throw new NotImplementedException($"Stage '{_stage}' of does not exist")
        };

        if (pause > 1)
        {
            wtiWaiting.Start(pause);
        }
    }

    // UI events

    private void Page_Loaded(object sender, RoutedEventArgs e)
	{
		Storage.Instance
			.BindScaleToZoomLevel(sctScale)
			.BindVisibilityToDebug(lblDebug);
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
            ForceToFinish();
        }
    }

    private void Interrupt_Click(object sender, RoutedEventArgs e)
	{
		Next?.Invoke(this, false);
	}
}
