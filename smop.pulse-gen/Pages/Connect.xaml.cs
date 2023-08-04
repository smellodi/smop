using Smop.OdorDisplay.Packets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Smop.PulseGen.Pages;

public partial class Connect : Page, IPage<Navigation>, INotifyPropertyChanged
{
    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasNecessaryConnections => _odorDisplay.IsOpen;

    public Connect()
    {
        InitializeComponent();

        _greenButtonImage = new(Utils.Resources.GetUri("Assets/images/button-green.png"));

        DataContext = this;

        UpdatePortList(cmbOdorDisplayCommPort);
        UpdatePortList(cmbSmellInspCommPort);

        Application.Current.Exit += (s, e) => Close();

        LoadSettings();

        _usb.Inserted += (s, e) => Dispatcher.Invoke(() =>
        {
            UpdatePortList(cmbOdorDisplayCommPort, OdorDisplay.COMUtils.SMOPPort);
            UpdatePortList(cmbSmellInspCommPort);
        });
        _usb.Removed += (s, e) => Dispatcher.Invoke(() =>
        {
            UpdatePortList(cmbOdorDisplayCommPort, OdorDisplay.COMUtils.SMOPPort);
            UpdatePortList(cmbSmellInspCommPort);
        });

        UpdateUI();
    }


    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Connect) + "Page");

    readonly System.Windows.Media.Imaging.BitmapImage _greenButtonImage;

    readonly OdorDisplay.COMUtils _usb = new();
    readonly Storage _storage = Storage.Instance;

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    string IonVisionSetupFilename = "Properties/IonVision.json";
    IonVision.Communicator? _ionVision = null;

    private static void UpdatePortList(ComboBox cmb, OdorDisplay.COMUtils.Port? defaultPort = null)
    {
        var current = cmb.SelectedValue ?? defaultPort?.Name;

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
        cmbOdorDisplayCommPort.IsEnabled = !_odorDisplay.IsOpen;
        cmbSmellInspCommPort.IsEnabled = !_smellInsp.IsOpen;
    }

    private void ConnectToOdorDispay(string? address)
    {
        if (!_odorDisplay.IsOpen)
        {
            OdorDisplay.Result result = _odorDisplay.Open(address);

            if (result.Error != OdorDisplay.Error.Success)
            {
                Utils.MsgBox.Error(Title, "Cannot open the port");
            }
            else
            {
                btnConnectToOdorDisplay.Content = new Image() { Source = _greenButtonImage };

                var queryResult = _odorDisplay.Request(new QueryVersion(), out Ack? ack, out Response? response);
                if (result.Error == OdorDisplay.Error.Success && response is OdorDisplay.Packets.Version version)
                {
                    lblOdorDisplayInfo.Content = $"Hardware: {version.Hardware}, Software: {version.Software}, Protocol: {version.Protocol}";
                }

                //_odorDisplay.Debug += Comm_DebugAsync;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNecessaryConnections)));
            }
        }
    }

    private void ConnectToSmellInsp(string? address)
    {
        if (!_smellInsp.IsOpen)
        {
            var result = _smellInsp.Open(address);

            if (result.Error != Smop.OdorDisplay.Error.Success)
            {
                Utils.MsgBox.Error(Title, "Cannot open the port");
            }
            else
            {
                btnConnectToSmellInsp.Content = new Image() { Source = _greenButtonImage };

                var queryResult = _smellInsp.Send(SmellInsp.Command.GET_INFO);
                lblSmellInspInfo.Content = $"Status: {queryResult.Reason}";

                //_smellInsp.Debug += Comm_DebugAsync;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNecessaryConnections)));
            }
        }
    }

    private async Task ConnectToIonVisionAsync()
    {
        if (_ionVision != null)
        {
            return;
        }

        _ionVision = new IonVision.Communicator(IonVisionSetupFilename, _storage.IsDebugging);

        btnConnectToIonVision.IsEnabled = false;
        var isConnected = await CheckIonVisionSettings(_ionVision);
        btnConnectToIonVision.IsEnabled = true;

        if (!isConnected)
        {
            _ionVision = null;
        }
        else
        {
            btnConnectToIonVision.Content = new Image() { Source = _greenButtonImage }; ;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNecessaryConnections)));

            var status = HandleIonVisionError(await _ionVision.GetSystemStatus(), "GetSystemStatus");
            if (status.Success)
            {
                lblIonVisionInfo.Content = $"Device type: {status.Value?.DeviceType ?? 0}";
            }

            App.IonVision = _ionVision;
        }
    }

    private async Task<bool> CheckIonVisionSettings(IonVision.Communicator ionVision)
    {
        var info = HandleIonVisionError(await ionVision.GetSystemInfo(), "GetSystemInfo");
        btnConnectToIonVision.IsEnabled = true;

        if (!info.Success)
        {
            Utils.MsgBox.Error(Title, "Cannot connect to the device");
            return false;
        }
        else if (!info.Value!.CurrentVersion.StartsWith(ionVision.SupportedVersion))
        {
            var settings = Properties.Settings.Default;
            var ignoredVersions = settings.Comm_IonVision_IgnoreVersionWarning ?? new System.Collections.Specialized.StringCollection();

            if (!ignoredVersions.Contains(info.Value!.CurrentVersion))
            {
                var msg = $"Version mismatch: this software targets the version {ionVision.SupportedVersion}, but the device is operating the version {info.Value!.CurrentVersion}. Continue?";
                var reply = Utils.MsgBox.Custom(Title, msg, Utils.MsgBox.MsgIcon.Warning,
                    "ignore warnings for this version", null, Utils.MsgBox.Button.Yes, Utils.MsgBox.Button.No);
                if (reply.Button == Utils.MsgBox.Button.No)
                {
                    return false;
                }
                else if (reply.IsOptionAccepted)
                {
                    ignoredVersions.Add(info.Value!.CurrentVersion);
                    settings.Comm_IonVision_IgnoreVersionWarning = ignoredVersions;
                    settings.Save();
                }
            }
        }

        await Task.Delay(200);
        var projects = HandleIonVisionError(await ionVision.GetProjects(), "GetProjects");

        if (!projects.Value?.Contains(ionVision.Settings.Project) ?? false)
        {
            string projectList = string.Join("\n", projects.Value!);
            Utils.MsgBox.Warn(Title, $"Project '{ionVision.Settings.Project}' does not exist.\nPlease edit '{IonVisionSetupFilename}' file\nand set one of the following projects\n\n{projectList}",
                Utils.MsgBox.Button.OK);

            return false;
        }

        return true;
    }

    private static IonVision.API.Response<T> HandleIonVisionError<T>(IonVision.API.Response<T> response, string action)
    {
        var error = !response.Success ? response.Error : "OK";
        _nlog.Info($"{action}: {error}");
        return response;
    }

    private void Close()
    {
        if (_odorDisplay.IsOpen)
        {
            _odorDisplay.Close();
        }

        if (_smellInsp.IsOpen)
        {
            _smellInsp.Close();
        }

        _ionVision?.Dispose();
    }

    private void LoadSettings()
    {
        var settings = Properties.Settings.Default;

        foreach (string item in cmbOdorDisplayCommPort.Items)
        {
            if (item == settings.Comm_OdorDisplay_Port)
            {
                cmbOdorDisplayCommPort.SelectedItem = item;
                break;
            }
        }

        foreach (string item in cmbSmellInspCommPort.Items)
        {
            if (item == settings.Comm_SmellInsp_Port)
            {
                cmbSmellInspCommPort.SelectedItem = item;
                break;
            }
        }

        IonVisionSetupFilename = settings.Comm_IonVision_SetupFilename;

        var ivSetings = new IonVision.Settings(IonVisionSetupFilename);
        txbIonVisionIP.Text = ivSetings.IP;
    }

    private void SaveSettings()
    {
        var settings = Properties.Settings.Default;
        try
        {
            settings.Comm_OdorDisplay_Port = cmbOdorDisplayCommPort.SelectedItem?.ToString() ?? "";
            settings.Comm_SmellInsp_Port = cmbSmellInspCommPort.SelectedItem?.ToString() ?? "";
        }
        catch { }
        settings.Save();
    }

    // implement later
    /*
    private async void Comm_DebugAsync(object? sender, string e)
	{
		await Task.Run(() => { });
	}*/

    // UI events

    private void Page_Loaded(object? sender, RoutedEventArgs e)
    {
        _storage
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
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
            .UnbindContentToZoomLevel(lblZoom)
            .UnbindVisibilityToDebug(lblDebug);
    }

    private void Page_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            cmbOdorDisplayCommPort.Items.Clear();
            cmbOdorDisplayCommPort.Items.Add("simulator");
            cmbOdorDisplayCommPort.SelectedIndex = 0;

            cmbSmellInspCommPort.Items.Clear();
            cmbSmellInspCommPort.Items.Add("simulator");
            cmbSmellInspCommPort.SelectedIndex = 0;

            txbIonVisionIP.Text = "simulator";

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

    private void Port_SelectionChanged(object? sender, SelectionChangedEventArgs e)
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

    private async void ConnectToIonVision_Click(object? sender, RoutedEventArgs e)
    {
        btnConnectToIonVision.IsEnabled = false;
        btnChooseIonVisionSetupFile.IsEnabled = false;

        await ConnectToIonVisionAsync();

        btnConnectToIonVision.IsEnabled = true;
        btnChooseIonVisionSetupFile.IsEnabled = true;
    }

    private void ChooseIonVisionSetupFile_Click(object? sender, RoutedEventArgs e)
    {
        string appBaseDir = AppDomain.CurrentDomain.BaseDirectory;
        string ionVisionSetupPath = Path.Combine(appBaseDir, IonVisionSetupFilename);

        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files|*.json",
            FileName = Path.GetFileName(ionVisionSetupPath),
            InitialDirectory = Path.GetDirectoryName(ionVisionSetupPath)
        };

        if (ofd.ShowDialog() ?? false)
        {
            IonVisionSetupFilename = ofd.FileName;

            var ivSetings = new IonVision.Settings(IonVisionSetupFilename);
            txbIonVisionIP.Text = ivSetings.IP;

            var settings = Properties.Settings.Default;
            settings.Comm_IonVision_SetupFilename = IonVisionSetupFilename;
            settings.Save();
        }
    }

    private void Continue_Click(object? sender, RoutedEventArgs e)
    {
        SaveSettings();
        Next?.Invoke(this, Navigation.Setup);
    }
}
