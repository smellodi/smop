using Smop.Common;
using Smop.IonVision;
using Smop.MainApp.Controllers;
using Smop.MainApp.Dialogs;
using Smop.MainApp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Smop.MainApp.Pages;

public partial class Setup : Page, IPage<object?>
{
    public event EventHandler<object?>? Next;

    public Setup()
    {
        InitializeComponent();

        brdENoseProgress.Visibility = Visibility.Collapsed;

        _dmsPlotTypes = new RadioButton[] { rdbDmsPlotTypeSingle, rdbDmsPlotTypeDiff, rdbDmsPlotTypeBlandAltman };
        foreach (int plotType in Enum.GetValues(typeof(Plot.ComparisonOperation)))
        {
            _dmsPlotTypes[plotType].Tag = plotType;
            _dmsPlotTypes[plotType].IsEnabled = false;
        }

        _indicatorController = new IndicatorController(lmsGraph);

        _ctrl = new SetupController();
        _ctrl.LogDms += (s, e) => AddToLog(LogType.DMS, e.Text, e.ReplaceLast);
        _ctrl.LogSnt += (s, e) => AddToLog(LogType.SNT, e.Text, e.ReplaceLast);
        _ctrl.LogOD += (s, e) => AddToLog(LogType.OD, e.Text, e.ReplaceLast);
        _ctrl.ScanProgress += (s, e) => Dispatcher.Invoke(() =>
        {
            prbENoseProgress.Value = e;
            lblENoseProgress.Content = $"{e}%";
        });

        pulseGeneratorSettings.Changed += (s, e) => UpdateUI();

        odorReproductionSettings.OdorNameChanged += (s, e) => _indicatorController.ApplyOdorChannelProps(e);

        DataContext = this;

        ((App)Application.Current).AddCleanupAction(_ctrl.ShutDown);
        ((App)Application.Current).AddCleanupAction(() => { App.ML?.Dispose(); });
    }

