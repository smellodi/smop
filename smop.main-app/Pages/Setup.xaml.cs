using Smop.IonVision;
using Smop.MainApp.Dialogs;
using Smop.MainApp.Controllers;
using Smop.MainApp.Utils;
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

        brdENoseProgress.Visibility = Visibility.Collapsed;

        _dmsPlotTypes = new RadioButton[] { rdbDmsPlotTypeSingle, rdbDmsPlotTypeDiff, rdbDmsPlotTypeBlandAltman };
        foreach (int plotType in Enum.GetValues(typeof(DataPlot.ComparisonOperation)))
        {
            _dmsPlotTypes[plotType].Tag = plotType;
            _dmsPlotTypes[plotType].IsEnabled = false;
        }

        _indicatorController = new IndicatorController(lmsGraph);

        _ctrl = new SetupController();
        _ctrl.Log += (s, e) => AddToLog(App.IonVision != null ? LogType.DMS : LogType.SNT, e.Text, e.ReplaceLast);
        _ctrl.LogDms += (s, e) => AddToLog(LogType.DMS, e.Text, e.ReplaceLast);
        _ctrl.LogSnt += (s, e) => AddToLog(LogType.SNT, e.Text, e.ReplaceLast);
        _ctrl.ScanProgress += (s, e) => Dispatcher.Invoke(() =>
        {
            prbENoseProgress.Value = e;
            lblENoseProgress.Content = $"{e}%";
        });

        pulseGeneratorSettings.Changed += (s, e) => UpdateUI();

        odorReproductionSettings.OdorNameChanged += (s, e) => _indicatorController.ApplyOdorChannelProps(e);

        DataContext = this;

        ((App)Application.Current).AddCleanupAction(_ctrl.ShutDown);
    }

    public void Init(SetupType type)
    {
        if (type == SetupType.OdorReproduction && App.ML == null)
        {
            App.ML = new ML.Communicator(ML.Communicator.Type.Tcp, _storage.Simulating.HasFlag(SimulationTarget.ML));
            App.ML.StatusChanged += ML_StatusChanged;
            App.ML.Error += ML_Error;

            _ctrl.AcquireOdorChannelsInfo();
            _ctrl.EnumOdorChannels(odorReproductionSettings.AddOdorChannel);
        }

        cnvDmsScan.Children.Clear();

        pulseGeneratorSettings.Visibility = type == SetupType.PulseGenerator ? Visibility.Visible : Visibility.Collapsed;
        odorReproductionSettings.Visibility = type == SetupType.OdorReproduction ? Visibility.Visible : Visibility.Collapsed;

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

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Setup) + "Page");

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    readonly IndicatorController _indicatorController;
    readonly SetupController _ctrl;

    readonly List<string> _ionVisionLog = new();
    readonly List<string> _smellInspLog = new();

    readonly List<MeasurementData> _dmsScans = new();
    readonly RadioButton[] _dmsPlotTypes;

    bool _isInitilized = false;
    bool _isOdorDisplayCleanedUp = false;
    bool _ionVisionIsReady = false;

    DataPlot.ComparisonOperation _dmsPlotType = DataPlot.ComparisonOperation.None;

    // Odor Reproduction
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
            bool isDmsReady = _ctrl.DmsScan != null && _ctrl.ParamDefinition != null;
            bool isSntReady = _ctrl.IsSntScanComplete || isDmsReady;
            btnStart.IsEnabled = 
                (isDmsReady || App.IonVision == null) && 
                (isSntReady || !_smellInsp.IsOpen) &&
                _mlIsConnected &&
                _isOdorDisplayCleanedUp &&
                brdENoseProgress.Visibility != Visibility.Visible;
            btnMeasureSample.Visibility = _ionVisionIsReady || App.IonVision == null ? Visibility.Visible : Visibility.Collapsed;

            odorReproductionSettings.MLStatus = App.ML != null && _mlIsConnected ? App.ML.ConnectionMean.ToString() : "";
            odorReproductionSettings.IsMLConnected = _mlIsConnected;
            odorReproductionSettings.IsEnabled = brdENoseProgress.Visibility != Visibility.Visible;
        }

        _dmsPlotTypes[(int)DataPlot.ComparisonOperation.None].IsEnabled = _dmsScans.Count > 0;
        _dmsPlotTypes[(int)DataPlot.ComparisonOperation.Difference].IsEnabled = _dmsScans.Count > 1;
        _dmsPlotTypes[(int)DataPlot.ComparisonOperation.BlandAltman].IsEnabled = _dmsScans.Count > 1;

        _dmsPlotTypes[(int)_dmsPlotType].SetValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
    }

    private void AddToLog(LogType destination, string line, bool replaceLast = false) => Dispatcher.Invoke(() =>
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

    private void ShowPlot(DataPlot.ComparisonOperation compOp)
    {
        if (_ctrl.ParamDefinition == null)
            return;

        if (compOp == DataPlot.ComparisonOperation.None)
        {
            if (_dmsScans.Count > 0)
                DataPlot.Create(
                    cnvDmsScan,
                    (int)_ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Usv.Steps,
                    (int)_ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Ucv.Steps,
                    _dmsScans[^1].IntensityTop
                );
            else
                throw new Exception($"No DMS scan performed yet");
        }
        else
        {
            if (_dmsScans.Count > 1)
                DataPlot.Create(
                    cnvDmsScan,
                    (int)_ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Usv.Steps,
                    (int)_ctrl.ParamDefinition.MeasurementParameters.SteppingControl.Ucv.Steps,
                    _dmsScans[^1].IntensityTop,
                    _dmsScans[^2].IntensityTop,
                    compOp
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
        _dmsScans.Clear();

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

            if (_storage.SetupType == SetupType.OdorReproduction)
            {
                _ctrl.CleanUpOdorPrinter(() =>
                {
                    _isOdorDisplayCleanedUp = true;
                    UpdateUI();
                });
            }

            if (App.IonVision != null)
            {
                _ionVisionIsReady = await _ctrl.InitializeIonVision(App.IonVision);
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
        btnMeasureSample.IsEnabled = false;

        brdENoseProgress.Visibility = Visibility.Visible;
        prbENoseProgress.Value = 0;
        lblENoseProgress.Content = "0%";

        UpdateUI();

        await _ctrl.MeasureSample();

        if (_ctrl.DmsScan != null)
        {
            _dmsScans.Add(_ctrl.DmsScan.MeasurementData);

            if (_dmsPlotType == DataPlot.ComparisonOperation.None || _dmsScans.Count > 1)
            {
                ShowDmsPlot();
            }
        }

        btnMeasureSample.IsEnabled = true;
        brdENoseProgress.Visibility = Visibility.Collapsed;

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
                    dataSize = new((int)sc.Ucv.Steps, (int)sc.Usv.Steps);
                }

                Next?.Invoke(this, new OdorReproducerController.Config(App.ML, targetFlows.ToArray(), dataSize));
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
        _dmsPlotType = (DataPlot.ComparisonOperation)Enum.Parse(typeof(DataPlot.ComparisonOperation), (sender as RadioButton)!.Tag.ToString() ?? "0");
        ShowDmsPlot();
    }
}
