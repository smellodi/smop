﻿using Smop.PulseGen.Utils;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Smop.PulseGen.Dialogs
{
    public partial class IonVisionSetupEditor : Window, INotifyPropertyChanged
    {
        public string? IP { get; private set; }

        public string? User { get; private set; }

        public string[] Projects { get; private set; } = System.Array.Empty<string>();

        public string[] Parameters => _parameters.Select(p => p.Name).ToArray();

        public string? Project => cmbProjects.SelectedItem?.ToString();
        public string? ParameterName => cmbParameters.SelectedIndex >= 0 ? _parameters[cmbParameters.SelectedIndex].Name : null;
        public string? ParameterId => cmbParameters.SelectedIndex >= 0 ? _parameters[cmbParameters.SelectedIndex].Id : null;

        public event PropertyChangedEventHandler? PropertyChanged;

        public IonVisionSetupEditor()
        {
            InitializeComponent();

            DialogTools.HideWindowButtons(this);
            DialogTools.SetCentralPosition(this);

            DataContext = this;
        }

        public void Load(string? filename)
        {
            tblFileName.Text = filename;

            _ivSettings = new IonVision.Settings(filename);

            IP = _ivSettings.IP;
            User = _ivSettings.User;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IP)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(User)));

            DispatchOnce.Do(0.5, () => Dispatcher.Invoke(async () => await Connect()));
        }

        // Internal

        IonVision.API? _ivAPI;
        IonVision.Settings? _ivSettings;
        IonVision.Parameter[] _parameters = System.Array.Empty<IonVision.Parameter>();

        private async Task Connect()
        {
            while (true)
            {
                bdrWait.Visibility = Visibility.Visible;

                IP = InputBox.Show(Title, "Please connect IonVision device with USB cable, then type its IP address and click 'OK'.", IP);
                if (string.IsNullOrEmpty(IP))
                {
                    Close();
                    return;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IP)));

                _ivAPI = new IonVision.API(IP);

                await Task.Delay(150);
                var status = await _ivAPI.GetSystemStatus();
                if (!status.Success)
                {
                    MsgBox.Error(Title, "Cannot connect to IonVision.");
                }
                else
                {
                    await Task.Delay(150);
                    var user = await _ivAPI.GetUser();
                    if (user?.Success == true)
                    {
                        User = user?.Value?.Name ?? "";
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(User)));
                    }

                    await Task.Delay(150);
                    var projects = await _ivAPI.GetProjects();
                    if (projects?.Success == true && projects.Value?.Length > 0)
                    {
                        cmbProjects.IsEnabled = true;
                        Projects = projects.Value ?? Projects;
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

        private async void Projects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_ivAPI == null || _ivSettings == null)
            {
                return;
            }

            var projectName = cmbProjects.SelectedItem.ToString();
            if (!string.IsNullOrEmpty(projectName))
            {
                var result = await _ivAPI.SetProject(new IonVision.ProjectAsName(projectName));
                if (result.Success)
                {
                    bdrWait.Visibility = Visibility.Visible;

                    await Task.Delay(150);
                    var defs = await _ivAPI.GetProjectDefinition(projectName);

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
                    }
                    else
                    {
                        MsgBox.Error(Title, "This project has no parameters.");
                    }

                    bdrWait.Visibility = Visibility.Hidden;
                }
            }
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
}