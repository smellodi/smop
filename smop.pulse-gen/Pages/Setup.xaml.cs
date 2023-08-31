using Microsoft.Win32;
using Smop.OdorDisplay.Packets;
using Smop.PulseGen.Controls;
using Smop.PulseGen.Generator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Smop.PulseGen.Pages;

public partial class Setup : Page, IPage<PulseSetup>
{
    public event EventHandler<PulseSetup>? Next;

    public Setup()
    {
        InitializeComponent();

        DataContext = this;

        Application.Current.Exit += (s, e) => Close();
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Setup) + "Page");

    readonly Storage _storage = Storage.Instance;

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    readonly Dictionary<string, ChannelIndicator> _indicators = new();

    bool _isInitilized = false;
    bool _ionVisionIsReady = false;
    string? _setupFileName = null;
    PulseSetup? _setup = null;

    ChannelIndicator? _currentIndicator = null;
    int _smellInspResistor = 0;

    private void ClearIndicators()
    {
        foreach (var chi in _indicators.Values)
        {
            chi.Value = 0;
        }

        if (_currentIndicator != null)
        {
            _currentIndicator.IsActive = false;
            _currentIndicator = null;
            lmsGraph.Empty();
        }
    }

    private void ResetGraph(ChannelIndicator? chi, double baseValue = .0)
    {
        var interval = 1.0;
        if (chi == null)
        {
            interval = 1.0;
        }
        else if (chi.Source.StartsWith("od"))
        {
            interval = (double)(_storage.IsDebugging ? OdorDisplay.SerialPortEmulator.SamplingFrequency : OdorDisplay.Device.DataMeasurementInterval) / 1000;
        }
        else if (chi.Source.StartsWith("snt"))
        {
            interval = SmellInsp.ISerialPort.Interval;
        }

        lmsGraph.Reset(interval, baseValue);
    }

    private async Task CreateIndicators()
    {
        await IndicatorGenerator.OdorDisplay(indicator => Dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpOdorDisplayIndicators.Children.Add(indicator);
            _indicators.Add(indicator.Source, indicator);
        }));

        await IndicatorGenerator.SmellInsp(indicator => Dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpSmellInspIndicators.Children.Add(indicator);
            _indicators.Add(indicator.Source, indicator);
        }));

        if (stpSmellInspIndicators.Children[0] is ChannelIndicator chi)
        {
            chi.ChannelIdChanged += (s, e) =>
            {
                _smellInspResistor = e;
                ResetGraph(chi);
            };
        }
    }

    private void UpdateIndicators(Data data)
    {
        foreach (var m in data.Measurements)
        {
            bool isBase = m.Device == OdorDisplay.Device.ID.Base;
            foreach (var sv in m.SensorValues)
            {
                var value = sv switch
                {
                    PIDValue pid => pid.Volts * 1000,
                    ThermometerValue temp => temp.Celsius,          // Ignored values:
                    BeadThermistorValue beadTemp => beadTemp.Ohms,  // beadTemp.Volts
                    HumidityValue humidity => humidity.Percent,     // humidity.Celsius
                    PressureValue pressure => pressure.Millibars,   // pressure.Celsius
                    GasValue gas => isBase ?                        // gas.Millibars, gas.Celsius
                        gas.SLPM :
                        gas.SLPM * 1000,
                    ValveValue valve => valve.Opened ? 1 : 0,
                    _ => 0
                };

                var source = IndicatorGenerator.GetSourceId(m.Device, (OdorDisplay.Device.Capability)sv.Sensor);
                UpdateIndicator(source, value);
            }
        }
    }

    private void UpdateIndicators(SmellInsp.Data data)
    {
        var value = data.Resistances[_smellInspResistor];
        var source = IndicatorGenerator.GetSourceId(IndicatorGenerator.SmellInspChannels[0].Type);
        UpdateIndicator(source, value);

        source = IndicatorGenerator.GetSourceId(IndicatorGenerator.SmellInspChannels[1].Type);
        UpdateIndicator(source, data.Temperature);

        source = IndicatorGenerator.GetSourceId(IndicatorGenerator.SmellInspChannels[2].Type);
        UpdateIndicator(source, data.Humidity);
    }

    private void UpdateIndicator(string source, float value)
    {
        if (_indicators.ContainsKey(source))
        {
            var indicator = _indicators[source];
            indicator.Value = value;

            if (_currentIndicator == indicator)
            {
                double timestamp = Utils.Timestamp.Sec;
                lmsGraph.Add(timestamp, value);
            }
        }
    }

    private void LoadPulseSetup(string filename)
    {
        _setupFileName = null;

        if (!File.Exists(filename))
        {
            return;
        }

        _setup = PulseSetup.Load(filename);
        if (_setup == null)
        {
            return;
        }

        _setupFileName = filename;

        txbSetupFile.Text = _setupFileName;
        txbSetupFile.ScrollToHorizontalOffset(double.MaxValue);

        UpdateUI();

        var settings = Properties.Settings.Default;
        settings.Pulses_SetupFilename = _setupFileName;
        settings.Save();
    }

    private void UpdateUI()
    {
        btnStart.IsEnabled = _setupFileName != null && (App.IonVision == null || _ionVisionIsReady);
    }

    private async Task InitializeIonVision(IonVision.Communicator ionVision)
    {
        HandleIonVisionError(await ionVision.SetClock(), "SetClock");

        await Task.Delay(300);
        List<string> completedSteps = new() { "Current clock set", $"Loading '{ionVision.Settings.Project}' project..." };
        tblDmsStatus.Text = string.Join('\n', completedSteps);

        var response = HandleIonVisionError(await ionVision.GetProject(), "GetProject");
        if (response.Value?.Project != ionVision.Settings.Project)
        {
            await Task.Delay(300);
            HandleIonVisionError(await ionVision.SetProjectAndWait(), "SetProjectAndWait");

            bool isProjectLoaded = false;
            while (!isProjectLoaded)
            {
                await Task.Delay(1000);
                response = await ionVision.GetProject();
                if (response.Success)
                {
                    isProjectLoaded = true;
                }
                else if (response.Error?.StartsWith("Request failed") ?? false)
                {
                    _nlog.Info("loading a project...");
                }
                else
                {
                    // impossible if the project exists
                }
            }
        }

        completedSteps.RemoveAt(completedSteps.Count - 1);
        completedSteps.Add($"Project '{ionVision.Settings.Project}' is loaded.");
        tblDmsStatus.Text = string.Join('\n', completedSteps);

        await Task.Delay(500);

        completedSteps.Add($"Loading '{ionVision.Settings.ParameterName}' parameter...");
        tblDmsStatus.Text = string.Join('\n', completedSteps);

        var getParameterResponse = await ionVision.GetParameter();
        bool hasParameterLoaded = getParameterResponse.Value?.Parameter.Id == ionVision.Settings.ParameterId;
        if (!hasParameterLoaded)
        {
            await Task.Delay(300);
            HandleIonVisionError(await ionVision.SetParameterAndPreload(), "SetParameterAndPreload");

            completedSteps.RemoveAt(completedSteps.Count - 1);
            completedSteps.Add($"Parameter '{ionVision.Settings.ParameterName}' is set. Preloading...");
            tblDmsStatus.Text = string.Join('\n', completedSteps);

            await Task.Delay(1000);
        }

        completedSteps.RemoveAt(completedSteps.Count - 1);
        completedSteps.Add($"Parameter '{ionVision.Settings.ParameterName}' is set and preloaded.");
        tblDmsStatus.Text = string.Join('\n', completedSteps);

        await Task.Delay(500);
        completedSteps.Add("Done!");
        tblDmsStatus.Text = string.Join('\n', completedSteps);

        _ionVisionIsReady = true;
        UpdateUI();
    }

    private void HandleOdorDisplayError(OdorDisplay.Result odorDisplayResult, string action)
    {
        if (odorDisplayResult.Error != OdorDisplay.Error.Success)
        {
            Dialogs.MsgBox.Error(Title, $"Odor Display: Cannot {action}:\n{odorDisplayResult.Reason}");
        }
    }

    private static IonVision.API.Response<T> HandleIonVisionError<T>(IonVision.API.Response<T> response, string action)
    {
        var error = !response.Success ? response.Error : "OK";
        _nlog.Info($"{action}: {error}");
        return response;
    }

    private void Close()
    {
        var queryMeasurements = new SetMeasurements(SetMeasurements.Command.Stop);
        _odorDisplay.Request(queryMeasurements, out _, out Response? _);
    }

    // Event handlers

    private async void OdorDisplay_Data(object? sender, Data data)
    {
        try
        {
            await Task.Run(() => Dispatcher.Invoke(() =>
            {
                UpdateIndicators(data);
            }));
        }
        catch (TaskCanceledException) { }
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data data)
    {
        try
        {
            await Task.Run(() => Dispatcher.Invoke(() =>
            {
                UpdateIndicators(data);
            }));
        }
        catch (TaskCanceledException) { }
    }

    // UI events

    private async void Page_Loaded(object? sender, RoutedEventArgs e)
    {
        _storage
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        _odorDisplay.Data += OdorDisplay_Data;
        _smellInsp.Data += SmellInsp_Data;

        ClearIndicators();

        var settings = Properties.Settings.Default;
        chkRandomize.IsChecked = settings.Pulses_Randomize;

        LoadPulseSetup(settings.Pulses_SetupFilename);

        if (Focusable)
        {
            Focus();
        }

        if (_isInitilized)
        {
            return;
        }

        // Next code is called only once

        _isInitilized = true;

        tabSmellInsp.IsEnabled = _smellInsp.IsOpen;
        tabIonVision.IsEnabled = App.IonVision != null;

        await CreateIndicators();

        var odController = new OdorDisplayController();
        HandleOdorDisplayError(odController.Init(), "initilize");

        System.Threading.Thread.Sleep(100);
        HandleOdorDisplayError(odController.Start(), "start measurements");

        if (App.IonVision != null)
        {
            await InitializeIonVision(App.IonVision);
        }
        else
        {
            tblDmsStatus.Text = "not in use";
        }
    }

    private void Page_Unloaded(object? sender, RoutedEventArgs e)
    {
        _odorDisplay.Data -= OdorDisplay_Data;
        _smellInsp.Data -= SmellInsp_Data;

        _storage
            .UnbindScaleToZoomLevel(sctScale)
            .UnbindContentToZoomLevel(lblZoom)
            .UnbindVisibilityToDebug(lblDebug);
    }

    private void Page_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F4)
        {
            Start_Click(this, new RoutedEventArgs());
        }
    }

    private void ChannelIndicator_MouseDown(object? sender, MouseButtonEventArgs e)
    {
        var chi = sender as ChannelIndicator;
        if (!chi?.IsActive ?? false)
        {
            if (_currentIndicator != null)
            {
                _currentIndicator.IsActive = false;
            }

            _currentIndicator = chi;
            _currentIndicator!.IsActive = true;

            ResetGraph(chi);
        }
    }

    private void ChoosePulseSetupFile_Click(object? sender, RoutedEventArgs e)
    {
        var editor = new Dialogs.PulseSetupEditor();

        var settings = Properties.Settings.Default;
        if (string.IsNullOrEmpty(settings.Pulses_SetupFilename) || !File.Exists(settings.Pulses_SetupFilename))
        {
            settings.Pulses_SetupFilename = "Properties/setup.txt";
        }

        editor.Load(settings.Pulses_SetupFilename);

        if (editor.ShowDialog() == true)
        {
            var filename = editor.Filename ?? settings.Pulses_SetupFilename;
            new PulseSetup()
                { Sessions = editor.Sessions }
                .Save(filename);
            LoadPulseSetup(filename);
        }

        return;

        var ofd = new OpenFileDialog
        {
            Filter = "Any file|*",
            FileName = Path.GetFileName(settings.Pulses_SetupFilename),
            InitialDirectory = Path.GetDirectoryName(settings.Pulses_SetupFilename) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        if (ofd.ShowDialog() ?? false)
        {
            LoadPulseSetup(ofd.FileName);
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_setup != null)
        {
            if (chkRandomize.IsChecked == true)
            {
                _setup.Randomize();
            }

            Next?.Invoke(this, _setup);
        }
    }

    private void Randomize_CheckedChanged(object sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;
        settings.Pulses_Randomize = chkRandomize.IsChecked == true;
        settings.Save();
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        lmsGraph.Visibility = tabOdorDisplay.IsSelected ? Visibility.Visible : Visibility.Hidden;
    }
}
