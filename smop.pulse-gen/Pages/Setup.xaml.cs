using Smop.OdorDisplay.Packets;
using Smop.PulseGen.Controls;
using Smop.PulseGen.Generator;
using Smop.PulseGen.Reproducer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Smop.PulseGen.Pages;

public partial class Setup : Page, IPage<object?>
{
    public enum Type { Undefined, PulseGenerator, OdorReproduction }

    public event EventHandler<object?>? Next;

    public Setup()
    {
        InitializeComponent();

        pulseGeneratorSettings.Changed += (s, e) => UpdateUI();

        DataContext = this;

        Application.Current.Exit += (s, e) => Close();
    }

    public void Init(Type type)
    {
        pulseGeneratorSettings.Visibility = type == Type.PulseGenerator ? Visibility.Visible : Visibility.Collapsed;
        odorReproductionSettings.Visibility = type == Type.OdorReproduction ? Visibility.Visible : Visibility.Collapsed;
        stpMLStatus.Visibility = odorReproductionSettings.Visibility;

        if (type == Type.OdorReproduction && App.ML == null)
        {
            App.ML = new ML.Communicator(ML.Communicator.Type.Tcp, _storage.IsDebugging);
            App.ML.StatusChanged += ML_StatusChanged;

            var channelIDs = GetAvailableChannelIDs();
            _gases = new Gases(channelIDs);

            foreach (var gas in _gases.Items)
            {
                odorReproductionSettings.AddGas(gas);
            }
        }

        /* This logic requires both SNT and DMS to be scanned if both were connected
        if (App.IonVision == null && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(0);
        if (!_smellInsp.IsOpen && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(1);

        grdDmsStatus.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;
        grdSntStatus.Visibility = _smellInsp.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        */

        // This logic prefers DMS over SNT if both were connected
        if (App.IonVision == null && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(0);
        if ((App.IonVision != null || !_smellInsp.IsOpen) && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(1);

        grdDmsStatus.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;
        grdSntStatus.Visibility = App.IonVision == null && _smellInsp.IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    // Internal

    const int SNT_MAX_DATA_COUNT = 10;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Setup) + "Page");

    readonly Storage _storage = Storage.Instance;

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    readonly Dictionary<string, ChannelIndicator> _indicators = new();

    bool _isInitilized = false;
    bool _ionVisionIsReady = false;

    ChannelIndicator? _currentIndicator = null;
    int _smellInspResistor = 0;
    List<SmellInsp.Data> _sntSamples = new();

    List<string> _ionVisionLog = new();
    List<string> _smellInspLog = new();

    // Odor Reproduction
    bool _mlIsConnected = false;
    IonVision.ScanResult? _sampleScan = null;
    IonVision.ParameterDefinition? _paramDefinition = null;
    Gases? _gases = null;

    #region Indicators

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
        await IndicatorFactory.OdorDisplay(indicator => Dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpOdorDisplayIndicators.Children.Add(indicator);
            _indicators.Add(indicator.Source, indicator);
        }));

        await IndicatorFactory.SmellInsp(indicator => Dispatcher.Invoke(() =>
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

                var source = IndicatorFactory.GetSourceId(m.Device, (OdorDisplay.Device.Capability)sv.Sensor);
                UpdateIndicator(source, value);
            }
        }
    }

    private void UpdateIndicators(SmellInsp.Data data)
    {
        var value = data.Resistances[_smellInspResistor];
        var source = IndicatorFactory.GetSourceId(IndicatorFactory.SmellInspChannels[0].Type);
        UpdateIndicator(source, value);

        source = IndicatorFactory.GetSourceId(IndicatorFactory.SmellInspChannels[1].Type);
        UpdateIndicator(source, data.Temperature);

        source = IndicatorFactory.GetSourceId(IndicatorFactory.SmellInspChannels[2].Type);
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

    #endregion

    private void UpdateUI()
    {
        if (_storage.SetupType == Type.PulseGenerator)
        {
            btnStart.IsEnabled = (App.IonVision == null || _ionVisionIsReady) && pulseGeneratorSettings.Setup != null;
        }
        else if (_storage.SetupType == Type.OdorReproduction)
        {
            /* This logic requires both SNT and DMS to be scanned if both were connected
            bool isDmsReady = (_sampleScan != null && _paramDefinition != null) || App.IonVision == null;
            bool isSntReady = !_smellInsp.IsOpen || _sntSamples.Count >= SNT_MAX_DATA_COUNT;
            btnStart.IsEnabled = isDmsReady && isSntReady && _mlIsConnected;
            btnMeasureSample.Visibility = _ionVisionIsReady || App.IonVision == null ? Visibility.Visible : Visibility.Collapsed;
            */

        // This logic prefers DMS over SNT if both were connected
        bool isDmsReady = (_sampleScan != null && _paramDefinition != null) || App.IonVision == null;
            bool isSntReady = isDmsReady || _sntSamples.Count >= SNT_MAX_DATA_COUNT;
            btnStart.IsEnabled = isDmsReady && isSntReady && _mlIsConnected;
            btnMeasureSample.Visibility = _ionVisionIsReady || App.IonVision == null ? Visibility.Visible : Visibility.Collapsed;

            tblMLStatus.Text = App.ML != null && _mlIsConnected ? $"connected via {App.ML.ConnectionMean}" : "not connected";
        }
    }

    private async Task InitializeIonVision(IonVision.Communicator ionVision)
    {
        HandleIonVisionError(await ionVision.SetClock(), "SetClock");

        await Task.Delay(300);
        AddToIonVisionLog("Current clock set");
        AddToIonVisionLog($"Loading '{ionVision.Settings.Project}' project...");

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

        AddToIonVisionLog($"Project '{ionVision.Settings.Project}' is loaded.", true);

        await Task.Delay(500);

        AddToIonVisionLog($"Loading '{ionVision.Settings.ParameterName}' parameter...");

        var getParameterResponse = await ionVision.GetParameter();
        bool hasParameterLoaded = getParameterResponse.Value?.Parameter.Id == ionVision.Settings.ParameterId;
        if (!hasParameterLoaded)
        {
            await Task.Delay(300);
            HandleIonVisionError(await ionVision.SetParameterAndPreload(), "SetParameterAndPreload");

            AddToIonVisionLog($"Parameter '{ionVision.Settings.ParameterName}' is set. Preloading...", true);
            await Task.Delay(1000);
        }

        AddToIonVisionLog($"Parameter '{ionVision.Settings.ParameterName}' is set and preloaded.", true);

        if (App.ML != null)
        {
            AddToIonVisionLog($"Retrieving the parameter...");
            var paramDefinition = HandleIonVisionError(await ionVision.GetParameterDefinition(), "GetParameterDefinition");
            _paramDefinition = paramDefinition.Value;

            await Task.Delay(500);
            AddToIonVisionLog("Ready for scanning the target odor.", true);

            App.ML.Parameter = _paramDefinition;
        }

        _ionVisionIsReady = true;

        UpdateUI();
    }

    public OdorDisplay.Device.ID[] GetAvailableChannelIDs()
    {
        var ids = new List<OdorDisplay.Device.ID>();
        var result = _odorDisplay.Request(new QueryDevices(), out Ack? ack, out Response? response);

        if (result.Error == OdorDisplay.Error.Success && response != null)
        {
            var devices = Devices.From(response);
            if (devices != null)
            {
                for (int i = 0; i < Devices.MaxOdorModuleCount; i++)
                {
                    if (devices.HasOdorModule(i))
                    {
                        ids.Add((OdorDisplay.Device.ID)(i + 1));
                    }
                }
            }
        }

        return ids.ToArray();
    }

    private async Task ConfigureML()
    {
        if (App.ML == null || _gases == null)
            return;

        var dataSources = new List<string>();
        if (App.IonVision != null)
        {
            dataSources.Add(ML.Source.DMS);
        }
        else if (_smellInsp.IsOpen)
        {
            dataSources.Add(ML.Source.SNT);
        }
        if (Properties.Settings.Default.Reproduction_UsePID)
        {
            dataSources.Add(ML.Source.PID);
        }

        if (_paramDefinition != null)
        {
            App.ML.Parameter = _paramDefinition;
        }

        var settings = Properties.Settings.Default;
        await App.ML.Config(dataSources.ToArray(),
            _gases.Items.Select(gas => new ML.ChannelProps((int)gas.ChannelID, gas.Name, gas.Propeties)).ToArray(),
            settings.Reproduction_MaxIterations,
            settings.Reproduction_Threshold
        );

        await Task.Delay(500);

        if (_sampleScan != null)
        {
            await App.ML.Publish(_sampleScan);
        }
        else if (_sntSamples.Count > 0)
        {
            foreach (var sample in _sntSamples)
            {
                await App.ML.Publish(sample);
            }
        }

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
        _nlog.Error($"{action}: {error}");
        return response;
    }

    private async Task MakeSampleScan(IonVision.Communicator ionVision)
    {
        _sampleScan = null;
        UpdateUI();

        var resp = HandleIonVisionError(await ionVision.StartScan(), "StartScan");
        if (!resp.Success)
        {
            AddToIonVisionLog("Failed to start sample scan.");
            return;
        }

        AddToIonVisionLog("Scanning...");

        var waitForScanProgress = true;

        do
        {
            await Task.Delay(1000);
            var progress = HandleIonVisionError(await ionVision.GetScanProgress(), "GetScanProgress");
            var value = progress?.Value?.Progress ?? -1;

            if (value >= 0)
            {
                waitForScanProgress = false;
                AddToIonVisionLog($"Scanning... {value} %", true);
            }
            else if (waitForScanProgress)
            {
                continue;
            }
            else
            {
                AddToIonVisionLog($"Scanning finished.", true);
                break;
            }

        } while (true);

        await Task.Delay(300);
        _sampleScan = HandleIonVisionError(await ionVision.GetScanResult(), "GetScanResult").Value;

        AddToIonVisionLog(_sampleScan != null ? "Ready to start." : "Failed to retrieve the scanning result");

        UpdateUI();
    }

    private async Task WaitForSntSamples()
    {
        AddToSmellInspLog($"Collecting SNT samples...");

        while (_sntSamples.Count < SNT_MAX_DATA_COUNT)
        {
            await Task.Delay(1000);
            AddToSmellInspLog($"Collected {_sntSamples.Count} samples...", true);
        }

        AddToSmellInspLog($"Ready to start.", true);

        UpdateUI();
    }

    private void AddToIonVisionLog(string line, bool replaceLast = false)
    {
        if (replaceLast)
        {
            _ionVisionLog.RemoveAt(_ionVisionLog.Count - 1);
        }
        _ionVisionLog.Add(line);
        tblDmsStatus.Text = string.Join('\n', _ionVisionLog);
        scvDmsStatus.ScrollToBottom();
    }

    private void AddToSmellInspLog(string line, bool replaceLast = false)
    {
        if (replaceLast)
        {
            _smellInspLog.RemoveAt(_smellInspLog.Count - 1);
        }
        _smellInspLog.Add(line);
        tblSntStatus.Text = string.Join('\n', _smellInspLog);
        scvSntStatus.ScrollToBottom();
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

    private void SmellInsp_TargetData(object? sender, SmellInsp.Data e)
    {
        if (_sntSamples.Count < SNT_MAX_DATA_COUNT)
        {
            _sntSamples.Add(e);
        }
    }

    private void ML_StatusChanged(object? sender, ML.Status e)
    {
        _mlIsConnected = e == ML.Status.Connected;
        Dispatcher.Invoke(UpdateUI);
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

        if (Focusable)
        {
            Focus();
        }

        if (_isInitilized)
        {
            return;
        }

        _sntSamples.Clear();

        // Next code is called only once

        _isInitilized = true;

        tabSmellInsp.IsEnabled = _smellInsp.IsOpen;
        tabIonVision.IsEnabled = App.IonVision != null;

        await CreateIndicators();

        var odController = new OdorDisplayController();
        HandleOdorDisplayError(odController.Init(), "initialize");

        System.Threading.Thread.Sleep(100);
        HandleOdorDisplayError(odController.Start(), "start measurements");

        if (App.IonVision != null)
        {
            await InitializeIonVision(App.IonVision);
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

    private async void MeasureSample_Click(object sender, RoutedEventArgs e)
    {
        btnMeasureSample.IsEnabled = false;
        _smellInsp.Data += SmellInsp_TargetData;

        List<Task> scans = new();
        if (App.IonVision != null)
        {
            scans.Add(MakeSampleScan(App.IonVision));
        }
        else if (_smellInsp.IsOpen)
        {
            scans.Add(WaitForSntSamples());
        }

        await Task.WhenAll(scans);

        _smellInsp.Data -= SmellInsp_TargetData;
        btnMeasureSample.IsEnabled = true;
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


    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_storage.SetupType == Type.OdorReproduction)
        {
            _gases?.Save();

            if (_mlIsConnected && App.ML != null)
            {
                btnStart.IsEnabled = false;
                await ConfigureML();
                btnStart.IsEnabled = true;

                Next?.Invoke(this, App.ML);
            }
        }
        else if (_storage.SetupType == Type.PulseGenerator)
        {
            var pulseGenSetup = pulseGeneratorSettings.Setup;

            if (pulseGenSetup != null)
            {
                Next?.Invoke(this, pulseGenSetup);
            }
        }
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        lmsGraph.Visibility = tabOdorDisplay.IsSelected ? Visibility.Visible : Visibility.Hidden;
    }
}
