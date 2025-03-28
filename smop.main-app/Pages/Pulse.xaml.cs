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

        Name = "Pulses";

        ((App)Application.Current).AddCleanupAction(CleanUp);
    }

    public void Start(PulseSetup setup)
    {
        grdStageDisplays.Children.Clear();

        _stageDisplays.Clear();

        var channelsExist = new bool[Devices.MaxOdorModuleCount];

        foreach (var session in setup.Sessions)
            foreach (var channelId in session.GetActiveChannelIds())
                channelsExist[channelId - 1] = true;

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

    PulseStage _stage = PulseStage.None;

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

                var flow = new Run();
                var pressure = new Run();
                var temperature = new Run();
                var value = new TextBlock
                {
                    Style = (Style)Resources["MeasurementValues"],
                };
                value.Inlines.Add(flow);
                value.Inlines.Add(new LineBreak());
                value.Inlines.Add(pressure);
                value.Inlines.Add(new LineBreak());
                value.Inlines.Add(temperature);

                container.Children.Add(label);
                container.Children.Add(valve);
                container.Children.Add(value);
                stpChannels.Children.Add(container);

                _odorChannelObservers.Add(m.Device, (flow, pressure, temperature, valve));
            }
        }
    }

    private void SetStage(PulseIntervals? intervals, PulseProps? pulse, PulseStage stage)
    {
        //if (_stage == stage)
        //{
        //    return;
        //}

        _stage = stage;

        if (stage.HasFlag(PulseStage.NewSession) && intervals != null)
        {
            psdInitialPause.Duration = (int)(intervals.InitialPause * 1000);
            psdFinalPause.Duration = (int)(intervals.FinalPause * 1000);

            runSession.Text = _controller?.SessionId.ToString() ?? "0";
            runSessionCount.Text = _controller?.SessionCount.ToString() ?? "0";
        }

        if (stage.HasFlag(PulseStage.NewPulse) && pulse != null)
        {
            foreach (var stageDisplay in _stageDisplays)
            {
                var channelFlow = pulse.Channels.FirstOrDefault(ch => ch.Id == stageDisplay.Key)?.Flow ?? 0;
                stageDisplay.Value.Flow = channelFlow;
            }

            runPulse.Text = _controller?.PulseId.ToString() ?? "0";
            runPulseCount.Text = _controller?.PulseCount.ToString() ?? "0";
        }

        IsInitialPause = stage.HasFlag(PulseStage.InitialPause);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInitialPause)));

        IsFinalPause = stage.HasFlag(PulseStage.FinalPause);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFinalPause)));

        stpDMS.Visibility = stage.HasFlag(PulseStage.DMS) ? Visibility.Visible : Visibility.Hidden;
        lblDmsProgress.Content = "DMS: started...";

        var isPulse = stage.HasFlag(PulseStage.Pulse);
        foreach (var stageDisplay in _stageDisplays)
        {
            stageDisplay.Value.IsCurrent = isPulse && (pulse?.Channels.Any(ch => ch.Id == stageDisplay.Key && ch.Active) ?? false);
        }

        //if (_preStageDisplay != null)
        psdPre.IsCurrent = isPulse;
        //if (_postStageDisplay != null)
        psdPost.IsCurrent = isPulse;

        stage = stage & ~PulseStage.NewPulse & ~PulseStage.NewSession;
        var pause = stage switch
        {
            PulseStage.InitialPause => intervals?.InitialPause ?? 0,
            PulseStage.Pulse => intervals?.Pulse ?? 0,
            (PulseStage.Pulse | PulseStage.DMS) => -1,
            PulseStage.FinalPause => intervals?.FinalPause ?? 0,
            PulseStage.None or PulseStage.Finished => 0,
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

        if (stage == PulseStage.Finished)
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
                float humidifiedAirFlow = 0;
                float dryAirFlow = 0;

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
                            humidifiedAirFlow = ((Sensor.Gas)sv).SLPM;
                            break;
                        case OdorDisplay.Device.Sensor.DilutionAirFlowSensor:
                            dryAirFlow = ((Sensor.Gas)sv).SLPM;
                            break;
                        case OdorDisplay.Device.Sensor.OdorantValveSensor:
                            chkHumidifierValveOpened.IsChecked = ((Sensor.Valve)sv).Opened;
                            break;
                        case OdorDisplay.Device.Sensor.PID:
                            lblPID.Content = $"{((Sensor.PID)sv).Volts * 1000:F1} mV";
                            break;
                        case OdorDisplay.Device.Sensor.BeadThermistor:
                            // do nothing, we have no indicator for this
                            break;
                    }
                }

                lblBaseAirFlow.Content = $"{humidifiedAirFlow:F2} : {dryAirFlow:F1} l/min";
            }
            else if (m.Device == OdorDisplay.Device.ID.DilutionAir)
            {
                float odoredAirFlow = 0;
                float clearnAirFLow = 0;

                foreach (var sv in m.SensorValues)
                {
                    switch (sv.Sensor)
                    {
                        case OdorDisplay.Device.Sensor.OdorantFlowSensor:
                            odoredAirFlow = ((Sensor.Gas)sv).SLPM;
                            break;
                        case OdorDisplay.Device.Sensor.DilutionAirFlowSensor:
                            clearnAirFLow = ((Sensor.Gas)sv).SLPM;
                            break;
                        case OdorDisplay.Device.Sensor.OutputValveSensor:
                            chkDilutionValveOpened.IsChecked = ((Sensor.Valve)sv).Opened;
                            break;
                    }
                }

                lblDilutionAirFlow.Content = $"{1000*odoredAirFlow:F0} sccm : {clearnAirFLow:F1} l/min";
            }
            else if (_odorChannelObservers?.ContainsKey(m.Device) ?? false)
            {
                var (flow, pressure, temperature, valve) = _odorChannelObservers[m.Device];
                foreach (var sv in m.SensorValues)
                {
                    switch (sv.Sensor)
                    {
                        case OdorDisplay.Device.Sensor.OdorantFlowSensor:
                            {
                                var v = (Sensor.Gas)sv;
                                flow.Text = $"{v.SLPM * 1000:F1} sccm";
                                pressure.Text = $"{v.Millibars:F1} mBar";
                                temperature.Text = $"{v.Celsius:F1} °C";
                                break;
                            }
                        case OdorDisplay.Device.Sensor.OdorantValveSensor:
                            valve.IsChecked = ((Sensor.Valve)sv).Opened;
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

        SetStage(null, null, PulseStage.None);
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

    private void Waiting_TimeUpdated(object sender, WaitingInstruction.TimeUpdatedEventArgs e)
    {
        double remainingTime = e.Remaining;
        if (remainingTime > 0)
            lblWaitingTime.Content = remainingTime.ToTime(wtiWaiting.WaitingTime);
        else
            lblWaitingTime.Content = " ";
    }
}
