using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WPFLocalizeExtension.Engine;

namespace Smop.PulseGen.Pages;

public partial class Connect : Page, IPage<EventArgs>, INotifyPropertyChanged
{
	public event EventHandler<EventArgs>? Next;
	public event PropertyChangedEventHandler? PropertyChanged;

	public bool HasNecessaryConnections => _odorDisplayCom.IsOpen && _ionVisionCom != null;

	public Connect()
	{
		InitializeComponent();

		DataContext = this;

		UpdatePortList(cmbOdorDisplayCommPort);
		UpdatePortList(cmbSmellInspCommPort);

		Application.Current.Exit += (s, e) => Close();

		LoadSettings();

		_usb.Inserted += (s, e) => Dispatcher.Invoke(() => {
			UpdatePortList(cmbOdorDisplayCommPort);
			UpdatePortList(cmbSmellInspCommPort);
		});
		_usb.Removed += (s, e) => Dispatcher.Invoke(() => {
			UpdatePortList(cmbOdorDisplayCommPort);
			UpdatePortList(cmbSmellInspCommPort);
		});

		UpdateUI();
	}


	// Internal

	readonly string IV_SETTINGS_FILENAME = "Properties/IonVision.json";

	readonly System.Windows.Media.Imaging.BitmapImage _greenButtonImage = new(new Uri($@"/smop.pulse-gen;component/Assets/images/button-green.png", UriKind.Relative));

    readonly Smop.OdorDisplay.COMUtils _usb = new();
	readonly Storage _storage = Storage.Instance;

	readonly Smop.OdorDisplay.CommPort _odorDisplayCom = Smop.OdorDisplay.CommPort.Instance;
	readonly Smop.SmellInsp.CommPort _smellInspCom = Smop.SmellInsp.CommPort.Instance;
    
	Smop.IonVision.Communicator? _ionVisionCom = null;

    //readonly MFC _mfc = MFC.Instance;
    //readonly PID _pid = PID.Instance;

    private void UpdatePortList(ComboBox cmb)
	{
		var current = cmb.SelectedValue;

		cmb.Items.Clear();

		var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
		var ports = new HashSet<string>(availablePorts);
		foreach (var port in ports)
		{
			cmb.Items.Add(port);
		}

		if (current != null)
		{
			foreach (var item in ports)
			{
				if (item == current.ToString())
				{
					cmb.SelectedItem = item;
					break;
				}
			}
		}
	}

	private void UpdateUI()
	{
		cmbOdorDisplayCommPort.IsEnabled = !_odorDisplayCom.IsOpen;
        cmbSmellInspCommPort.IsEnabled = !_smellInspCom.IsOpen;
    }

