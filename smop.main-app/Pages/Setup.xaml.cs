using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Smop.MainApp.Pages;

public partial class Setup : Page, IPage<object?>
{
    public event EventHandler<object?>? Next;

    public Setup()
    {
        InitializeComponent();

        _indicatorController = new Indicators.Controller(lmsGraph);

        _procedure = new SetupProcedure();
        _procedure.Log += (s, e) => AddToLog(App.IonVision != null ? LogType.DMS : LogType.SNT, e.Text, e.ReplaceLast);
        _procedure.LogDms += (s, e) => AddToLog(LogType.DMS, e.Text, e.ReplaceLast);
        _procedure.LogSnt += (s, e) => AddToLog(LogType.SNT, e.Text, e.ReplaceLast);

        pulseGeneratorSettings.Changed += (s, e) => UpdateUI();

        DataContext = this;

        Application.Current.Exit += (s, e) => _procedure.ShutDown();
    }

    public void Init(SetupType type)
    {
        if (type == SetupType.OdorReproduction && App.ML == null)
        {
            App.ML = new ML.Communicator(ML.Communicator.Type.Tcp, _storage.Simulating.HasFlag(SimulationTarget.ML));
            App.ML.StatusChanged += ML_StatusChanged;

            _procedure.EnumGases(odorReproductionSettings.AddGas);
        }

        pulseGeneratorSettings.Visibility = type == SetupType.PulseGenerator ? Visibility.Visible : Visibility.Collapsed;
        odorReproductionSettings.Visibility = type == SetupType.OdorReproduction ? Visibility.Visible : Visibility.Collapsed;
        stpMLStatus.Visibility = odorReproductionSettings.Visibility;

        if (App.IonVision == null && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(0);

        grdDmsStatus.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;

        /* BOTH ENOSES
        if (!_smellInsp.IsOpen && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(1);

        grdSntStatus.Visibility = _smellInsp.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        */

        // SINGLE ENOSE
        if ((App.IonVision != null || !_smellInsp.IsOpen) && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(1);

        grdSntStatus.Visibility = App.IonVision == null && _smellInsp.IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    // Internal

    enum LogType { DMS, SNT }

    readonly Storage _storage = Storage.Instance;

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    readonly Indicators.Controller _indicatorController;
    readonly SetupProcedure _procedure;

    bool _isInitilized = false;
    bool _ionVisionIsReady = false;

    List<string> _ionVisionLog = new();
    List<string> _smellInspLog = new();

    // Odor Reproduction
    bool _mlIsConnected = false;

    private void UpdateUI()
    {
        if (_storage.SetupType == SetupType.PulseGenerator)
        {
            btnStart.IsEnabled = (App.IonVision == null || _ionVisionIsReady) && pulseGeneratorSettings.Setup != null;
        }
        else if (_storage.SetupType == SetupType.OdorReproduction)
        {
            bool isDmsReady = _procedure.SampleScan != null && _procedure.ParamDefinition != null;
            bool isSntReady = _procedure.IsSntScanComplete || isDmsReady;
            btnStart.IsEnabled = (isDmsReady || App.IonVision == null) && (isSntReady || !_smellInsp.IsOpen) && _mlIsConnected;
            btnMeasureSample.Visibility = _ionVisionIsReady || App.IonVision == null ? Visibility.Visible : Visibility.Collapsed;

            tblMLStatus.Text = App.ML != null && _mlIsConnected ? $"connected via {App.ML.ConnectionMean}" : "not connected";
        }
    }

    private void AddToLog(LogType destination, string line, bool replaceLast = false)
    {
        var lines = destination == LogType.DMS ? _ionVisionLog : _smellInspLog;
        var tbl = destination == LogType.DMS ? tblDmsStatus : tblSntStatus;
        var scv = destination == LogType.DMS ? scvDmsStatus : scvSntStatus;

        if (replaceLast)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        lines.Add(line);
        tbl.Text = string.Join('\n', lines);
        scv.ScrollToBottom();
    }

    // Event handlers

    private async void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
    {
        try
        {
            await Task.Run(() => Dispatcher.Invoke(() =>
            {
                _indicatorController.Update(data);
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
                _indicatorController.Update(data);
            }));
        }
        catch (TaskCanceledException) { }
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

        _indicatorController.Clear();

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

        await _indicatorController.Create(Dispatcher, stpOdorDisplayIndicators, stpSmellInspIndicators);

        _procedure.InitializeOdorPrinter();

        if (App.IonVision != null)
        {
            _ionVisionIsReady = await _procedure.InitializeIonVision(App.IonVision);
            UpdateUI();
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
        if (e.Key == Key.F2)
        {
            _storage.AddSimulatingTarget(SimulationTarget.ML);

            if (App.ML != null)
            {
                App.ML.Dispose();

                App.ML = new ML.Communicator(ML.Communicator.Type.Tcp, _storage.Simulating.HasFlag(SimulationTarget.ML));
                App.ML.StatusChanged += ML_StatusChanged;
            }
        }
        if (e.Key == Key.F4)
        {
            Start_Click(this, new RoutedEventArgs());
        }
    }

    private async void MeasureSample_Click(object sender, RoutedEventArgs e)
    {
        btnMeasureSample.IsEnabled = false;

        await _procedure.MeasureSample();
        UpdateUI();

        btnMeasureSample.IsEnabled = true;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_storage.SetupType == SetupType.OdorReproduction)
        {
            if (_mlIsConnected && App.ML != null)
            {
                btnStart.IsEnabled = false;

                await _procedure.ConfigureML();
                _procedure.Finalize();

                UpdateUI();

                Next?.Invoke(this, App.ML);
            }
        }
        else if (_storage.SetupType == SetupType.PulseGenerator)
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
