using Microsoft.Win32;
using Smop.OdorDisplay.Packets;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Smop.PulseGen.Pages;

public partial class Setup : Page, IPage<EventArgs>
{
	public event EventHandler<EventArgs>? Next;

	public Setup()
	{
		InitializeComponent();

		DataContext = this;
	}


	// Internal

	readonly Storage _storage = Storage.Instance;

	readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

	bool _isInitilized = false;
	string? _pulseSetupFileName = null;

    Controls.ChannelIndicator? _currentIndicator = null;

	private void ClearIndicators()
	{
		foreach (Controls.ChannelIndicator chi in stpIndicators.Children)
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

	private void UpdateIndicators(Data data)
	{
		if (_currentIndicator == null)
		{
			return;
		}

		foreach (var m in data.Measurements)
		{
			foreach (var sv in m.SensorValues)
			{
				var source = $"{m.Device}\n{sv.Sensor}";
				if (_currentIndicator.Source == source)
				{
					var value = sv switch
					{
						PIDValue pid => pid.Volts,
                        ThermometerValue temp => temp.Celsius,
                        BeadThermistorValue beadTemp => beadTemp.Ohms,	// beadTemp.Volts
                        HumidityValue humidity => humidity.Percent,		// humidity.Celsius
                        PressureValue pressure => pressure.Millibars,   // pressure.Celsius
                        GasValue gas => gas.SLPM,                       // gas.Millibars, gas.Celsius
                        ValveValue valve => valve.Opened ? 1 : 0,
                        _ => 0
					};

					double timestamp = Utils.Timestamp.Sec;
                    lmsGraph.Add(timestamp, value);

					goto exit;
				}
			}
		}

	exit:
		return;
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

    private async void OdorDisplay_Data(object? sender, Data e)
    {
		await Task.Run(() => Dispatcher.Invoke(() =>
		{
			UpdateIndicators(e);
		}));
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data e)
    {
        await Task.Run(() =>
        {
			// TODO
        });
    }

    // UI events

    private void InitilizeSetup()
	{
        var settings = Properties.Settings.Default;
        _pulseSetupFileName = settings.PulseSetupFilename;

        if (File.Exists(_pulseSetupFileName))
        {
            txbSetupFile.Text = _pulseSetupFileName;
            btnStart.IsEnabled = true;
        }
    }

    private async Task CreateIndicators()
	{
        var indicatorGenerator = new IndicatorGenerator();
        await indicatorGenerator.Run(indicator => Dispatcher.Invoke(() =>
        {
            indicator.MouseDown += ChannelIndicator_MouseDown;
            stpIndicators.Children.Add(indicator);
        }));
    }

    private async void Page_Loaded(object? sender, RoutedEventArgs e)
	{
		_storage
			.BindScaleToZoomLevel(sctScale)
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

        InitilizeSetup();

		await CreateIndicators();

        var queryMeasurements = new SetMeasurements(SetMeasurements.Command.Start);
        var queryResult = _odorDisplay.Request(queryMeasurements, out Ack? ack, out Response? response);
        if (queryResult.Error != OdorDisplay.Error.Success)
        {
			Utils.MsgBox.Error(Title, $"Cannot start measurements in Odor Display:\n{queryResult.Reason}");
        }

        _isInitilized = true;

        /*
		if (_com.IsOpen && _pid.ArePIDsOn)
		{
			UpdateDeviceStateUI();

			if (_lastSample == null)
			{
				var result = _pid.GetSample(out DeviceSample sample);
				HandleError(result);

				if (result.Error == Error.Success)
				{
					_lastSample = sample;
				}
			}

			ResetIndicators(_lastSample);
		}*/
    }

    private void Page_Unloaded(object? sender, RoutedEventArgs e)
	{
        _odorDisplay.Data -= OdorDisplay_Data;
        _smellInsp.Data -= SmellInsp_Data;
        
		_storage
            .UnbindScaleToZoomLevel(sctScale)
			.UnbindVisibilityToDebug(lblDebug);
	}

	private void Page_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.F4)
		{
			Next?.Invoke(this, new EventArgs());
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
			FileName = Path.GetFileName(settings.PulseSetupFilename),
            InitialDirectory = Path.GetDirectoryName(settings.PulseSetupFilename) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        if (ofd.ShowDialog() ?? false)
        {
            _pulseSetupFileName = ofd.FileName;

            txbSetupFile.Text = _pulseSetupFileName;
            btnStart.IsEnabled = true;

            settings.PulseSetupFilename = _pulseSetupFileName;
            settings.Save();
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {

    }
}
