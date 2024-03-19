using Smop.Common;
using Smop.MainApp.Controllers;
using Smop.MainApp.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ODDevice = Smop.OdorDisplay.Device;
using ODPackets = Smop.OdorDisplay.Packets;

namespace Smop.MainApp.Pages;

public partial class Reproduction : Page, IPage<Navigation>
{
    public event EventHandler<Navigation>? Next;

    public Reproduction()
    {
        InitializeComponent();

        _inactiveElementStyle = FindResource("Element") as Style;
        _activeElementStyle = FindResource("ActiveElement") as Style;
        _recipeValueStyle = FindResource("RecipeValue") as Style;
        _recipeLabelStyle = FindResource("RecipeLabel") as Style;
        _odChannelStyle = FindResource("OdorDisplayMeasurement") as Style;
        _odChannelLabelStyle = FindResource("OdorDisplayMeasurementLabel") as Style;

        ((App)Application.Current).AddCleanupAction(CleanUp);
        OdorDisplay.CommPort.Instance.Closed += (s, e) => SetConnectionColor(cclODStatus, false);
    }

    public void Start(OdorReproducerController.Config config)
    {
        config.MLComm.StatusChanged += (s, e) => SetConnectionColor(cclMLStatus, e == ML.Status.Connected);

        _proc = new OdorReproducerController(config);
        _proc.ScanFinished += (s, e) => Dispatcher.Invoke(() => HandleScanFinished(e, config.DataSize));
        _proc.ScopeScanFinished += (s, e) => Dispatcher.Invoke(() => HandleScanFinished(e));
        _proc.SntCollected += (s, e) => Dispatcher.Invoke(() => HandleSntCollected(e));
        _proc.MlComputationStarted += (s, e) => Dispatcher.Invoke(() =>
        {
            SetActiveElement(ActiveElement.ML);
            adaAnimation.Next();
        });
        _proc.ENoseStarted += (s, e) => Dispatcher.Invoke(() =>
        {
            SetActiveElement(ActiveElement.OdorDisplay | ActiveElement.ENose);
            adaAnimation.Next();
        });
        _proc.ENoseProgressChanged += (s, e) => Dispatcher.Invoke(() =>
        {
            prbENoseProgress.Value = e;
            lblENoseProgress.Content = $"{e}%";
        });
        _proc.OdorDisplayData += (s, e) => Dispatcher.Invoke(() => DisplayODState(e));

        _procConfig = config;
        _procConfig.MLComm.RecipeReceived += HandleRecipe;

        imgDms.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;
        imgSnt.Visibility = App.IonVision == null && SmellInsp.CommPort.Instance.IsOpen ? Visibility.Visible : Visibility.Collapsed;

        tblRecipeName.Text = "";
        
        lblMeasurementInfo.Content = "";

        adaAnimation.Visibility = Visibility.Visible;
        prbProgress.Value = 0;
        prbProgress.Maximum = (Properties.Settings.Default.Reproduction_ML_MaxIterations + 1) * 5;  // this may change in future

        crtRMSE.Reset();
        crtBestRMSE.Reset();
        cnvMeasurement.Children.Clear();

        crtSearchSpace.Reset();
        if (Storage.Instance.Simulating.HasFlag(SimulationTarget.ML))
        {
            crtSearchSpace.RmseThreshold = 0.1f;
        }
        if (Storage.Instance.Simulating.HasFlag(SimulationTarget.IonVision))
        {
            crtSearchSpace.RmseThreshold = 2.5f;
        }
        crtSearchSpace.Add(0, config.TargetFlows[0].Flow, config.TargetFlows[1].Flow, System.Drawing.Color.Black);

        tblOdor1.Text = _proc.OdorChannels[0].Name;
        tblOdor2.Text = _proc.OdorChannels[1].Name;

        if (config.TargetMeasurement is IonVision.Scan.ScanResult dms)
        {
            DispatchOnce.Do(0.4, () => Dispatcher.Invoke(() =>
                new IonVision.Plot().Create(cnvTargetMeasurement,
                    (int)config.DataSize.Height,
                    (int)config.DataSize.Width,
                    dms.MeasurementData.IntensityTop,
                    theme: PLOT_THEME)));
        }
        else if (config.TargetMeasurement is SmellInsp.Data snt)
        {
            // TODO
            int a = 0;
        }

        var odorChannels = _proc.OdorChannels;
        ConfigureChannelTable(odorChannels, grdODChannels, _odChannelLabelStyle, _odChannelStyle, MEASUREMENT_ROW_FIRST_ODOR_CHANNEL);
        ConfigureChannelTable(odorChannels, grdRecipeChannels, _recipeLabelStyle, _recipeValueStyle, 1);

        DisplayRecipeInfo(new ML.Recipe("", false, 0, 0, odorChannels.Select(odorChannel => new ML.ChannelRecipe((int)odorChannel.ID, -1, -1)).ToArray()));

        SetActiveElement(ActiveElement.ML);
        SetConnectionColor(cclMLStatus, config.MLComm.IsConnected);
        SetConnectionColor(cclODStatus, OdorDisplay.CommPort.Instance.IsOpen);
    }

