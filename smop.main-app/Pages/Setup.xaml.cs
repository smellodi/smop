using Smop.Common;
using Smop.MainApp.Controllers;
using Smop.MainApp.Controls;
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

        brdMeasurementProgress.Visibility = Visibility.Collapsed;

        chkShowThermistorIndicators.IsChecked = Properties.Settings.Default.Setup_ShowThermistors;
        chkShowPressureIndicators.IsChecked = Properties.Settings.Default.Setup_ShowMonometers;

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
        _ctrl.MeasurementProgress += (s, e) => Dispatcher.Invoke(() =>
        {
            prbMeasurementProgress.Value = e;
            lblMeasurementProgress.Content = $"{e}%";
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

        //btnCheckChemicalLevels.Visibility = Storage.Instance.Simulating.HasFlag(SimulationTarget.OdorDisplay) ? Visibility.Collapsed : Visibility.Visible;

        var measSource = OdorReproductionSettings.MeasurementSouce.None;
        if (App.IonVision != null)
            measSource |= OdorReproductionSettings.MeasurementSouce.DMS;
        if (_smellInsp.IsOpen)
            measSource |= OdorReproductionSettings.MeasurementSouce.SNT;
        odorReproductionSettings.SetMeasurementSource(measSource);

        if (App.IonVision == null && grdStatuses.RowDefinitions.Count == 3)
            grdStatuses.RowDefinitions.RemoveAt(1);

        if (App.IonVision == null)
            rdfDmsStatus.Height = new GridLength(0, GridUnitType.Pixel);


        /* BOTH ENOSES
        if (!_smellInsp.IsOpen && grdStatuses.RowDefinitions.Count == 2)
            grdStatuses.RowDefinitions.RemoveAt(1);

        if (!_smellInsp.IsOpen)
        rdfSntStatus.Height = new GridLength(0, GridUnitType.Pixel);
        */

        // SINGLE ENOSE
        if ((App.IonVision != null || !_smellInsp.IsOpen) && grdStatuses.RowDefinitions.Count == 3)
            grdStatuses.RowDefinitions.RemoveAt(2);

        if (App.IonVision != null || !_smellInsp.IsOpen)
            rdfSntStatus.Height = new GridLength(0, GridUnitType.Pixel);
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

    readonly List<IonVision.Defs.ScanResult> _dmsScans = new();
    readonly RadioButton[] _dmsPlotTypes;

    bool _isInitilized = false;
    bool _ionVisionIsReady = false;
    bool _isOdorDisplayCleanedUp = false;
    bool _isOdorDisplayCleaningRunning = false;
    bool _isDMSInitRunning = false;
    bool _isCollectingData = false;
    bool _isCheckngChemicalLevels = false;

    bool _doesOdorDisplayRequireCleanup = false;

    Plot.ComparisonOperation _dmsPlotType = Plot.ComparisonOperation.None;

    bool _mlIsConnected = false;

    private void UpdateUI()
    {
        if (_storage.SetupType == SetupType.PulseGenerator)
        {
            btnStart.IsEnabled = (App.IonVision == null || _ionVisionIsReady) &&
                pulseGeneratorSettings.Setup != null;
            stpTargetOdorActions.Visibility = Visibility.Collapsed;
        }
        else if (_storage.SetupType == SetupType.OdorReproduction)
        {
            bool isODReady = _isOdorDisplayCleanedUp || !_doesOdorDisplayRequireCleanup;
            bool isDmsReady = _ctrl.DmsScan != null && _ctrl.ParamDefinition != null;
            bool isSntReady = _ctrl.SntSample != null || isDmsReady;
            bool isMeasuringSomething = brdMeasurementProgress.Visibility == Visibility.Visible;

            btnStart.IsEnabled =
                isODReady &&
                (isDmsReady || App.IonVision == null) &&
                (isSntReady || !_smellInsp.IsOpen) &&
                _mlIsConnected &&
                !_isCheckngChemicalLevels &&
                !isMeasuringSomething;

            btnCheckChemicalLevels.IsEnabled = isODReady && 
                !_isCheckngChemicalLevels &&
                !isMeasuringSomething;

            btnTargetMeasure.IsEnabled = (_ionVisionIsReady || App.IonVision == null) && 
                !_isOdorDisplayCleaningRunning &&
                !isMeasuringSomething;
            btnTargetLoad.IsEnabled = btnTargetMeasure.IsEnabled;
            stpTargetOdorActions.IsEnabled = !isMeasuringSomething;

            odorReproductionSettings.MLStatus = App.ML != null && _mlIsConnected ? App.ML.ConnectionMean.ToString() : "not connected";
            odorReproductionSettings.IsMLConnected = _mlIsConnected;
            odorReproductionSettings.IsEnabled = !isMeasuringSomething;

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
            ShowDmsPlot(_dmsPlotType);
        }
        catch (Exception ex)
        {
            MsgBox.Error("DMS scan", ex.Message);
        }
    }

    private void ShowDmsPlot(Plot.ComparisonOperation compOp)
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
        _nlog.Info(Logging.LogIO.Text(Timestamp.Ms, "ML", "Status", e));
        _mlIsConnected = e == ML.Status.Connected;
        Dispatcher.Invoke(UpdateUI);
    }

    private void ML_Error(object? sender, ML.Communicator.ErrorEventHandlerArgs args)
    {
        _nlog.Error(Logging.LogIO.Text(Timestamp.Ms, "ML", "Error", args.Action, args.Error));
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

            await _indicatorController.Create(Dispatcher, stpOdorDisplayIndicators, stpSmellInspIndicators,
                chkShowThermistorIndicators.IsChecked ?? false,
                chkShowPressureIndicators.IsChecked ?? false);

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

    private async void TargetMeasure_Click(object sender, RoutedEventArgs e)
    {
        _isCollectingData = true;

        btnTargetMeasure.IsEnabled = false;

        brdMeasurementProgress.Visibility = Visibility.Visible;
        prbMeasurementProgress.Value = 0;
        lblMeasurementProgress.Content = "0%";

        UpdateUI();

        if (App.ML != null && App.ML.CmdParams != Properties.Settings.Default.Reproduction_ML_CmdParams)
        {
            App.ML.LaunchMlExe(Properties.Settings.Default.Reproduction_ML_CmdParams);
        }

        await _ctrl.MeasureSample();

        if (_ctrl.DmsScan is IonVision.Defs.ScanResult fullScan)
        {
            _dmsScans.Add(fullScan);

            if (_dmsPlotType == Plot.ComparisonOperation.None || _dmsScans.Count > 1)
            {
                ShowDmsPlot();
            }
        }
        else if (_ctrl.DmsScan is IonVision.Defs.ScopeResult scopeScan)
        {
            new Plot().Create(cnvDmsScan,
                1, scopeScan.IntensityTop.Length,
                scopeScan.IntensityTop,
                theme: PLOT_THEME);
        }

        btnTargetMeasure.IsEnabled = true;
        brdMeasurementProgress.Visibility = Visibility.Collapsed;

        _isCollectingData = false;

        UpdateUI();
    }

    private void TargetLoad_Click(object sender, RoutedEventArgs e)
    {
        UpdateUI();

        if (App.ML != null && App.ML.CmdParams != Properties.Settings.Default.Reproduction_ML_CmdParams)
        {
            App.ML.LaunchMlExe(Properties.Settings.Default.Reproduction_ML_CmdParams);
        }

        var ofd = new Microsoft.Win32.OpenFileDialog();
        if (App.IonVision != null)
        {
            ofd.Filter = "JSON files|*.json|Any file|*.*";
        }
        else if (_smellInsp.IsOpen)
        {
            ofd.Filter = "Text files|*.txt|Any file|*.*";
        }
        else
        {
            ofd.Filter = "Any file|*.*";
        }

        if (ofd.ShowDialog() ?? false)
        {
            if (System.IO.Path.GetExtension(ofd.FileName).ToLower() == ".json")
            {
                var scan = _ctrl.LoadDms(ofd.FileName);
                if (scan is IonVision.Defs.ScanResult fullScan)
                {
                    _dmsScans.Add(fullScan);

                    if (_dmsPlotType == Plot.ComparisonOperation.None || _dmsScans.Count > 1)
                    {
                        ShowDmsPlot();
                    }
                }
                else if (_ctrl.DmsScan is IonVision.Defs.ScopeResult scopeScan)
                {
                    new Plot().Create(cnvDmsScan,
                        1, scopeScan.IntensityTop.Length,
                        scopeScan.IntensityTop,
                        theme: PLOT_THEME);
                }
            }
            else if (System.IO.Path.GetExtension(ofd.FileName).ToLower() == ".txt")
            {
                _ctrl.LoadSnt(ofd.FileName);
            }
        }

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
                _ctrl.EnumOdorChannels(odorChannel =>
                {
                    if (!string.IsNullOrWhiteSpace(odorChannel.Name))
                        targetFlows.Add(new(odorChannel.ID, odorChannel.Flow));
                });

                var dataSize = new Size();

                IMeasurement? targetMeasurement = null;
                if (_ctrl.DmsScan is IonVision.Defs.ScanResult fullScan)
                {
                    targetMeasurement = fullScan;
                    if (_ctrl.ParamDefinition != null)
                    {
                        var sc = _ctrl.ParamDefinition.MeasurementParameters.SteppingControl;
                        dataSize = new(sc.Ucv.Steps, sc.Usv.Steps);
                    }
                }
                else if (_ctrl.DmsScan is IonVision.Defs.ScopeResult scopeScan)
                {
                    targetMeasurement = scopeScan;
                    dataSize = new(scopeScan.Ucv.Length, 1);
                }
                else if (_ctrl.SntSample != null)
                {
                    targetMeasurement = _ctrl.SntSample;
                }

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
        chkShowThermistorIndicators.Visibility = tabOdorDisplay.IsSelected ? Visibility.Visible : Visibility.Collapsed;
        chkShowPressureIndicators.Visibility = tabOdorDisplay.IsSelected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DmsPlotType_Click(object sender, RoutedEventArgs e)
    {
        _dmsPlotType = (Plot.ComparisonOperation)Enum.Parse(typeof(Plot.ComparisonOperation), (sender as RadioButton)!.Tag.ToString() ?? "0");
        ShowDmsPlot();
    }

    private async void CheckChemicalLevels_Click(object sender, RoutedEventArgs e)
    {
        _isCheckngChemicalLevels = true;

        brdMeasurementProgress.Visibility = Visibility.Visible;
        prbMeasurementProgress.Value = 0;
        lblMeasurementProgress.Content = "0%";

        UpdateUI();

        var chemicalLevels = await _ctrl.CheckChemicalLevels();
        var lines = chemicalLevels?.Select(gasFlow => $"{gasFlow.OdorName}: {gasFlow.Level:F1} %") ?? new string[] { "Failed to measure gas levels" };
        var msg = string.Join("\n", lines);
        if (chemicalLevels?.All(level => level.Level >= ChemicalLevel.Threshold) ?? false)
        {
            MsgBox.Notify(Title, $"All odor levels are sufficient:\n\n{msg}");
        }
        else
        {
            MsgBox.Custom(Title, $"Some odor levels are insufficient:\n\n{msg}\n\nPlease add chemicals!", MsgBox.MsgIcon.Warning, null, null, MsgBox.Button.OK);
        }

        brdMeasurementProgress.Visibility = Visibility.Collapsed;

        _isCheckngChemicalLevels = false;
        UpdateUI();
    }

    private void chkShowThermistorIndicators_Checked(object sender, RoutedEventArgs e)
    {
        bool areVisible = chkShowThermistorIndicators.IsChecked ?? false;
        Properties.Settings.Default.Setup_ShowThermistors = areVisible;
        Properties.Settings.Default.Save();

        foreach (var child in stpOdorDisplayIndicators.Children)
        {
            if (child is ChannelIndicator ci && ci.IsThermistor)
            {
                ci.Visibility = areVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void chkShowPressureIndicators_Checked(object sender, RoutedEventArgs e)
    {
        bool areVisible = chkShowPressureIndicators.IsChecked ?? false;
        Properties.Settings.Default.Setup_ShowMonometers = areVisible;
        Properties.Settings.Default.Save();

        foreach (var child in stpOdorDisplayIndicators.Children)
        {
            if (child is ChannelIndicator ci && ci.IsMonometer)
            {
                ci.Visibility = areVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
