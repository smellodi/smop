using Smop.Common;
using Smop.MainApp.Controllers;
using Smop.MainApp.Controls;
using Smop.MainApp.Utils.Extensions;
using Smop.OdorDisplay.Packets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Sensor = Smop.OdorDisplay.Packets.Sensor;

namespace Smop.MainApp.Pages;

public partial class Pulse : Page, IPage<Navigation>, IDisposable, INotifyPropertyChanged
{
    public bool IsInitialPause { get; private set; } = false;
    public bool IsFinalPause { get; private set; } = false;

    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Pulse()
    {
        InitializeComponent();

        DataContext = this;

        ((App)Application.Current).AddCleanupAction(CleanUp);
    }

    public void Start(PulseSetup setup)
    {
        grdStageDisplays.Children.Clear();

        _stageDisplays.Clear();

        var maxChannelId = Devices.MaxOdorModuleCount;
        var channelsExist = new bool[maxChannelId];

        foreach (var session in setup.Sessions)
            foreach (var pulse in session.Pulses)
                foreach (var channel in pulse.Channels)
                    if (channel.Flow > 0 || channel.Active)
                        channelsExist[channel.Id - 1] = true;

        CreateChannelStageIndicators(channelsExist);

        _controller = new PulseController(setup, App.IonVision);
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

    Dictionary<OdorDisplay.Device.ID, (Run, Run, Run, CheckBox)>? _odorChannelObservers;

    PulseController? _controller = null;

    Stage _stage = Stage.None;

    DispatchOnce? _delayedAction = null;

    private void CleanUp()
    {
        _delayedAction?.Close();
        _delayedAction = null;

        _controller?.Stop();
        _controller?.Dispose();
        _controller = null;
    }

    private void CreateChannelStageIndicators(bool[] channelsExist)
    {
        var channels = new OdorChannels();

        for (int i = 0; i < channelsExist.Length; i++)
        {
            if (channelsExist[i])
            {
                var channelID = Enum.Parse<OdorDisplay.Device.ID>($"Odor{i + 1}");
                var channel = channels.FirstOrDefault(c => c.ID == channelID);

                var stageDisplay = new StageDisplay() { Text = channel?.Name ?? $"Channel #{i + 1}" };
                stageDisplay.Margin = new Thickness(12, 0, 0, 0);

                Grid.SetColumn(stageDisplay, _stageDisplays.Count);

                grdStageDisplays.Children.Add(stageDisplay);

                _stageDisplays.Add(i + 1, stageDisplay);
            }
        }
    }

    private void CreateChannelObservers(Data data)
    {
        _odorChannelObservers = new();

        var channels = new OdorChannels();

        foreach (var m in data.Measurements)
        {
            if (m.Device >= OdorDisplay.Device.ID.Odor1 && m.Device <= OdorDisplay.Device.ID.Odor9)
            {
                var channel = channels.FirstOrDefault(c => c.ID == m.Device);

                var container = new WrapPanel()
                {
                    Margin = new Thickness(0, 6, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                var label = new Label
                {
                    Content = string.IsNullOrEmpty(channel?.Name) ? m.Device.ToString() : channel.Name,
                    Style = (Style)Resources["MeasurementLabel"]
                };
                var valve = new CheckBox
                {
                    Style = (Style)Resources["MeasurementValve"]
                };

                var valueTop = new Run();
                var valueMiddle = new Run();
                var valueBottom = new Run();
                var value = new TextBlock
                {
                    Style = (Style)Resources["MeasurementValues"],
                };
                value.Inlines.Add(valueTop);
                value.Inlines.Add(new LineBreak());
                value.Inlines.Add(valueMiddle);
                value.Inlines.Add(new LineBreak());
                value.Inlines.Add(valueBottom);

                container.Children.Add(label);
                container.Children.Add(valve);
                container.Children.Add(value);
                stpChannels.Children.Add(container);

                _odorChannelObservers.Add(m.Device, (valueTop, valueMiddle, valueBottom, valve));
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
            psdInitialPause.Duration = (int)(intervals.InitialPause * 1000);
            psdFinalPause.Duration = (int)(intervals.FinalPause * 1000);

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

        //if (_preStageDisplay != null)
        psdPre.IsCurrent = isPulse;
        //if (_postStageDisplay != null)
        psdPost.IsCurrent = isPulse;

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
            lblWaitingTime.Content = wtiWaiting.WaitingTime.ToTime();
        }
        else if (pause == 0)
        {
            wtiWaiting.Reset();
            lblWaitingTime.Content = " ";
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
                            lblChassisTemp.Content = $"{((Sensor.Thermometer)sv).Celsius:F1} °C";
                            break;
                        case OdorDisplay.Device.Sensor.OdorSourceThermometer:
                            lblOdorSourceTemp.Content = $"{((Sensor.Thermometer)sv).Celsius:F1} °C";
                            break;
                        case OdorDisplay.Device.Sensor.GeneralPurposeThermometer:
                            lblGeneralTemp.Content = $"{((Sensor.Thermometer)sv).Celsius:F1} °C";
                            break;
                        case OdorDisplay.Device.Sensor.OutputAirHumiditySensor:
                            {
                                var v = (Sensor.Humidity)sv;
                                lblOutputAirHumidity.Content = $"{v.Percent:F1} %, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.InputAirHumiditySensor:
                            {
                                var v = (Sensor.Humidity)sv;
                                lblInputAirHumidity.Content = $"{v.Percent:F1} %, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.PressureSensor:
                            {
                                var v = (Sensor.Pressure)sv;
                                lblPressure.Content = $"{v.Millibars:F1} mBar, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.OdorantFlowSensor:
                            {
                                var v = (Sensor.Gas)sv;
                                lblHumidifiedAirFlow.Content = $"{v.SLPM:F1} l/min, {v.Millibars:F1} mBar, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.DilutionAirFlowSensor:
                            {
                                var v = (Sensor.Gas)sv;
                                lblDilutionAirFlow.Content = $"{v.SLPM:F1} l/min, {v.Millibars:F1} mBar, {v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.OdorantValveSensor:
                            chkOdorantValveOpened.IsChecked = ((Sensor.Valve)sv).Opened;
                            break;
                        case OdorDisplay.Device.Sensor.OutputValveSensor:
                            chkOutputValveOpened.IsChecked = ((Sensor.Valve)sv).Opened;
                            break;
                        case OdorDisplay.Device.Sensor.PID:
                            lblPID.Content = $"{((Sensor.PID)sv).Volts * 1000:F1} mV";
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
                                var v = (Sensor.Gas)sv;
                                observer.Item1.Text = $"{v.SLPM * 1000:F1} sccm";
                                observer.Item2.Text = $"{v.Millibars:F1} mBar";
                                observer.Item3.Text = $"{v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.OdorantValveSensor:
                            observer.Item4.IsChecked = ((Sensor.Valve)sv).Opened;
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
        Next?.Invoke(this, Storage.Instance.SetupPage);
    }

    private void Waiting_TimeUpdated(object sender, double e)
    {
        double remainingTime = wtiWaiting.WaitingTime - e;
        if (remainingTime > 0)
            lblWaitingTime.Content = (wtiWaiting.WaitingTime - e).ToTime(wtiWaiting.WaitingTime);
        else
            lblWaitingTime.Content = " ";
    }
}
