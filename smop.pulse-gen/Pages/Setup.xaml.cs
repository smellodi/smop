using Microsoft.Win32;
using Smop.OdorDisplay.Packets;
using Smop.PulseGen.Logging;
using Smop.PulseGen.Test;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static Smop.OdorDisplay.Device;

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

    readonly Storage _storage = Storage.Instance;

	readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

	readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    readonly SmellInspLogger _smellInspLogger = SmellInspLogger.Instance;

    readonly Dictionary<string, Controls.ChannelIndicator> _indicators = new();

	bool _isInitilized = false;
    bool _ionVisionIsReady = false;
    string? _setupFileName = null;
    PulseSetup? _setup = null;

    Controls.ChannelIndicator? _currentIndicator = null;
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

	private void ResetGraph(string? dataSource = null, double baseValue = .0)
	{
		if (_currentIndicator?.Source == dataSource || dataSource == null)
		{
			var interval = (double)(_storage.IsDebugging ? OdorDisplay.SerialPortEmulator.SamplingFrequency : OdorDisplay.Device.DataMeasurementInterval) / 1000;
			lmsGraph.Reset(interval, baseValue);
		}
	}

    private async Task CreateIndicators()
    {
        var indicatorGenerator = new IndicatorGenerator();
        await indicatorGenerator.OdorDisplay(indicator => Dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpOdorDisplayIndicators.Children.Add(indicator);
			_indicators.Add(indicator.Source, indicator);
        }));

        IndicatorGenerator.SmellInsp(indicator => Dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpSmellInspIndicators.Children.Add(indicator);
            _indicators.Add(indicator.Source, indicator);
        }));

        (stpSmellInspIndicators.Children[0] as Controls.ChannelIndicator)!.ChannelIdChanged += (s, e) =>
        {
            _smellInspResistor = e;
            ResetGraph();
        };
    }

    private void UpdateIndicators(Data data)
	{
		foreach (var m in data.Measurements)
		{
			foreach (var sv in m.SensorValues)
			{
                var value = sv switch
                {
                    PIDValue pid => pid.Volts,
                    ThermometerValue temp => temp.Celsius,
                    BeadThermistorValue beadTemp => beadTemp.Ohms,  // beadTemp.Volts
                    HumidityValue humidity => humidity.Percent,     // humidity.Celsius
                    PressureValue pressure => pressure.Millibars,   // pressure.Celsius
                    GasValue gas => gas.SLPM,                       // gas.Millibars, gas.Celsius
                    ValveValue valve => valve.Opened ? 1 : 0,
                    _ => 0
                };
                
				var source = IndicatorGenerator.GetSourceId(m.Device, (Smop.OdorDisplay.Device.Capability)sv.Sensor);
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
        UpdateUI();

        var settings = Properties.Settings.Default;
        settings.Pulses_SetupFilename = _setupFileName;
        settings.Save();
    }

    private void UpdateUI()
    {
        btnStart.IsEnabled = _setupFileName != null && _ionVisionIsReady;
    }

    private async Task InitializeIonVision(IonVision.Communicator ionVision)
    {
        await ionVision.SetClock();

        await Task.Delay(100);
        List<string> completedSteps = new() { "Initialized", $"Loading '{ionVision.Settings.Project}' project..." };
        lblDmsStatus.Content = string.Join('\n', completedSteps);

        await ionVision.SetProjectAndWait();
        await Task.Delay(15000);
        completedSteps.RemoveAt(completedSteps.Count - 1);
        completedSteps.Add($"Project '{ionVision.Settings.Project}' is loaded");
        completedSteps.Add($"Loading '{ionVision.Settings.ParameterName}' parameter...");
        lblDmsStatus.Content = string.Join('\n', completedSteps);

        await ionVision.SetParameterAndPreload();
        await Task.Delay(500);
        completedSteps.RemoveAt(completedSteps.Count - 1);
        completedSteps.Add($"Parameter '{ionVision.Settings.ParameterName}' is loaded");
        lblDmsStatus.Content = string.Join('\n', completedSteps);

        await Task.Delay(500);
        completedSteps.Add("Done!");
        lblDmsStatus.Content = string.Join('\n', completedSteps);

        _ionVisionIsReady = true;
        UpdateUI();
    }

    private void HandleOdorDisplayError(OdorDisplay.Result odorDisplayResult, string action)
    {
        if (odorDisplayResult.Error != OdorDisplay.Error.Success)
        {
            Utils.MsgBox.Error(Title, $"Odor Display: Cannot {action}:\n{odorDisplayResult.Reason}");
        }
    }

    private void Close()
    {
        _odorDisplay.Data -= OdorDisplay_Data;
        _smellInsp.Data -= SmellInsp_Data;

        var queryMeasurements = new SetMeasurements(SetMeasurements.Command.Stop);
        _odorDisplay.Request(queryMeasurements, out _, out Response? _);
    }

    /*
	private void HandleError(Result result)
	{
		if (result.Error == Error.Success)
		{
			return; // no error
		}
		else if (result.Error == Error.CRC)
		{
			return; // these are not critical errors, just continue
		}
		else if (result.Error.HasFlag(Error.DeviceError))
		{
			//var deviceError = (OdorDisplay.Packets.Packet.Result)((int)result.Error & ~(int)Error.DeviceError);
			Utils.MsgBox.Error(Title, $"Device error:\n{result.Reason}");
			return; // hopefully, these are not critical errors, just continue
		}

		Utils.MsgBox.Error(Title, $"Critical error:\n{result}\n\nApplication terminated.");
		Application.Current.Shutdown();
	}*/


    // Event handlers

    private async void OdorDisplay_Data(object? sender, Data data)
    {
		try
		{
			await Task.Run(() => Dispatcher.Invoke(() =>
			{
				UpdateIndicators(data);
				_odorDisplayLogger.Add(data);
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
                _smellInspLogger.Add(data);
            }));
        }
        catch (TaskCanceledException) { }
    }

    // UI events

    private async void Page_Loaded(object? sender, RoutedEventArgs e)
	{
		_storage
			.BindScaleToZoomLevel(sctScale)
			.BindVisibilityToDebug(lblDebug);

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

        _odorDisplay.Data += OdorDisplay_Data;
        _smellInsp.Data += SmellInsp_Data;

        tabSmellInsp.IsEnabled = _smellInsp.IsOpen;

        await CreateIndicators();

        var odController = new OdorDisplayController();
        HandleOdorDisplayError(odController.Init(), "initilize");

        System.Threading.Thread.Sleep(100);
        HandleOdorDisplayError(odController.Start(), "start measurements");

        if (App.IonVision != null)
        {
            await InitializeIonVision(App.IonVision);
        }
    }

    private void Page_Unloaded(object? sender, RoutedEventArgs e)
	{
		_storage
            .UnbindScaleToZoomLevel(sctScale)
			.UnbindVisibilityToDebug(lblDebug);
	}

	private void Page_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.F4 && _setup != null)
		{
			Next?.Invoke(this, _setup);
		}
	}

	private void FreshAir_KeyUp(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.F2 && txbFreshAir.IsReadOnly)
		{
			txbFreshAir.IsReadOnly = false;
		}
		else if (e.Key == Key.Enter)
		{
			/*
			if (Utils.Validation.Do(txbFreshAir, 0, 10, (object? s, double value) => _mfc.FreshAirSpeed = value))
			{
				txbFreshAir.MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
			}
			else
			{
				txbFreshAir.Undo();
			}*/
		}
	}

	private void ChannelIndicator_MouseDown(object? sender, MouseButtonEventArgs e)
	{
		var chi = sender as Controls.ChannelIndicator;
		if (!chi?.IsActive ?? false)
		{
			if (_currentIndicator != null)
			{
				_currentIndicator.IsActive = false;
			}

			_currentIndicator = chi;
			_currentIndicator!.IsActive = true;

            ResetGraph();
        }
	}

    private void ChoosePulseSetupFile_Click(object? sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;

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
}