    private void ConnectToOdorDispay(string? address)
	{
		if (!_odorDisplayCom.IsOpen)
		{
            Smop.OdorDisplay.Result result = _odorDisplayCom.Open(address);

			if (result.Error != Smop.OdorDisplay.Error.Success)
			{
				Utils.MsgBox.Error(Title, Utils.L10n.T("CannotOpenPort"));
			}
			else
			{
				btnConnectToOdorDisplay.Content = new Image() { Source = _greenButtonImage };

                if (_storage.IsDebugging)
				{
					//OdorDisplay.Emulator.MFC.Instance.Debug += Comm_DebugAsync;
					//OdorDisplay.Emulator.PID.Instance.Debug += Comm_DebugAsync;
					//OdorDisplay.Emulator.Source.Instance.Debug += Comm_DebugAsync;
				}
				else
				{
					_odorDisplayCom.Debug += Comm_DebugAsync;
				}

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNecessaryConnections)));
            }
		}
	}

    private void ConnectToSmellInsp(string? address)
    {
        if (!_smellInspCom.IsOpen)
        {
            var result = _smellInspCom.Open(address);

            if (result.Error != Smop.OdorDisplay.Error.Success)
            {
                Utils.MsgBox.Error(Title, Utils.L10n.T("CannotOpenPort"));
            }
            else
            {
                btnConnectToSmellInsp.Content = new Image() { Source = _greenButtonImage }; ;

                if (_storage.IsDebugging)
                {
                    //OdorDisplay.Emulator.MFC.Instance.Debug += Comm_DebugAsync;
                    //OdorDisplay.Emulator.PID.Instance.Debug += Comm_DebugAsync;
                    //OdorDisplay.Emulator.Source.Instance.Debug += Comm_DebugAsync;
                }
                else
                {
                    _smellInspCom.Debug += Comm_DebugAsync;
                }

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNecessaryConnections)));
            }
        }
    }

    private async void ConnectToIonVision(string address)
    {
        if (_ionVisionCom == null)
        {
			_ionVisionCom = new IonVision.Communicator(IV_SETTINGS_FILENAME, _storage.IsDebugging);

			btnConnectToIonVision.IsEnabled = false;
            var version = await _ionVisionCom.GetSystemInfo();
            btnConnectToIonVision.IsEnabled = true;

            if (!version.Success)
            {
                _ionVisionCom = null;
                Utils.MsgBox.Error(Title, Utils.L10n.T("CannotConnectToHost"));
                return;
            }
            else if (version.Value!.CurrentVersion != _ionVisionCom.SupportedVersion)
            {
                Utils.MsgBox.Warn(Title, string.Format(Utils.L10n.T("VersionMismatch"), _ionVisionCom.SupportedVersion, version.Value!.CurrentVersion));
            }
			else
			{
                btnConnectToIonVision.Content = new Image() { Source = _greenButtonImage }; ;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNecessaryConnections)));
            }
        }
    }

    private void Close()
	{
		if (_odorDisplayCom.IsOpen)
		{
			//_pid.Stop();
			//_mfc.Stop();
			_odorDisplayCom.Close();
		}

		SaveSettings();
	}

	private void LoadSettings()
	{
		var settings = Properties.Settings.Default;

		foreach (string item in cmbOdorDisplayCommPort.Items)
		{
			if (item == settings.OdorDisplayPort)
			{
				cmbOdorDisplayCommPort.SelectedItem = item;
				break;
			}
		}

        foreach (string item in cmbSmellInspCommPort.Items)
        {
            if (item == settings.SmellInspPort)
            {
                cmbSmellInspCommPort.SelectedItem = item;
                break;
            }
        }


        IonVision.Settings ivSetings = new IonVision.Settings(IV_SETTINGS_FILENAME);
        txbIonVisionIP.Text = ivSetings.IP;
    }

    private void SaveSettings()
	{
		var settings = Properties.Settings.Default;
		try
		{
			settings.Language = LocalizeDictionary.Instance.Culture.Name;
			settings.OdorDisplayPort = cmbOdorDisplayCommPort.SelectedItem?.ToString() ?? "";
            settings.SmellInspPort = cmbSmellInspCommPort.SelectedItem?.ToString() ?? "";
        }
        catch { }
		settings.Save();
	}

	private async void Comm_DebugAsync(object? sender, string e)
	{
		// implement later
		await Task.Run(() => { });
	}

	// UI events

	private void Page_Loaded(object? sender, RoutedEventArgs e)
	{
		_storage
			.BindScaleToZoomLevel(sctScale)
			.BindVisibilityToDebug(lblDebug);

		if (Focusable)
		{
			Focus();
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
		if (e.Key == Key.F2)
		{
			cmbOdorDisplayCommPort.Items.Clear();
			cmbOdorDisplayCommPort.Items.Add("COM3");
			cmbOdorDisplayCommPort.SelectedIndex = 0;

            cmbSmellInspCommPort.Items.Clear();
            cmbSmellInspCommPort.Items.Add("COM4");
            cmbSmellInspCommPort.SelectedIndex = 0;

            txbIonVisionIP.Text = "simulator";

            //_mfc.IsDebugging = true;
            //_pid.IsDebugging = true;

            _storage.IsDebugging = true;
			lblDebug.Visibility = Visibility.Visible;
		}
		else if (e.Key == Key.Enter)
		{
			if (btnConnectToOdorDisplay.IsEnabled)
			{
                ConnectToOdorDisplay_Click(sender, e);
			}
		}
	}

	private void OdorDisplayPort_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		UpdateUI();
	}

    private void SmellInspPort_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateUI();
    }

    private void ConnectToOdorDisplay_Click(object? sender, RoutedEventArgs e)
	{
		ConnectToOdorDispay(_storage.IsDebugging ? null : (string)cmbOdorDisplayCommPort.SelectedItem);

		UpdateUI();
	}

    private void ConnectToSmellInsp_Click(object? sender, RoutedEventArgs e)
    {
        ConnectToSmellInsp(_storage.IsDebugging ? null : (string)cmbSmellInspCommPort.SelectedItem);

        UpdateUI();
    }

    private void ConnectToIonVision_Click(object? sender, RoutedEventArgs e)
    {
        ConnectToIonVision(_storage.IsDebugging ? "" : txbIonVisionIP.Text);

        UpdateUI();
    }

    private void Connect_Click(object? sender, RoutedEventArgs e)
    {
        Next?.Invoke(this, new EventArgs());
    }

    private void Language_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		var culture = CultureInfo.GetCultureInfo("en-US");
		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.DefaultThreadCurrentUICulture = culture;
		System.Threading.Thread.CurrentThread.CurrentCulture = culture;
		System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
	}
}