    // Internal

    [Flags]
    enum ActiveElement
    {
        None = 0,
        ML = 1,
        OdorDisplay = 2,
        ENose = 4
    }

    record class MeasurementEnvInfo(double Temperatore);

    const int MEASUREMENT_ROW_PID = 0;
    const int MEASUREMENT_ROW_HUMIDITY = 1;
    const int MEASUREMENT_ROW_TEMPERATURE_GAS = 2;
    const int MEASUREMENT_ROW_TEMPERATURE_ENOSE = 3;
    const int MEASUREMENT_ROW_FIRST_ODOR_CHANNEL = 5;

    readonly KeyValuePair<double, Color>[] PLOT_THEME = new Dictionary<double, Color>()
    {
        { 0, (Color)Application.Current.FindResource("ColorLight") },
        { 0.05, (Color)Application.Current.FindResource("ColorLightDarker") },
        { 0.15, (Color)Application.Current.FindResource("ColorDarkLighter") },
        { 0.4, (Color)Application.Current.FindResource("ColorDark") },
        { 1, (Color)Application.Current.FindResource("ColorDarkDarker") },
    }.ToArray();

    readonly Style? _activeElementStyle;
    readonly Style? _inactiveElementStyle;
    readonly Style? _recipeValueStyle;
    readonly Style? _recipeLabelStyle;
    readonly Style? _odChannelStyle;
    readonly Style? _odChannelLabelStyle;

    readonly BlurEffect _blurEffect = new() { Radius = 3 };

    OdorReproducerController? _proc;
    OdorReproducerController.Config? _procConfig = null;

    ActiveElement _activeElement = ActiveElement.None;

    private Style? BoolToStyle(bool isActive) => isActive ? _activeElementStyle : _inactiveElementStyle;

