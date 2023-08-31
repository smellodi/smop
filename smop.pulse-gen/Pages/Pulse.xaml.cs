using Smop.OdorDisplay.Packets;
using Smop.PulseGen.Controls;
using Smop.PulseGen.Generator;
using Smop.PulseGen.Logging;
using Smop.PulseGen.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Smop.PulseGen.Pages;

public partial class Pulse : Page, IPage<Navigation>, IDisposable, INotifyPropertyChanged
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

    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Pulse()
    {
        InitializeComponent();

        DataContext = this;

        pdsInitialPause.Flow = -1;
        pdsFinalPause.Flow = -1;

        Application.Current.Exit += (s, e) => CleanUp();
    }

    public void Start(PulseSetup setup)
    {
        var maxChannelId = Devices.MaxOdorModuleCount;
        var channelsExist = new bool[maxChannelId];

        foreach (var session in setup.Sessions)
            foreach (var pulse in session.Pulses)
                foreach (var channel in pulse.Channels)
                    if (channel.Flow > 0 || channel.Active)
                        channelsExist[channel.Id - 1] = true;

        stpStageDisplays.Children.Clear();
        _stageDisplays.Clear();

        CreateChannelStageIndicators(channelsExist);
        CreateAdditionalStageIndicators();

        _controller = new PulseController(setup);
        _controller.StageChanged += (s, e) => Dispatcher.Invoke(() => SetStage(e.Intervals, e.Pulse, e.Stage));
        _controller.DmsScanProgressChanged += (s, e) => Dispatcher.Invoke(() => SetDmsProgress(e));
        _controller.OdorDisplayDataArrived += (s, e) => Dispatcher.Invoke(() => SetMeasurments(e));

        _delayedAction = DispatchOnce.Do(0.5, () => _controller?.Start());
    }

    public void Dispose()
    {
        CleanUp();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly Dictionary<int, StageDisplay> _stageDisplays = new();

    Dictionary<OdorDisplay.Device.ID, (Label, CheckBox)>? _odorChannelObservers;

    PulseController? _controller = null;

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
        _preStageDisplay = new StageDisplay() { Width = 24, Margin = new Thickness(0, 0, 12, 0), Flow = -1 };
        stpStageDisplays.Children.Insert(0, _preStageDisplay);

        _postStageDisplay = new StageDisplay() { Width = 24, Margin = new Thickness(12, 0, 0, 0), Flow = -1 };
        stpStageDisplays.Children.Add(_postStageDisplay);
    }


    private void CreateChannelObservers(Data data)
    {
        _odorChannelObservers = new();

        foreach (var m in data.Measurements)
        {
            if (m.Device >= OdorDisplay.Device.ID.Odor1 && m.Device <= OdorDisplay.Device.ID.Odor9)
            {
                var container = new WrapPanel() { Margin = new Thickness(0, 6, 0, 6) };
                var label = new Label
                {
                    Content = m.Device,
                    Style = (Style)Resources["MeasurementLabel"]
                };
                var valve = new CheckBox
                {
                    Style = (Style)Resources["MeasurementValve"]
                };
                var value = new Label
                {
                    Content = 0,
                    Style = (Style)Resources["MeasurementValue"]
                };

                container.Children.Add(label);
                container.Children.Add(valve);
                container.Children.Add(value);
                stpChannels.Children.Add(container);

                _odorChannelObservers.Add(m.Device, (value, valve));
            }
        }
    }

    private void SetStage(PulseIntervals? intervals, PulseProps? pulse, Stage stage)
    {
        if (_stage == stage)
        {
            return;
        }

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
                var channelFlow = pulse.Channels.FirstOrDefault(ch => ch.Id == stageDisplay.Key)?.Flow ?? 0;
                stageDisplay.Value.Flow = channelFlow;
            }

            runPulse.Text = _controller?.PulseId.ToString() ?? "0";
            runPulseCount.Text = _controller?.PulseCount.ToString() ?? "0";
        }

        IsInitialPause = stage.HasFlag(Stage.InitialPause);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInitialPause)));

        IsFinalPause = stage.HasFlag(Stage.FinalPause);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFinalPause)));

        stpDMS.Visibility = stage.HasFlag(Stage.DMS) ? Visibility.Visible : Visibility.Hidden;
        lblDmsProgress.Content = "DMS: started...";

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
            _ => throw new NotImplementedException($"Stage '{_stage}' does not exist")
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

    private void SetDmsProgress(int progress)
    {
        lblDmsProgress.Content = progress >= 0 ? $"DMS: {progress}%" : $"DMS: finished";
    }

    private void SetMeasurments(Data data)
    {
        if (_odorChannelObservers == null)
        {
            CreateChannelObservers(data);
        }

        foreach (var m in data.Measurements)
        {
            if (m.Device == OdorDisplay.Device.ID.Base)
            {
                foreach (var sv in m.SensorValues)
                {
                    switch (sv.Sensor)
                    {
                        case OdorDisplay.Device.Sensor.ChassisThermometer:
                            lblChassisTemp.Content = $"{((ThermometerValue)sv).Celsius:F1} °C";
                            break;
                        case OdorDisplay.Device.Sensor.OdorSourceThermometer:
                            lblOdorSourceTemp.Content = $"{((ThermometerValue)sv).Celsius:F1} °C";
                            break;
                        case OdorDisplay.Device.Sensor.GeneralPurposeThermometer:
                            lblGeneralTemp.Content = $"{((ThermometerValue)sv).Celsius:F1} °C";
                            break;
                        case OdorDisplay.Device.Sensor.OutputAirHumiditySensor:
                            {
                                var v = (HumidityValue)sv;
                                lblOutputAirHumidity.Content = $"{v.Percent:F1} %, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.InputAirHumiditySensor:
                            {
                                var v = (HumidityValue)sv;
                                lblInputAirHumidity.Content = $"{v.Percent:F1} %, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.PressureSensor:
                            {
                                var v = (PressureValue)sv;
                                lblPressure.Content = $"{v.Millibars:F1} mBar, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.OdorantFlowSensor:
                            {
                                var v = (GasValue)sv;
                                lblHumidifiedAirFlow.Content = $"{v.SLPM:F1} l/min, {v.Millibars:F1} mBar, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.DilutionAirFlowSensor:
                            {
                                var v = (GasValue)sv;
                                lblDilutionAirFlow.Content = $"{v.SLPM:F1} l/min, {v.Millibars:F1} mBar, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.OdorantValveSensor:
                            chkOdorantValveOpened.IsChecked = ((ValveValue)sv).Opened;
                            break;
                        case OdorDisplay.Device.Sensor.OutputValveSensor:
                            chkOutputValveOpened.IsChecked = ((ValveValue)sv).Opened;
                            break;
                        case OdorDisplay.Device.Sensor.PID:
                            lblPID.Content = $"{((PIDValue)sv).Volts * 1000:F1} mV";
                            break;
                        case OdorDisplay.Device.Sensor.BeadThermistor:
                            // do nothing, we have no indicator for this
                            break;
                    }
                }
            }
            else if (_odorChannelObservers?.ContainsKey(m.Device) ?? false)
            {
                var observer = _odorChannelObservers[m.Device];
                foreach (var sv in m.SensorValues)
                {
                    switch (sv.Sensor)
                    {
                        case OdorDisplay.Device.Sensor.OdorantFlowSensor:
                            {
                                var v = (GasValue)sv;
                                observer.Item1.Content = $"{v.SLPM * 1000:F1} sccm,\n{v.Millibars:F1} mBar,\n{v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.OdorantValveSensor:
                            observer.Item2.IsChecked = ((ValveValue)sv).Opened;
                            break;
                    }
                }
            }
        }
    }

    // UI events

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        if (Focusable)
        {
            Focus();
        }

        SetStage(null, null, Stage.None);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .UnbindScaleToZoomLevel(sctScale)
            .UnbindContentToZoomLevel(lblZoom)
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
