using Smop.OdorDisplay.Packets;
using Smop.MainApp.Dialogs;
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

namespace Smop.MainApp.Pages;

public partial class Connect : Page, IPage<Navigation>, INotifyPropertyChanged
{
    public event EventHandler<Navigation>? Next;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasOutputConnection => _odorDisplay.IsOpen;
    public bool HasOutputAndInputConnections => _odorDisplay.IsOpen && (_ionVision != null || _smellInsp.IsOpen);

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
        if (_odorDisplay.IsOpen)
            return;

        Comm.Result result = _odorDisplay.Open(address);

        if (result.Error != Comm.Error.Success)
        {
            MsgBox.Error(Title, "Cannot open the port");
        }
        else
        {
            btnConnectToOdorDisplay.Content = new Image() { Source = _greenButtonImage };

            var queryResult = _odorDisplay.Request(new QueryVersion(), out Ack? ack, out Response? response);
            if (queryResult.Error == Comm.Error.Success && response is OdorDisplay.Packets.Version version)
            {
                lblOdorDisplayInfo.Content = $"Hardware: {version.Hardware}, Software: {version.Software}, Protocol: {version.Protocol}";
            }

            //_odorDisplay.Debug += Comm_DebugAsync;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasOutputConnection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasOutputAndInputConnections)));
        }
    }

    private void ConnectToSmellInsp(string? address)
    {
        if (_smellInsp.IsOpen)
            return;

        var result = _smellInsp.Open(address);

        if (result.Error != Comm.Error.Success)
        {
            MsgBox.Error(Title, "Cannot open the port");
        }
        else
        {
            btnConnectToSmellInsp.Content = new Image() { Source = _greenButtonImage };

            var queryResult = _smellInsp.Send(SmellInsp.Command.GET_INFO);
            lblSmellInspInfo.Content = $"Status: {queryResult.Reason}";

            //_smellInsp.Debug += Comm_DebugAsync;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasOutputAndInputConnections)));
        }
    }

    private async Task ConnectToIonVisionAsync()
    {
        if (_ionVision != null)
            return;

        _ionVision = new IonVision.Communicator(IonVisionSetupFilename, _storage.Simulating.HasFlag(SimulationTarget.IonVision));

        btnConnectToIonVision.IsEnabled = false;
        var isConnected = await CheckIonVisionSettings(_ionVision);
        btnConnectToIonVision.IsEnabled = true;

        if (!isConnected)
        {
            _ionVision = null;
        }
        else
        {
            btnConnectToIonVision.Content = new Image() { Source = _greenButtonImage };
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasOutputAndInputConnections)));

            var status = HandleIonVisionError(await _ionVision.GetSystemStatus(), "GetSystemStatus");
            if (status.Success)
            {
                lblIonVisionInfo.Content = $"Device type: {status.Value?.DeviceType ?? 0}";
                btnChooseIonVisionSetupFile.IsEnabled = false;
                btnEditIonVisionSetup.IsEnabled = false;
            }

            App.IonVision = _ionVision;
        }
    }

    private async Task<bool> CheckIonVisionSettings(IonVision.Communicator ionVision)
    {
        var info = HandleIonVisionError(await ionVision.GetSystemInfo(), "GetSystemInfo");
        btnConnectToIonVision.IsEnabled = true;

        if (!info.Success || info.Value == null)
        {
            MsgBox.Error(Title, "Cannot connect to the device");
            return false;
        }
        else if (!info.Value.CurrentVersion.StartsWith(ionVision.SupportedVersion))
        {
            var settings = Properties.Settings.Default;
            var ignoredVersions = settings.Comm_IonVision_IgnoreVersionWarning ?? new System.Collections.Specialized.StringCollection();

            if (!ignoredVersions.Contains(info.Value.CurrentVersion))
            {
                var msg = $"Version mismatch: this software targets the version {ionVision.SupportedVersion}, but the device is operating the version {info.Value.CurrentVersion}. Continue?";
                var reply = MsgBox.Custom(Title, msg, MsgBox.MsgIcon.Warning,
                    "ignore warnings for this version", null, MsgBox.Button.Yes, MsgBox.Button.No);
                if (reply.Button == MsgBox.Button.No)
                {
                    return false;
                }
                else if (reply.IsOptionAccepted)
                {
                    ignoredVersions.Add(info.Value.CurrentVersion);
                    settings.Comm_IonVision_IgnoreVersionWarning = ignoredVersions;
                    settings.Save();
                }
            }
        }

        await Task.Delay(200);
        var projects = HandleIonVisionError(await ionVision.GetProjects(), "GetProjects");

        if (!projects.Value?.Contains(ionVision.Settings.Project) ?? false)
        {
            string projectList = string.Join("\n", projects.Value ?? Array.Empty<string>());
            if (_storage.Simulating.HasFlag(SimulationTarget.IonVision))
            {
                projectList += "\n\nNote that even in the simulation mode the project names and parameters";
                projectList += "\nspecified in the simulator and in the IonVision settings file must match.";
            }
            MsgBox.Warn(Title, $"Project '{ionVision.Settings.Project}' does not exist.\nPlease edit '{IonVisionSetupFilename}' file\nand specify one of the following projects\n\n{projectList}",
                MsgBox.Button.OK);

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

        var ivSettings = new IonVision.Settings(IonVisionSetupFilename);
        txbIonVisionIP.Text = ivSettings.IP;
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
            if (e.Source == this || e.Source == cmbOdorDisplayCommPort)
            {
                cmbOdorDisplayCommPort.Items.Clear();
                cmbOdorDisplayCommPort.Items.Add("simulator");
                cmbOdorDisplayCommPort.SelectedIndex = 0;
                _storage.AddSimulatingTarget(SimulationTarget.OdorDisplay);
            }

            if (e.Source == this || e.Source == cmbSmellInspCommPort)
            {
                cmbSmellInspCommPort.Items.Clear();
                cmbSmellInspCommPort.Items.Add("simulator");
                cmbSmellInspCommPort.SelectedIndex = 0;
                _storage.AddSimulatingTarget(SimulationTarget.SmellInspector);
            }

            if (e.Source == this || e.Source == txbIonVisionIP)
            {
                txbIonVisionIP.Text = "simulator";
                _storage.AddSimulatingTarget(SimulationTarget.IonVision);
            }

            if (e.Source == this)
            {
                _storage.AddSimulatingTarget(SimulationTarget.All);
            }

            //lblDebug.Visibility = Visibility.Visible;
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
        ConnectToOdorDispay(_storage.Simulating.HasFlag(SimulationTarget.OdorDisplay) ? null : (string)cmbOdorDisplayCommPort.SelectedItem);
        UpdateUI();
    }

    private void ConnectToSmellInsp_Click(object? sender, RoutedEventArgs e)
    {
        ConnectToSmellInsp(_storage.Simulating.HasFlag(SimulationTarget.SmellInspector) ? null : (string)cmbSmellInspCommPort.SelectedItem);
        UpdateUI();
    }

    private async void ConnectToIonVision_Click(object? sender, RoutedEventArgs e)
    {
        btnConnectToIonVision.IsEnabled = false;
        btnChooseIonVisionSetupFile.IsEnabled = false;
        btnEditIonVisionSetup.IsEnabled = false;

        await ConnectToIonVisionAsync();

        btnConnectToIonVision.IsEnabled = true;
        btnChooseIonVisionSetupFile.IsEnabled = true;
        btnEditIonVisionSetup.IsEnabled = true;
    }

    private void ChooseIonVisionSetupFile_Click(object? sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files|*.json",
            FileName = Path.GetFileName(IonVisionSetupFilename),
            InitialDirectory = Path.GetDirectoryName(IonVisionSetupFilename)
        };

        if (ofd.ShowDialog() ?? false)
        {
            var filename = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, ofd.FileName);
            if (filename.StartsWith(".."))
            {
                IonVisionSetupFilename = ofd.FileName;
            }
            else
            {
                IonVisionSetupFilename = filename;
            }

            var ivSettings = new IonVision.Settings(IonVisionSetupFilename);
            txbIonVisionIP.Text = ivSettings.IP;

            var settings = Properties.Settings.Default;
            settings.Comm_IonVision_SetupFilename = IonVisionSetupFilename;
            settings.Save();
        }
    }

    private void EditIonVisionSetup_Click(object? sender, RoutedEventArgs e)
    {
        var setupDialog = new IonVisionSetupEditor();
        setupDialog.Load(IonVisionSetupFilename);
        if (setupDialog.ShowDialog() == true)
        {
            var ivSettings = new IonVision.Settings(IonVisionSetupFilename)
            {
                IP = setupDialog.IP,
                Project = setupDialog.Project,
                ParameterName = setupDialog.ParameterName,
                ParameterId = setupDialog.ParameterId
            };
            ivSettings.Save();
        }
    }

    private void GeneratePulses_Click(object? sender, RoutedEventArgs e)
    {
        SaveSettings();
        Next?.Invoke(this, Navigation.PulseGeneratorSetup);
    }

    private void ReproduceOdor_Click(object? sender, RoutedEventArgs e)
    {
        SaveSettings();
        Next?.Invoke(this, Navigation.OdorReproductionSetup);
    }
}
