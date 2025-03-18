using Smop.Common;
using System;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace Smop.MainApp.Dialogs;

public partial class IonVisionSetupEditor : Window, INotifyPropertyChanged, IDisposable
{
    public string Filename { get; private set; } = "";

    public string IP { get; private set; } = "";

    public string? User { get; private set; }

    public string[] Projects { get; private set; } = Array.Empty<string>();

    public string[] Parameters => _parameters.Select(p => p.Name).ToArray();

    public string Project => cmbProjects.SelectedItem?.ToString() ?? "";
    public string ParameterName => cmbParameters.SelectedIndex >= 0 ? _parameters[cmbParameters.SelectedIndex].Name : "";
    public string ParameterId => cmbParameters.SelectedIndex >= 0 ? _parameters[cmbParameters.SelectedIndex].Id : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public IonVisionSetupEditor()
    {
        InitializeComponent();

        DialogTools.HideWindowButtons(this);
        DialogTools.SetCentralPosition(this);
    }

    public void Load(string filename)
    {
        tblFileName.Text = filename;

        _ivSettings = new IonVision.Settings(filename);

        Filename = filename;

        IP = _ivSettings.IP;
        User = _ivSettings.User;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IP)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(User)));

        DispatchOnce.Do(0.5, () => Dispatcher.Invoke(async () => await Connect()));
    }

    public void Dispose()
    {
        _ivComm?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    IonVision.Communicator? _ivComm;
    IonVision.Settings? _ivSettings;
    IonVision.Defs.Parameter[] _parameters = Array.Empty<IonVision.Defs.Parameter>();

    private async Task Connect()
    {
        while (true)
        {
            bdrWait.Visibility = Visibility.Visible;

            IP = InputBox.Show(Title, "Make sure IonVision device is reachable over the local network, then type its IP address and click 'OK'.", IP) ?? "";
            if (string.IsNullOrEmpty(IP))
            {
                Close();
                return;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IP)));

            _ivComm = new IonVision.Communicator(tblFileName.Text, IP == "" || IP.ToLower() == "localhost" || IP == "127.0.0.1");

            await Task.Delay(150);
            var status = await _ivComm.GetSystemStatus();
            if (!status.Success)
            {
                MsgBox.Error(Title, "Cannot connect to IonVision.");
            }
            else
            {
                await Task.Delay(150);
                var user = await _ivComm.GetUser();
                if (user?.Success == true)
                {
                    User = user?.Value?.Name ?? "";
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(User)));
                }

                await Task.Delay(150);
                var projects = await _ivComm.GetProjects();
                if (projects?.Success == true && projects.Value?.Length > 0)
                {
                    cmbProjects.IsEnabled = true;
                    Projects = projects.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Projects)));
                }
                else
                {
                    MsgBox.Error(Title, "No projects found.");
                    Close();
                }

                bdrWait.Visibility = Visibility.Hidden;
                break;
            }
        }
    }

    // UI

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files|*.json",
            FileName = System.IO.Path.GetFileName(Filename),
            InitialDirectory = System.IO.Path.GetDirectoryName(Filename)
        };

        if (sfd.ShowDialog() ?? false)
        {
            Filename = Utils.IoHelper.GetShortestFilePath(sfd.FileName);
            DialogResult = true;
        }
    }

    private async void Projects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_ivComm == null || _ivSettings == null)
        {
            return;
        }

        //var isProjectLoaded = false;
        //void UpdateProjectStatus(object? s, IonVision.EventSink.TimedEventArgs e) => isProjectLoaded = true;

        //IonVision.EventSink events = new IonVision.EventSink(_ivSettings.IP);
        //events.ProjectSetupFinished += UpdateProjectStatus;
        //events.CurrentProjectChanged += UpdateProjectStatus;

        var projectName = cmbProjects.SelectedItem.ToString();
        if (!string.IsNullOrEmpty(projectName))
        {
            var result = await _ivComm.SetProject(new IonVision.Defs.ProjectAsName(projectName));
            if (result.Success)
            {
                bdrWait.Visibility = Visibility.Visible;

                await Task.Delay(300);
                /*while (!isProjectLoaded)
                {
                    System.Diagnostics.Debug.WriteLine("loading the project...");
                    await Task.Delay(100);
                }*/

                var defs = await _ivComm.GetProjectDefinition(projectName);

                if (defs?.Success == true && defs.Value?.Parameters.Length > 0)
                {
                    _parameters = defs.Value.Parameters;

                    cmbParameters.IsEnabled = true;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Parameters)));

                    try
                    {
                        cmbParameters.SelectedItem = _parameters.First(p => p.Name == _ivSettings.ParameterName).Name;
                    }
                    catch
                    {
                        cmbParameters.SelectedIndex = 0;
                    }

                    btnSave.IsEnabled = true;
                    btnSaveAs.IsEnabled = true;
                }
                else
                {
                    MsgBox.Error(Title, "This project has no parameters.");
                }

                bdrWait.Visibility = Visibility.Hidden;
            }
        }

        //events.ProjectSetupFinished -= UpdateProjectStatus;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.BindScaleToZoomLevel(sctScale);
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.UnbindScaleToZoomLevel(sctScale);
    }
}
