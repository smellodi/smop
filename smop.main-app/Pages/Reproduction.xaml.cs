using Smop.Common;
using Smop.MainApp.Controllers;
using Smop.MainApp.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MOD = Smop.OdorDisplay.Device;
using ODPackets = Smop.OdorDisplay.Packets;

namespace Smop.MainApp.Pages;

public partial class Reproduction : Page, IPage<Navigation>
{
    public event EventHandler<Navigation>? Next;

    public Reproduction()
    {
        InitializeComponent();

        Name = "OdorReproduction";

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
        config.MLComm.StatusChanged += (s, e) => SetConnectionColor(cclMLStatus, e == ML.Status.Activated);

        adaAnimation.Init();

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
        prbProgress.Maximum = (Properties.Settings.Default.Reproduction_ML_MaxIterations + 1) * config.TrialsPerIteration;

        crtTrialDistance.Reset();
        crtBestDistance.Reset();
        cnvMeasurement.Children.Clear();

        // Limitation: only two odors
        crtSearchSpace.Reset();
        crtSearchSpace.DistanceThreshold = 3 * Properties.Settings.Default.Reproduction_ML_Threshold;
        crtSearchSpace.Add(0, config.TargetFlows.Select(tf => tf.Flow).ToArray(), System.Drawing.Color.Black);

        tblOdor1.Text = _proc.OdorChannels.Length > 0 ? _proc.OdorChannels[0].Name : "";
        tblOdor2.Text = _proc.OdorChannels.Length > 1 ? _proc.OdorChannels[1].Name : "";

        grdSearchSpace.Visibility = config.TargetFlows.Length == 2 ? Visibility.Visible : Visibility.Collapsed;
        scvSearchSpace.Visibility = config.TargetFlows.Length != 2 ? Visibility.Visible : Visibility.Collapsed;

        grdSearchSpaceTable.Children.Clear();

        if (config.TargetFlows.Length != 2)
        {
            grdSearchSpaceTable.RowDefinitions.Clear();
            grdSearchSpaceTable.ColumnDefinitions.Clear();

            var channelNames = _proc.OdorChannels
                .Where(ch => !string.IsNullOrEmpty(ch.Name))
                .Select(ch => ch.Name)
                .ToArray();
            for (int i = 0; i < channelNames.Length + 1; i++)     // 1 extra column for the distance
                grdSearchSpaceTable.ColumnDefinitions.Add(new ColumnDefinition() { MaxWidth = 46 });

            AddFlowsRecordToSearchSpaceTable(channelNames, "Dist");
        }

        DispatchOnce.Do(0.4, () => Dispatcher.Invoke(() =>
        {
            _plotScale = config.TargetMeasurement switch
            {
                IonVision.Defs.ScanResult dms => new Plot().Create(cnvTargetMeasurement,
                    (int)config.DataSize.Height,
                    (int)config.DataSize.Width,
                    dms.MeasurementData.IntensityTop,
                    theme: PLOT_THEME),
                IonVision.Defs.ScopeResult dmsSingleSV => new Plot().Create(cnvTargetMeasurement,
                    1, dmsSingleSV.IntensityTop.Length,
                    dmsSingleSV.IntensityTop,
                    theme: PLOT_THEME),
                SmellInsp.Data snt => new Plot().Create(cnvTargetMeasurement,
                    1, snt.Resistances.Length,
                    snt.Resistances,
                    theme: PLOT_THEME),
                _ => 0
            };
        }));

        var odorChannels = _proc.OdorChannels;
        ConfigureChannelTable(odorChannels, grdODChannels, _odChannelLabelStyle, _odChannelStyle, MEASUREMENT_ROW_FIRST_ODOR_CHANNEL);
        ConfigureChannelTable(odorChannels, grdRecipeChannels, _recipeLabelStyle, _recipeValueStyle, 1);

        DisplayRecipeInfo(new ML.Recipe("", false, 0, odorChannels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name))
            .Select(odorChannel => new ML.ChannelRecipe((int)odorChannel.ID, -1, -1))
            .ToArray()
        ));

        SetActiveElement(ActiveElement.ML);
        SetConnectionColor(cclMLStatus, config.MLComm.IsConnected);
        SetConnectionColor(cclODStatus, OdorDisplay.CommPort.Instance.IsOpen);

        _procConfig.MLComm.Start();
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

    readonly Brush BRUSH_DARK = (Brush)Application.Current.FindResource("BrushPanelLight");
    readonly Brush BRUSH_LIGHT = (Brush)Application.Current.FindResource("BrushPanelLightest");
    readonly Brush FONT_DARK = (Brush)Application.Current.FindResource("BrushFontDark");
    readonly Brush FONT_LIGHT = (Brush)Application.Current.FindResource("BrushFont");

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

    float _plotScale = 0;

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

    private static T? GetMeasurement<T>(ODPackets.Sensors measurement, MOD.Sensor sensor)
        where T : ODPackets.Sensor.Value
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
            if (!string.IsNullOrEmpty(odorChannel.Name))
            {
                grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                AddLabel(grid, odorChannel.Name, grid.RowDefinitions.Count - 1, 0, labelStyle, odorChannel.ID);
                AddTextBlock(grid, "", grid.RowDefinitions.Count - 1, 1, valueStyle);
            }
        }

        // Distance row
        grid.RowDefinitions.Add(new RowDefinition() { MinHeight = 36 });
    }

    private void AddFlowsRecordToSearchSpaceTable(string[] flows, string distance)
    {
        void AddValue(string value, int column)
        {
            var tbl = new TextBlock() { Text = value, Padding = new Thickness(2) };
            if (grdSearchSpaceTable.RowDefinitions.Count == 1)
            {
                tbl.FontWeight = FontWeights.Bold;
            }
            tbl.Background = (grdSearchSpaceTable.RowDefinitions.Count % 2) == 0 ? BRUSH_DARK : BRUSH_LIGHT;
            tbl.Foreground = /*(grdSearchSpaceTable.RowDefinitions.Count % 2) == 0 ? FONT_LIGHT : */FONT_DARK;
            Grid.SetColumn(tbl, column);
            Grid.SetRow(tbl, grdSearchSpaceTable.RowDefinitions.Count - 1);
            grdSearchSpaceTable.Children.Add(tbl);
        }

        if (flows.Length == 0)   // only the distance was updated
        {
            return;
        }

        grdSearchSpaceTable.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

        for (int i = 0; i < flows.Length; i++)
        {
            AddValue(flows[i], i);
        }

        AddValue(distance, flows.Length);

        scvSearchSpace.ScrollToBottom();
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

        imgTubeFull.Visibility = BoolToVisible(isActiveOD || hasNoActiveElement);
        imgTubeEmpty.Visibility = BoolToVisible(!isActiveOD && !hasNoActiveElement);

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

    private void HandleScanFinished(IonVision.Defs.ScanResult scan, Size size)
    {
        new Plot().Create(cnvMeasurement,
                    (int)size.Height,
                    (int)size.Width,
                    scan.MeasurementData.IntensityTop,
                    theme: PLOT_THEME,
                    scale: _plotScale);
        DisplayMeasurementInfo();
        DisplayMeasurementEnvInfo(new MeasurementEnvInfo(scan.SystemData.Sample.Temperature.Avg));
    }

    private void HandleScanFinished(IonVision.Defs.ScopeResult scan)
    {
        new Plot().Create(cnvMeasurement,
                    1, scan.IntensityTop.Length,
                    scan.IntensityTop,
                    theme: PLOT_THEME,
                    scale: _plotScale);
        DisplayMeasurementInfo();
    }

    private void HandleSntCollected(SmellInsp.Data snt)
    {
        new Plot().Create(cnvMeasurement,
                    1, snt.Resistances.Length,
                    snt.Resistances,
                    theme: PLOT_THEME,
                    scale: _plotScale);
        DisplayMeasurementInfo();
        DisplayMeasurementEnvInfo(new MeasurementEnvInfo(snt.Temperature));
    }

    private void HandleRecipe(object? sender, ML.Recipe recipe)
    {
        Dispatcher.Invoke(async () =>
        {
            if (_proc == null)
                return;

            if (_proc.CurrentStep == 0)
            {
                await Task.Delay(1000); // Wait for the first recipe to be executed after the animation initialization is finished
            }

            if (recipe.Distance < 10000)
            {
                DisplayMeasurementInfo(recipe.Distance);    // update the previous scan distance from the target scan
                if (grdSearchSpace.Visibility == Visibility.Visible)
                {
                    crtSearchSpace.Add(recipe.Distance, _proc.RecipeFlows);
                }
                else
                {
                    string[] flows = recipe.IsFinal ? Array.Empty<string>() : _proc.RecipeFlows.Select(f => f.ToString("F1")).ToArray();
                    AddFlowsRecordToSearchSpaceTable(flows, recipe.Distance.ToString("F2"));
                }
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

    private void DisplayMeasurementInfo(float distance = 0)
    {
        if (_proc == null)
            return;

        var flowsStr = _proc.RecipeFlows.Select(flow => flow.ToString("F1"));
        var info = $"#{_proc.CurrentStep}:   " + string.Join(' ', flowsStr);
        if (distance > 0)
        {
            info += $", r={distance:F3}";
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
            if (m.Device == MOD.ID.Base)
            {
                var value = m.SensorValues.FirstOrDefault(sv => sv.Sensor == MOD.Sensor.PID);
                if (value != null && value is ODPackets.Sensor.PID pid)
                {
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_PID, 1) is TextBlock pidEl)
                        pidEl.Text = (pid.Volts * 1000).ToString("0.0") + " mV";
                }

                value = m.SensorValues.FirstOrDefault(sv => sv.Sensor == MOD.Sensor.OutputAirHumiditySensor);
                if (value != null && value is ODPackets.Sensor.Humidity humidity)
                {
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_HUMIDITY, 1) is TextBlock humidityEl)
                        humidityEl.Text = humidity.Percent.ToString("0.0") + " %";
                }

                value = m.SensorValues.FirstOrDefault(sv => sv.Sensor == MOD.Sensor.OdorSourceThermometer);
                if (value != null && value is ODPackets.Sensor.Thermometer thermometer)
                {
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_TEMPERATURE_GAS, 1) is TextBlock tempGasEl)
                        tempGasEl.Text = thermometer.Celsius.ToString("0.0") + "°";
                }
            }
            else
            {
                int row = -1;
                for (int i = MEASUREMENT_ROW_FIRST_ODOR_CHANNEL; i < grdODChannels.RowDefinitions.Count; i++)
                {
                    if (GetElementInGrid(grdODChannels, i, 0) is Label odorNameEl)
                    {
                        if ((MOD.ID)odorNameEl.Tag == m.Device)
                        {
                            row = i;
                            break;
                        }
                    }
                }

                if (row < 0 || GetElementInGrid(grdODChannels, row, 1) is not TextBlock flowEl)
                    continue;

                var gas = GetMeasurement<ODPackets.Sensor.Gas>(m, MOD.Sensor.OdorantFlowSensor);
                var valve = GetMeasurement<ODPackets.Sensor.Valve>(m, MOD.Sensor.OdorantValveSensor);

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
                prbProgress.Value = iterationId * _procConfig.TrialsPerIteration + searchId;
            }
        }

        if (!string.IsNullOrEmpty(recipe.Name) && recipe.Distance >= 0 && recipe.Distance < 100000)
        {
            crtTrialDistance.Add(recipe.Distance);
            crtBestDistance.Add(_proc.BestDistance);
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
                var id = (MOD.ID)channel.Id;
                var label = _proc.OdorChannels.FirstOrDefault(odorChannel => odorChannel.ID == id)?.Name ?? id.ToString();
                AddLabel(grdRecipeChannels, label, rowIndex, 0, _recipeLabelStyle);

                AddTextBlock(grdRecipeChannels, channel.Flow >= 0 ? channel.Flow.ToString("0.#") : "-", rowIndex, 1, _recipeValueStyle);
                AddTextBlock(grdRecipeChannels, _proc.BestFlows[rowIndex - 1].ToString("0.#") ?? "-", rowIndex, 2, _recipeValueStyle);
                AddTextBlock(grdRecipeChannels, _procConfig.TargetFlows.FirstOrDefault(channelConfig => channelConfig.ID == id) is
                    OdorReproducerController.OdorChannelConfig targetFlow ? targetFlow.Flow.ToString("0.#") : "-", rowIndex, 4, _recipeValueStyle);

                rowIndex++;
            }
        }

        // Add distance info
        var settings = Properties.Settings.Default;
        var lastRowId = grdRecipeChannels.RowDefinitions.Count - 1;
        AddLabel(grdRecipeChannels, "Distance", lastRowId, 0, _recipeLabelStyle);
        if (_proc.BestDistance < 10000)
            AddTextBlock(grdRecipeChannels, _proc.BestDistance.ToString("0.###"), lastRowId, 2, _recipeValueStyle);
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
            _procConfig.MLComm.CleanUp();
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

    private static void AddLabel(Grid grid, string text, int row, int column, Style? style, object? tag = null)
    {
        var lbl = new Label()
        {
            Content = text,
            Style = style,
            Tag = tag
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
