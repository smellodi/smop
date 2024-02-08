﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ODPackets = Smop.OdorDisplay.Packets;
using ODDevice = Smop.OdorDisplay.Device;
using System.Windows.Shapes;

namespace Smop.MainApp.Pages;

public partial class Reproduction : Page, IPage<Navigation>
{
    public event EventHandler<Navigation>? Next;

    public Reproduction()
    {
        InitializeComponent();

        _inactiveElementStyle = FindResource("Element") as Style;
        _activeElementStyle = FindResource("ActiveElement") as Style;
        _recipeChannelStyle = FindResource("RecipeChannel") as Style;
        _recipeChannelLabelStyle = FindResource("RecipeChannelLabel") as Style;
        _odChannelStyle = FindResource("OdorDisplayMeasurement") as Style;
        _odChannelLabelStyle = FindResource("OdorDisplayMeasurementLabel") as Style;

        ((App)Application.Current).AddCleanupAction(CleanUp);
        OdorDisplay.CommPort.Instance.Closed += (s, e) => SetConnectionColor(elpODStatus, false);
    }

    public void Start(Reproducer.Procedure.Config config)
    {
        config.MLComm.StatusChanged += (s, e) => SetConnectionColor(elpMLStatus, e == ML.Status.Connected);

        _proc = new Reproducer.Procedure(config);
        _proc.ScanFinished += (s, e) => Dispatcher.Invoke(() => IonVision.DataPlot.Create(cnvDmsScan,
            (int)config.DataSize.Height, (int)config.DataSize.Width, e.MeasurementData.IntensityTop));
        _proc.MlComputationStarted += (s, e) => Dispatcher.Invoke(() => SetActiveElement(ActiveElement.ML));
        _proc.ENoseStarted += (s, e) => Dispatcher.Invoke(() => SetActiveElement(ActiveElement.OdorDisplay | ActiveElement.ENose));
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
        tblRecipeRMSQ.Text = "";
        tblRecipeIteration.Text = "";

        crtRMSQ.Reset();

        var gases = _proc.Gases;
        ConfigureChannelTable(gases, grdODChannels, _odChannelLabelStyle, _odChannelStyle, MEASUREMENT_ROW_FIRST_GAS);
        ConfigureChannelTable(gases, grdRecipeChannels, _recipeChannelLabelStyle, _recipeChannelStyle, 1);

        DisplayRecipeInfo(new ML.Recipe("", 0, 0, gases.Select(gas => new ML.ChannelRecipe((int)gas.ChannelID, -1, -1)).ToArray()));

        SetActiveElement(ActiveElement.ML);
        SetConnectionColor(elpMLStatus, config.MLComm.IsConnected);
        SetConnectionColor(elpODStatus, OdorDisplay.CommPort.Instance.IsOpen);
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

    const int MEASUREMENT_ROW_PID = 0;
    const int MEASUREMENT_ROW_HUMIDITY = 1;
    const int MEASUREMENT_ROW_FIRST_GAS = 2;

    readonly Brush BRUSH_STATUS_CONNECTED = new SolidColorBrush(Color.FromRgb(32, 160, 32));
    readonly Brush BRUSH_STATUS_DISCONNECTED = new SolidColorBrush(Color.FromRgb(128, 160, 32));

    readonly Style? _activeElementStyle;
    readonly Style? _inactiveElementStyle;
    readonly Style? _recipeChannelStyle;
    readonly Style? _recipeChannelLabelStyle;
    readonly Style? _odChannelStyle;
    readonly Style? _odChannelLabelStyle;

    Reproducer.Procedure? _proc;
    Reproducer.Procedure.Config? _procConfig = null;

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

    private static void ConfigureChannelTable(Reproducer.Gas[] gases, Grid grid, Style? labelStyle, Style? valueStyle, int constantRowCount = 0)
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
        lblENoseProgress.Content = "";

        tblRecipeName.Visibility = BoolToVisible(!isActiveML);
        grdRecipeChannels.Visibility = BoolToVisible(!isActiveML);
        tblRecipeRMSQ.Visibility = BoolToVisible(isActiveOD || hasNoActiveElement);
        tblRecipeIteration.Visibility = BoolToVisible(isActiveOD || hasNoActiveElement);

        cnvDmsScan.Visibility = BoolToVisible(!isActiveENose && App.IonVision != null);

        btnQuit.IsEnabled = !isActiveENose;

        var stateText = new List<string>();
        if (hasNoActiveElement)
        {
            tblRecipeName.Text = "Final recipe:";
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
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_PID, 1) is TextBlock pidEl)
                        pidEl.Text = (pid.Volts * 1000).ToString("0.0") + " mV";
                }

                value = m.SensorValues.FirstOrDefault(sv => sv.Sensor == ODDevice.Sensor.OutputAirHumiditySensor);
                if (value != null && value is ODPackets.HumidityValue humidity)
                {
                    if (GetElementInGrid(grdODChannels, MEASUREMENT_ROW_HUMIDITY, 1) is TextBlock humidityEl)
                        humidityEl.Text = humidity.Percent.ToString("0.0") + " %";
                }
            }
            else
            {
                var row = ((int)m.Device - 1) + MEASUREMENT_ROW_FIRST_GAS;
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
            tblRecipeName.Text = recipe.Name + ":";
        }
        tblRecipeRMSQ.Text = "r = " + recipe.MinRMSE.ToString("0.####");
        tblRecipeIteration.Text = $"iteration #{_proc?.CurrentStep + 1}";

        if (!string.IsNullOrEmpty(recipe.Name))
        {
            crtRMSQ.Add(recipe.MinRMSE);
        }

        // Clear the table leaving only the header row
        var tableElements = new UIElement[grdRecipeChannels.Children.Count];
        grdRecipeChannels.Children.CopyTo(tableElements, 0);
        foreach (var el in tableElements)
        {
            if (Grid.GetRow(el) > 0)
            {
                grdRecipeChannels.Children.Remove(el);
            }
        }

        if (recipe.Channels != null)
        {
            int rowIndex = 1;
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

                AddToTable(channel.Flow >= 0 ? channel.Flow.ToString("0.#") : "-", rowIndex, 1);
                AddToTable(channel.Duration >= 0 ? channel.Duration.ToString("0.##") : "-", rowIndex, 2);
                AddToTable(channel.Temperature > 0 ? channel.Duration.ToString("0.##") : "-", rowIndex, 3);
                AddToTable(_procConfig?.TargetFlows.FirstOrDefault(gasFlow => gasFlow.ID == id) is 
                    Reproducer.Procedure.GasFlow targetFlow ? targetFlow.Flow.ToString("0.#") : "-", rowIndex, 4);

                rowIndex++;
            }
        }

        btnQuit.Content = recipe.Finished ? "Return" : "Interrupt";
    }

    private void SetConnectionColor(Ellipse elp, bool isConnected) => Dispatcher.Invoke(() => elp.Fill = isConnected ?
                    BRUSH_STATUS_CONNECTED :
                    BRUSH_STATUS_DISCONNECTED);

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

    void AddToTable(string text, int row, int column)
    {
        var tbl = new TextBlock()
        {
            Text = text,
            Style = _recipeChannelStyle,
        };
        Grid.SetRow(tbl, row);
        Grid.SetColumn(tbl, column);
        grdRecipeChannels.Children.Add(tbl);
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
