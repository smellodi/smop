using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ODPackets = Smop.OdorDisplay.Packets;
using ODDevice = Smop.OdorDisplay.Device;

namespace Smop.MainApp.Pages;

public partial class Reproduction : Page, IPage<Navigation>
{
    public event EventHandler<Navigation>? Next;

    public Reproduction()
    {
        InitializeComponent();

        _activeElementStyle = FindResource("ActiveElement") as Style;
        _inactiveElementStyle = FindResource("Element") as Style;
        _recipeChannelStyle = FindResource("RecipeChannel") as Style;
        _recipeChannelLabelStyle = FindResource("RecipeChannelLabel") as Style;
        _odChannelStyle = FindResource("OdorDisplayMeasurement") as Style;
        _odChannelLabelStyle = FindResource("OdorDisplayMeasurementLabel") as Style;

        Application.Current.Exit += (s, e) => CleanUp();
    }

    public void Start(ML.Communicator ml)
    {
        _proc = new Reproducer.Procedure(ml);
        _proc.MlComputationStarted += (s, e) => Dispatcher.Invoke(() => SetActiveElement(ActiveElement.ML));
        _proc.ENoseStarted += (s, e) => Dispatcher.Invoke(() => SetActiveElement(ActiveElement.OdorDisplay | ActiveElement.ENose));
        _proc.ENoseProgressChanged += (s, e) => Dispatcher.Invoke(() => prbENoseProgress.Value = e);
        _proc.OdorDisplayData += (s, e) => Dispatcher.Invoke(() => DisplayODState(e));

        _ml = ml;
        _ml.RecipeReceived += HandleRecipe;

        imgDms.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;
        imgSnt.Visibility = App.IonVision == null && SmellInsp.CommPort.Instance.IsOpen ? Visibility.Visible : Visibility.Collapsed;

        tblRecipeName.Text = "";
        tblRecipeRMSQ.Text = "";

        ConfigureChannelTable(grdODChannels, _odChannelLabelStyle, _odChannelStyle);
        ConfigureChannelTable(grdRecipeChannels, _recipeChannelLabelStyle, _recipeChannelStyle);

        DisplayRecipeInfo(new ML.Recipe("", 0, 0, _proc.Gases.Select(gas => new ML.ChannelRecipe((int)gas.ChannelID, -1, -1)).ToArray()));

        SetActiveElement(ActiveElement.ML);
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

    Reproducer.Procedure? _proc;
    ML.Communicator? _ml = null;

    ActiveElement _activeElement = ActiveElement.None;

    readonly Style? _activeElementStyle;
    readonly Style? _inactiveElementStyle;
    readonly Style? _recipeChannelStyle;
    readonly Style? _recipeChannelLabelStyle;
    readonly Style? _odChannelStyle;
    readonly Style? _odChannelLabelStyle;

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

    private void ConfigureChannelTable(Grid grid, Style? labelStyle, Style? valueStyle)
    {
        var gases = _proc?.Gases;
        if (gases == null)
            return;

        foreach (var gas in gases)
        {
            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            var lbl = new Label()
            {
                Content = gas.Name,
                Style = labelStyle,
            };
            Grid.SetRow(lbl, grid.RowDefinitions.Count - 1);
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var tbl = new TextBlock()
            {
                Style = valueStyle,
            };
            Grid.SetRow(tbl, grid.RowDefinitions.Count - 1);
            Grid.SetColumn(tbl, 1);
            grid.Children.Add(tbl);
        }
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

        tblRecipeName.Visibility = BoolToVisible(!isActiveML);
        grdRecipeChannels.Visibility = BoolToVisible(!isActiveML);
        tblRecipeRMSQ.Visibility = BoolToVisible(isActiveOD || hasNoActiveElement);

        if (hasNoActiveElement)
        {
            tblRecipeState.Text = "Finished";
            tblRecipeName.Text = "";
        }
        else if (isActiveML)
        {
            tblRecipeState.Text = "Creating a recipe";
        }
        else if (isActiveOD)
        {
            tblRecipeState.Text = "Mixing the chemicals to produce the odor";
        }
        else if (isActiveENose)
        {
            tblRecipeState.Text = "Sniffing the produced odor with eNose";
        }
    }

    private void HandleRecipe(object? sender, ML.Recipe recipe)
    {
        Dispatcher.Invoke(() =>
        {
            DisplayRecipeInfo(recipe);
            _proc?.ExecuteRecipe(recipe);

            SetActiveElement(recipe.Finished ? ActiveElement.None : ActiveElement.OdorDisplay);
        });
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
                    if (GetElementInGrid(grdODChannels, 0, 1) is TextBlock pidEl)
                        pidEl.Text = (pid.Volts * 1000).ToString("0.0") + " mV";
                }
            }
            else
            {
                var row = (int)m.Device;
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
        if (!string.IsNullOrEmpty(recipe.Name))
        {
            tblRecipeName.Text = recipe.Name;
        }
        tblRecipeState.Text = recipe.Finished ? $"Finished in {_proc?.CurrentStep} steps" : $"In progress (step #{_proc?.CurrentStep})";
        tblRecipeRMSQ.Text = "r = " + recipe.MinRMSE.ToString("0.####");

        grdRecipeChannels.Children.Clear();
        if (recipe.Channels != null)
        {
            int rowIndex = 0;
            foreach (var channel in recipe.Channels)
            {
                var id = (ODDevice.ID)channel.Id;
                var lbl = new Label()
                {
                    Content = _proc?.Gases.FirstOrDefault(gas => gas.ChannelID == id)?.Name ?? id.ToString(),
                    Style = _recipeChannelLabelStyle,
                };
                Grid.SetRow(lbl, rowIndex);
                Grid.SetColumn(lbl, 0);
                grdRecipeChannels.Children.Add(lbl);

                var value = new List<string>();
                if (channel.Flow >= 0)
                {
                    value.Add($"{channel.Flow} ml/min");
                }
                if (channel.Duration > 0)
                {
                    value.Add($"{channel.Duration:F2} sec.");
                }
                if (channel.Temperature != null)
                {
                    value.Add($"{channel.Temperature:F1}°");
                }

                if (value.Count > 0)
                {
                    var tbl = new TextBlock()
                    {
                        Text = string.Join(", ", value),
                        Style = _recipeChannelStyle,
                    };
                    Grid.SetRow(tbl, rowIndex);
                    Grid.SetColumn(tbl, 1);
                    grdRecipeChannels.Children.Add(tbl);
                }

                rowIndex++;
            }
        }

        btnQuit.Content = recipe.Finished ? "Return" : "Interrupt";
    }

    private void CleanUp()
    {
        _proc?.ShutDownFlows();

        if (_ml != null)
        {
            _ml.RecipeReceived -= HandleRecipe;
        }

        _ml = null;
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
