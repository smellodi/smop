using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SMOP.Comm;
using WPFLocalizeExtension.Engine;

namespace SMOP.Pages
{
    public partial class Connect : Page, IPage<EventArgs>, INotifyPropertyChanged
    {
        public event EventHandler<EventArgs>? Next;
        public event PropertyChangedEventHandler? PropertyChanged;

        public string PortAction => _com?.IsOpen ?? false ? "Close" : "Open";        // keys in L10n dictionaries

        public Connect()
        {
            InitializeComponent();

            DataContext = this;

            UpdatePortList(cmbCommPort);

            Application.Current.Exit += (s, e) => Close();

            LoadSettings();

            _usb.Inserted += (s, e) => Dispatcher.Invoke(() => UpdatePortList(cmbCommPort));
            _usb.Removed += (s, e) => Dispatcher.Invoke(() => UpdatePortList(cmbCommPort));

            UpdateUI();
        }


        // Internal

        readonly USB _usb = new();
        readonly Storage _storage = Storage.Instance;
        readonly CommPort _com = CommPort.Instance;
        readonly MFC _mfc = MFC.Instance;
        readonly PID _pid = PID.Instance;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PortAction)));

            cmbCommPort.IsEnabled = !_com.IsOpen;
        }

        private void ConnectToPort(string? address)
        {
            if (!_com.IsOpen)
            {
                Result result = _com.Start(address);

                if (result.Error != Error.Success)
                {
                    Utils.MsgBox.Error(Title, Utils.L10n.T("CannotOpenPort"));
                }
                else
                {
                    if (_storage.IsDebugging)
                    {
                        Comm.Emulator.MFC.Instance.Debug += Comm_DebugAsync;
                        Comm.Emulator.PID.Instance.Debug += Comm_DebugAsync;
                        Comm.Emulator.Source.Instance.Debug += Comm_DebugAsync;
                    }
                    else
                    {
                        _com.Debug += Comm_DebugAsync;
                    }

                    if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                    {
                        _mfc.Pump = DeviceState.On;
                    }
                    Next?.Invoke(this, new EventArgs());
                }
            }
        }

        private void Close()
        {
            if (_com.IsOpen)
            {
                _pid.Stop();
                _mfc.Stop();
                _com.Stop();
            }

            SaveSettings();
        }

        private void LoadSettings()
        {
            var settings = Properties.Settings.Default;

            foreach (string item in cmbCommPort.Items)
            {
                if (item == settings.SetupPort)
                {
                    cmbCommPort.SelectedItem = item;
                    break;
                }
            }
        }

        private void SaveSettings()
        {
            var settings = Properties.Settings.Default;
            try
            {
                settings.Language = LocalizeDictionary.Instance.Culture.Name;
                settings.SetupPort = cmbCommPort.SelectedItem?.ToString() ?? "";
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
                cmbCommPort.Items.Clear();
                cmbCommPort.Items.Add("COM3");
                cmbCommPort.SelectedIndex = 0;

                _mfc.IsDebugging = true;
                _pid.IsDebugging = true;

                _storage.IsDebugging = true;
                lblDebug.Visibility = Visibility.Visible;
            }
            else if (e.Key == Key.Enter)
            {
                if (btnConnectToPort.IsEnabled)
                {
                    ConnectToPort_Click(sender, e);
                }
            }
            // TODO remove from the release
            else if (e.Key >= Key.D0 && e.Key <= Key.D9 && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                Comm.Emulator.PID.Instance.Model._PulseInput(e.Key - Key.D0);
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9 && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                Comm.Emulator.PID.Instance.Model._PulseOutput(e.Key - Key.NumPad0);
            }
        }

        private void Port_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateUI();
        }

        private void ConnectToPort_Click(object? sender, RoutedEventArgs e)
        {
            ConnectToPort(_storage.IsDebugging ? null : (string)cmbCommPort.SelectedItem);

            UpdateUI();
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
}