    public void Init(SetupType type, bool odorDisplayRequiresCleanup)
    {
        if (type == SetupType.OdorReproduction)
        {
            if (App.ML == null)
            {
                App.ML = new ML.Communicator(ML.Communicator.Type.Tcp, _storage.Simulating.HasFlag(SimulationTarget.ML));
                App.ML.StatusChanged += ML_StatusChanged;
                App.ML.Error += ML_Error;

                _ctrl.AcquireOdorChannelsInfo();
                _ctrl.EnumOdorChannels(odorReproductionSettings.AddOdorChannel);
            }

            App.ML?.LaunchMlExe(Properties.Settings.Default.Reproduction_ML_CmdParams);

            _doesOdorDisplayRequireCleanup = odorDisplayRequiresCleanup;
        }

        //cnvDmsScan.Children.Clear();

        pulseGeneratorSettings.Visibility = type == SetupType.PulseGenerator ? Visibility.Visible : Visibility.Collapsed;
        odorReproductionSettings.Visibility = type == SetupType.OdorReproduction ? Visibility.Visible : Visibility.Collapsed;

        if (App.IonVision == null && grdStatuses.RowDefinitions.Count == 3)
            grdStatuses.RowDefinitions.RemoveAt(1);

        grdDmsStatus.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;


        /* BOTH ENOSES
        if (!_smellInsp.IsOpen && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(1);

        grdSntStatus.Visibility = _smellInsp.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        */

        // SINGLE ENOSE
        if ((App.IonVision != null || !_smellInsp.IsOpen) && grdStatuses.RowDefinitions.Count == 3)
            grdStatuses.RowDefinitions.RemoveAt(2);

        grdSntStatus.Visibility = App.IonVision == null && _smellInsp.IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    // Internal

    enum LogType { DMS, SNT, OD }

    readonly KeyValuePair<double, Color>[] PLOT_THEME = new Dictionary<double, Color>()
    {
        { 0, (Color)Application.Current.FindResource("ColorLight") },
        { 0.05, (Color)Application.Current.FindResource("ColorLightDarker") },
        { 0.15, (Color)Application.Current.FindResource("ColorDarkLighter") },
        { 0.4, (Color)Application.Current.FindResource("ColorDark") },
        { 1, (Color)Application.Current.FindResource("ColorDarkDarker") },
    }.ToArray();

    readonly Storage _storage = Storage.Instance;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Setup) + "Page");

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    readonly IndicatorController _indicatorController;
    readonly SetupController _ctrl;

    readonly List<string> _ionVisionLog = new();
    readonly List<string> _smellInspLog = new();
    readonly List<string> _odorDisplayLog = new();

    readonly List<IonVision.Scan.ScanResult> _dmsScans = new();
    readonly RadioButton[] _dmsPlotTypes;

    bool _isInitilized = false;
    bool _ionVisionIsReady = false;
    bool _isOdorDisplayCleanedUp = false;
    bool _isOdorDisplayCleaningRunning = false;
    bool _isDMSInitRunning = false;
    bool _isCollectingData = false;

    bool _doesOdorDisplayRequireCleanup = false;

    Plot.ComparisonOperation _dmsPlotType = Plot.ComparisonOperation.None;

    bool _mlIsConnected = false;

    private void UpdateUI()
    {
        if (_storage.SetupType == SetupType.PulseGenerator)
        {
            btnStart.IsEnabled = (App.IonVision == null || _ionVisionIsReady) &&
                pulseGeneratorSettings.Setup != null;
        }
        else if (_storage.SetupType == SetupType.OdorReproduction)
        {
            bool isODReady = _isOdorDisplayCleanedUp || !_doesOdorDisplayRequireCleanup;
            bool isDmsReady = _ctrl.DmsScan != null && _ctrl.ParamDefinition != null;
            bool isSntReady = _ctrl.SntSample != null || isDmsReady;

            btnStart.IsEnabled =
                isODReady &&
                (isDmsReady || App.IonVision == null) &&
                (isSntReady || !_smellInsp.IsOpen) &&
                _mlIsConnected &&
                brdENoseProgress.Visibility != Visibility.Visible;
            btnMeasureSample.Visibility = _ionVisionIsReady || App.IonVision == null ? Visibility.Visible : Visibility.Collapsed;
            btnMeasureSample.IsEnabled = !_isOdorDisplayCleaningRunning;

            odorReproductionSettings.MLStatus = App.ML != null && _mlIsConnected ? App.ML.ConnectionMean.ToString() : "not connected";
            odorReproductionSettings.IsMLConnected = _mlIsConnected;
            odorReproductionSettings.IsEnabled = brdENoseProgress.Visibility != Visibility.Visible;

            prbODBusy.Visibility = _isOdorDisplayCleaningRunning ? Visibility.Visible : Visibility.Hidden;
            prbDMSBusy.Visibility = _isDMSInitRunning ? Visibility.Visible : Visibility.Hidden;
            prbSNTBusy.Visibility = _isCollectingData ? Visibility.Visible : Visibility.Hidden;
        }

        _dmsPlotTypes[(int)Plot.ComparisonOperation.None].IsEnabled = _dmsScans.Count > 0;
        _dmsPlotTypes[(int)Plot.ComparisonOperation.Difference].IsEnabled = _dmsScans.Count > 1;
        _dmsPlotTypes[(int)Plot.ComparisonOperation.BlandAltman].IsEnabled = _dmsScans.Count > 1;

        _dmsPlotTypes[(int)_dmsPlotType].SetValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
    }

    private void AddToLog(LogType destination, string line, bool replaceLast = false) => Dispatcher.Invoke(() =>
    {
        (IList<string> lines, TextBlock tbl, ScrollViewer scv) = destination switch
        {
            LogType.DMS => (_ionVisionLog, tblDmsStatus, scvDmsStatus),
            LogType.SNT => (_smellInspLog, tblSntStatus, scvSntStatus),
            LogType.OD => (_odorDisplayLog, tblODStatus, scvODStatus),
            _ => throw new Exception("Log type not supported")
        };

        if (replaceLast)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        lines.Add(line);
        tbl.Text = string.Join('\n', lines);
        scv.ScrollToBottom();
    });

    private void ShowDmsPlot()
    {
        try
        {
            ShowPlot(_dmsPlotType);
        }
        catch (Exception ex)
        {
            MsgBox.Error("DMS scan", ex.Message);
        }
    }

    private void ShowPlot(Plot.ComparisonOperation compOp)
    {
        if (_ctrl.ParamDefinition == null)
            return;

        if (compOp == Plot.ComparisonOperation.None)
        {
            if (_dmsScans.Count > 0)
                new Plot().Create(
                    cnvDmsScan,
                    _ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Usv.Steps,
                    _ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Ucv.Steps,
                    _dmsScans[^1].MeasurementData.IntensityTop,
                    theme: PLOT_THEME
                );
            else
                throw new Exception($"No DMS scan performed yet");
        }
        else
        {
            if (_dmsScans.Count > 1)
                new Plot().Create(
                    cnvDmsScan,
                    _ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Usv.Steps,
                    _ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Ucv.Steps,
                    _dmsScans[^1].MeasurementData.IntensityTop,
                    _dmsScans[^2].MeasurementData.IntensityTop,
                    compOp,
                    PLOT_THEME
                );
            else
                throw new Exception($"At least 2 DMS scans must be performed to display this plot");
        }
    }

    // Event handlers

    private async void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
    {
        await COMHelper.Do(Dispatcher, () =>
        {
            _indicatorController.Update(data);
        });
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data data)
    {
        await COMHelper.Do(Dispatcher, () =>
        {
            _indicatorController.Update(data);
        });
    }

    private void ML_StatusChanged(object? sender, ML.Status e)
    {
        _nlog.Info(Logging.LogIO.Text("ML", "Status", e));
        _mlIsConnected = e == ML.Status.Connected;
        Dispatcher.Invoke(UpdateUI);
    }

    private void ML_Error(object? sender, ML.Communicator.ErrorEventHandlerArgs args)
    {
        _nlog.Error(Logging.LogIO.Text("ML", "Error", args.Action, args.Error));
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
        //_dmsScans.Clear();

        if (Focusable)
        {
            Focus();
        }

        if (!_isInitilized)
        {
            _isInitilized = true;

            tabSmellInsp.IsEnabled = _smellInsp.IsOpen;
            tabIonVision.IsEnabled = App.IonVision != null;

            if (tabIonVision.IsEnabled && !tabIonVision.IsSelected)
            {
                // this is needed to arrange the Canvas, so that its ActualWidth/Height is not 0 anymore
                tabIonVision.IsSelected = true;
                await Task.Delay(10);
                tabOdorDisplay.IsSelected = true;
            }

            await _indicatorController.Create(Dispatcher, stpOdorDisplayIndicators, stpSmellInspIndicators);

            _ctrl.EnumOdorChannels(_indicatorController.ApplyOdorChannelProps);
            _ctrl.InitializeOdorPrinter();

            if (_doesOdorDisplayRequireCleanup)
            {
                _isOdorDisplayCleaningRunning = true;

                await Task.Delay(150);
                _ctrl.CleanUpOdorPrinter(() =>
                {
                    _isOdorDisplayCleaningRunning = false;
                    _isOdorDisplayCleanedUp = true;
                    UpdateUI();
                });
            }

            if (App.IonVision != null)
            {
                _isDMSInitRunning = true;
                UpdateUI();

                _ionVisionIsReady = await _ctrl.InitializeIonVision(App.IonVision);

                _isDMSInitRunning = false;
                UpdateUI();
            }
        }

        UpdateUI();
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
                App.ML.Error += ML_Error;
            }
        }
        if (e.Key == Key.F4)
        {
            Start_Click(this, new RoutedEventArgs());
        }
    }

    private async void MeasureSample_Click(object sender, RoutedEventArgs e)
    {
        _isCollectingData = true;

        btnMeasureSample.IsEnabled = false;

        brdENoseProgress.Visibility = Visibility.Visible;
        prbENoseProgress.Value = 0;
        lblENoseProgress.Content = "0%";

        UpdateUI();

        if (App.ML != null && App.ML.CmdParams != Properties.Settings.Default.Reproduction_ML_CmdParams)
        {
            App.ML.LaunchMlExe(Properties.Settings.Default.Reproduction_ML_CmdParams);
        }

        await _ctrl.MeasureSample();

        if (_ctrl.DmsScan != null)
        {
            _dmsScans.Add(_ctrl.DmsScan);

            if (_dmsPlotType == Plot.ComparisonOperation.None || _dmsScans.Count > 1)
            {
                ShowDmsPlot();
            }
        }

        btnMeasureSample.IsEnabled = true;
        brdENoseProgress.Visibility = Visibility.Collapsed;

        _isCollectingData = false;

        UpdateUI();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_storage.SetupType == SetupType.OdorReproduction)
        {
            if (_mlIsConnected && App.ML != null)
            {
                btnStart.IsEnabled = false;

                await _ctrl.ConfigureML();
                _ctrl.SaveSetup();
                UpdateUI();

                var targetFlows = new List<OdorReproducerController.OdorChannelConfig>();
                _ctrl.EnumOdorChannels(odorChannel => targetFlows.Add(new(odorChannel.ID, odorChannel.Flow)));

                var dataSize = new Size();
                if (_ctrl.ParamDefinition != null)
                {
                    var sc = _ctrl.ParamDefinition.MeasurementParameters.SteppingControl;
                    dataSize = new(sc.Ucv.Steps, sc.Usv.Steps);
                }

                IMeasurement? targetMeasurement = null;
                if (_dmsScans.Count > 0)
                    targetMeasurement = _dmsScans[^1];
                else if (_ctrl.SntSample != null)
                    targetMeasurement = _ctrl.SntSample;

                Next?.Invoke(this, new OdorReproducerController.Config(App.ML, targetFlows.ToArray(), targetMeasurement, dataSize));
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
        lmsGraph.Visibility = (tabOdorDisplay.IsSelected || tabSmellInsp.IsSelected) ? Visibility.Visible : Visibility.Hidden;
    }

    private void DmsPlotType_Click(object sender, RoutedEventArgs e)
    {
        _dmsPlotType = (Plot.ComparisonOperation)Enum.Parse(typeof(Plot.ComparisonOperation), (sender as RadioButton)!.Tag.ToString() ?? "0");
        ShowDmsPlot();
    }
}