    private static Visibility BoolToVisible(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Hidden;

    private static UIElement? GetElementInGrid(Grid grid, int row, int column)
    {
        foreach (UIElement element in grid.Children)
        {
            if (Grid.GetColumn(element) == column && Grid.GetRow(element) == row)
                return element;
        }

        return null;
    }

    private static T? GetMeasurement<T>(ODPackets.Measurement measurement, ODDevice.Sensor sensor)
        where T : ODPackets.SensorValue
    {
        T? result = default;

        var sensorValues = measurement.SensorValues.FirstOrDefault(sv => sv.Sensor == sensor);
        if (sensorValues is T desiredSensor)
        {
            result = desiredSensor;
        }

        return result;
    }

    private static void ConfigureChannelTable(OdorChannel[] odorChannels, Grid grid, Style? labelStyle, Style? valueStyle, int constantRowCount = 0)
    {
        var elementsToRemove = new List<UIElement>();
        foreach (UIElement el in grid.Children)
        {
            if (Grid.GetRow(el) >= constantRowCount)
            {
                elementsToRemove.Add(el);
            }
        }

        foreach (var el in elementsToRemove)
        {
            grid.Children.Remove(el);
        }

        if (grid.RowDefinitions.Count > constantRowCount)
        {
            grid.RowDefinitions.RemoveRange(constantRowCount, grid.RowDefinitions.Count - constantRowCount);
        }

        foreach (var odorChannel in odorChannels)
        {
            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            AddLabel(grid, odorChannel.Name, grid.RowDefinitions.Count - 1, 0, labelStyle);
            AddTextBlock(grid, "", grid.RowDefinitions.Count - 1, 1, valueStyle);
        }

        // RMSE row
        grid.RowDefinitions.Add(new RowDefinition() { MinHeight = 36 });
    }

    private void SetActiveElement(ActiveElement el)
    {
        _activeElement = el;

        bool isActiveML = _activeElement.HasFlag(ActiveElement.ML);
        bool isActiveOD = _activeElement.HasFlag(ActiveElement.OdorDisplay);
        bool isActiveENose = _activeElement.HasFlag(ActiveElement.ENose);
        bool hasNoActiveElement = _activeElement == ActiveElement.None;

        brdML.Style = BoolToStyle(isActiveML);
        imgMLActive.Visibility = BoolToVisible(isActiveML);
        imgMLPassive.Visibility = BoolToVisible(!isActiveML);

        brdOdorDisplay.Style = BoolToStyle(isActiveOD);

        imgGas.Visibility = BoolToVisible(isActiveOD || hasNoActiveElement);

        brdENoses.Style = BoolToStyle(isActiveENose);
        prbENoseProgress.Visibility = BoolToVisible(isActiveENose);
        prbENoseProgress.Value = 0;
        lblENoseProgress.Content = "";

        btnQuit.IsEnabled = !isActiveENose;

        imgOdorPrinter.Effect = !isActiveOD && !hasNoActiveElement ? _blurEffect : null;
        if (imgSnt.IsVisible)
            imgSnt.Effect = !isActiveENose && !hasNoActiveElement ? _blurEffect : null;
        if (imgDms.IsVisible)
            imgDms.Effect = !isActiveENose && !hasNoActiveElement ? _blurEffect : null;
        imgMLPassive.Effect = !isActiveML && !hasNoActiveElement ? _blurEffect : null;

        var stateText = new List<string>();
        if (hasNoActiveElement)
        {
            stateText.Add("Finished");
        }
        if (isActiveML)
        {
            stateText.Add("Creating a recipe");
        }
        if (isActiveOD)
        {
            stateText.Add("Producing the odor");
        }
        if (isActiveENose)
        {
            stateText.Add("Measuring with eNose");
        }

        tblRecipeState.Text = string.Join(". ", stateText) + ".";
    }

    private void HandleScanFinished(IonVision.Scan.ScanResult scan, Size size)
    {
        new IonVision.Plot().Create(cnvMeasurement,
                    (int)size.Height,
                    (int)size.Width,
                    scan.MeasurementData.IntensityTop,
                    theme: PLOT_THEME);
        DisplayScanInfo();
        DisplayMeasurementEnvInfo(new MeasurementEnvInfo(scan.SystemData.Sample.Temperature.Avg));
    }
    private void HandleScanFinished(IonVision.Defs.ScopeResult scan)
    {
        new IonVision.Plot().Create(cnvMeasurement,
                    1, scan.IntensityTop.Length,
                    scan.IntensityTop,
                    theme: PLOT_THEME);
        DisplayScanInfo();
    }

    private void HandleSntCollected(SmellInsp.Data snt)
    {
        DisplayMeasurementEnvInfo(new MeasurementEnvInfo(snt.Temperature));
    }

    private void HandleRecipe(object? sender, ML.Recipe recipe)
    {
        Dispatcher.Invoke(() =>
        {
            if (_proc == null)
                return;

            if (recipe.RMSE < 10000)
            {
                DisplayScanInfo(recipe.RMSE);    // update the previous scan RMSE
                crtSearchSpace.Add(recipe.RMSE, _proc.RecipeFlows);
            }

            _proc.ExecuteRecipe(recipe);
            DisplayRecipeInfo(recipe);

            SetActiveElement(recipe.IsFinal ? ActiveElement.None : ActiveElement.OdorDisplay);

            if (recipe.IsFinal)
                adaAnimation.Visibility = Visibility.Collapsed;
            else
                adaAnimation.Next();
        });
    }

    private void DisplayScanInfo(float rmse = 0)
    {
        if (_proc == null)
            return;

        var flowsStr = _proc.RecipeFlows.Select(flow => flow.ToString("F1"));
        var id = rmse == 0 ? _proc.CurrentStep : _proc.CurrentStep + 1;     // proc step number not yet increased when the scan just finished
        var info = $"#{id}:   " + string.Join(' ', flowsStr);
        if (rmse > 0)
        {
            info += $", r={rmse:F3}";
        }
        lblMeasurementInfo.Content = info;
    }

    private void DisplayMeasurementEnvInfo(MeasurementEnvInfo info)
    {
        if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_TEMPERATURE_ENOSE, 1) is TextBlock tempEnoseEl)
            tempEnoseEl.Text = info.Temperatore.ToString("0.0") + "°";
    }

    private void DisplayODState(ODPackets.Data data)
    {
        foreach (var m in data.Measurements)
        {
            if (m.Device == ODDevice.ID.Base)
            {
                var value = m.SensorValues.FirstOrDefault(sv => sv.Sensor == ODDevice.Sensor.PID);
                if (value != null && value is ODPackets.PIDValue pid)
                {
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_PID, 1) is TextBlock pidEl)
                        pidEl.Text = (pid.Volts * 1000).ToString("0.0") + " mV";
                }

                value = m.SensorValues.FirstOrDefault(sv => sv.Sensor == ODDevice.Sensor.OutputAirHumiditySensor);
                if (value != null && value is ODPackets.HumidityValue humidity)
                {
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_HUMIDITY, 1) is TextBlock humidityEl)
                        humidityEl.Text = humidity.Percent.ToString("0.0") + " %";
                }

                value = m.SensorValues.FirstOrDefault(sv => sv.Sensor == ODDevice.Sensor.OdorSourceThermometer);
                if (value != null && value is ODPackets.ThermometerValue thermometer)
                {
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_TEMPERATURE_GAS, 1) is TextBlock tempGasEl)
                        tempGasEl.Text = thermometer.Celsius.ToString("0.0") + "°";
                }
            }
            else
            {
                var row = ((int)m.Device - 1) + MEASUREMENT_ROW_FIRST_ODOR_CHANNEL;
                if (GetElementInGrid(grdODChannels, row, 1) is not TextBlock flowEl)
                    continue;

                var gas = GetMeasurement<ODPackets.GasValue>(m, ODDevice.Sensor.OdorantFlowSensor);
                var valve = GetMeasurement<ODPackets.ValveValue>(m, ODDevice.Sensor.OdorantValveSensor);

                if (!(valve?.Opened ?? true))
                {
                    flowEl.Text = "-";
                }
                else if (gas != null)
                {
                    flowEl.Text = (gas.SLPM * 1000).ToString("0.0") + " ml/min";
                }
            }
        }
    }

    private void DisplayRecipeInfo(ML.Recipe recipe)
    {
        if (_proc == null || _procConfig == null)
            return;

        if (!string.IsNullOrEmpty(recipe.Name))
        {
            tblRecipeName.Text = recipe.Name + ":";

            // Dirty hack to get the current search ID from the recipe name
            var re = new Regex(@"#(\d+)");
            var matches = re.Matches(recipe.Name);
            if (matches.Count == 0)
            {
                prbProgress.Value = prbProgress.Maximum;
            }
            else
            {
                int iterationId = matches.Count == 1 ? 0 : int.Parse(matches[0].Captures[0].Value[1..]);
                int searchId = int.Parse(matches[^1].Captures[0].Value[1..]);
                prbProgress.Value = iterationId * 5 + searchId;
            }
        }

        if (!string.IsNullOrEmpty(recipe.Name) && recipe.RMSE >= 0 && recipe.RMSE < 100000)
        {
            crtRMSE.Add(recipe.RMSE);
            crtBestRMSE.Add(_proc.BestRMSE);
        }

        // Clear the table leaving only the header row
        if (recipe.Channels != null)
        {
            var tableElements = new UIElement[grdRecipeChannels.Children.Count];
            grdRecipeChannels.Children.CopyTo(tableElements, 0);
            foreach (var el in tableElements)
            {
                if (Grid.GetRow(el) > 0)
                {
                    grdRecipeChannels.Children.Remove(el);
                }
            }

            int rowIndex = 1;
            foreach (var channel in recipe.Channels)
            {
                var id = (ODDevice.ID)channel.Id;
                var label = _proc.OdorChannels.FirstOrDefault(odorChannel => odorChannel.ID == id)?.Name ?? id.ToString();
                AddLabel(grdRecipeChannels, label, rowIndex, 0, _recipeLabelStyle);

                AddTextBlock(grdRecipeChannels, channel.Flow >= 0 ? channel.Flow.ToString("0.#") : "-", rowIndex, 1, _recipeValueStyle);
                AddTextBlock(grdRecipeChannels, _proc.BestFlows[rowIndex - 1].ToString("0.#") ?? "-", rowIndex, 2, _recipeValueStyle);
                AddTextBlock(grdRecipeChannels, _procConfig.TargetFlows.FirstOrDefault(channelConfig => channelConfig.ID == id) is
                    OdorReproducerController.OdorChannelConfig targetFlow ? targetFlow.Flow.ToString("0.#") : "-", rowIndex, 4, _recipeValueStyle);

                rowIndex++;
            }
        }

        // Add RMSE info
        var settings = Properties.Settings.Default;
        var lastRowId = grdRecipeChannels.RowDefinitions.Count - 1;
        AddLabel(grdRecipeChannels, "RMSE", lastRowId, 0, _recipeLabelStyle);
        if (_proc.BestRMSE < 10000)
            AddTextBlock(grdRecipeChannels, _proc.BestRMSE.ToString("0.###"), lastRowId, 2, _recipeValueStyle);
        AddTextBlock(grdRecipeChannels, settings.Reproduction_ML_Threshold.ToString("0.###"), lastRowId, 4, _recipeValueStyle);

        btnQuit.Content = recipe.IsFinal ? "Return" : "Interrupt";
    }

    private void SetConnectionColor(ConnectionCircle ccl, bool isConnected) => Dispatcher.Invoke(() => ccl.IsConnected = isConnected);

    private void CleanUp()
    {
        _proc?.ShutDownFlows();
        _proc?.CleanUp();

        if (_procConfig != null)
        {
            _procConfig.MLComm.RecipeReceived -= HandleRecipe;
        }

        _procConfig = null;
    }

    private static void AddTextBlock(Grid grid, string text, int row, int column, Style? style)
    {
        var tbl = new TextBlock()
        {
            Text = text,
            Style = style,
        };
        Grid.SetRow(tbl, row);
        Grid.SetColumn(tbl, column);
        grid.Children.Add(tbl);
    }

    private static void AddLabel(Grid grid, string text, int row, int column, Style? style)
    {
        var lbl = new Label()
        {
            Content = text,
            Style = style,
        };
        Grid.SetRow(lbl, row);
        Grid.SetColumn(lbl, column);
        grid.Children.Add(lbl);
    }

    // UI

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

        adaAnimation.Init();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .UnbindScaleToZoomLevel(sctScale)
            .UnbindContentToZoomLevel(lblZoom)
            .UnbindVisibilityToDebug(lblDebug);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        CleanUp();
        Next?.Invoke(this, Storage.Instance.SetupPage);
    }
}
