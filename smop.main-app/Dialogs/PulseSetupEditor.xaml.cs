using Smop.MainApp.Generator;
using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Dialogs
{
    public partial class PulseSetupEditor : Window, INotifyPropertyChanged
    {
        public SessionProps[] Sessions => _sessions.ToArray();

        public string? Filename { get; private set; } = null;

        // Session props
        public string SessionHumidity
        {
            get => Session?.Humidity.ToString("F1") ?? "";
            set 
            {
                if (_sessionIndex >= 0 && float.TryParse(value, out float v))
                    _sessions[_sessionIndex].Humidity = v; 
            }
        }
        public string SessionInitialPause
        {
            get => Session?.Intervals.InitialPause.ToString("F1") ?? "";
            set 
            {
                if (_sessionIndex >= 0 && float.TryParse(value, out float v))
                    _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { InitialPause = v };
            }
        }

        public string SessionPulseDuration
        {
            get => Session?.Intervals.Pulse.ToString("F1") ?? "";
            set
            {
                if (_sessionIndex >= 0 && float.TryParse(value, out float v)) 
                    _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { Pulse = v };
            }
        }

        public string SessionDMSDelay
        {
            get => Session?.Intervals.DmsDelay >= 0 ? Session.Intervals.DmsDelay.ToString("F1") : "";
            set
            {
                if (_sessionIndex >= 0)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { DmsDelay = -1 };
                    }
                    else if (float.TryParse(value, out float v))
                    {
                        _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { DmsDelay = v };
                    }
                }
            }
        }

        public bool UseSessionDMS
        {
            get => Session?.Intervals.DmsDelay >= 0;
            set
            {
                if (_sessionIndex >= 0)
                {
                    if (value)
                    {
                        if (_sessions[_sessionIndex].Intervals.DmsDelay < 0)
                        {
                            _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { DmsDelay = _lastDmsDelay };
                        }
                    }
                    else
                    {
                        _lastDmsDelay = _sessions[_sessionIndex].Intervals.DmsDelay >= 0 ? _sessions[_sessionIndex].Intervals.DmsDelay : 5f;
                        _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { DmsDelay = -1 };
                    }
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionDMSDelay)));
                }
            }
        }

        public string SessionFinalPause
        {
            get => Session?.Intervals.FinalPause.ToString("F1") ?? "";
            set 
            {
                if (_sessionIndex >= 0 && float.TryParse(value, out float v)) 
                    _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { FinalPause = v };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PulseSetupEditor()
        {
            InitializeComponent();

            DialogTools.HideWindowButtons(this);
            DialogTools.SetCentralPosition(this);

            DataContext = this;

            _pulseChannelFlowValid = FindResource("TextBoxWithoutError") as Style;
            _pulseChannelFlowInvalid = FindResource("TextBoxWithError") as Style;

            for (int i = 1; i <= PulseChannels.Count; i++)
            {
                var id = i.ToString();

                grdPulse.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                var label = new Label() { Content = id };
                Grid.SetRow(label, i);
                Grid.SetColumn(label, 0);
                grdPulse.Children.Add(label);

                var chk = new CheckBox() { Tag = id };
                chk.Checked += PulseChannelUse_CheckedChanged;
                chk.Unchecked += PulseChannelUse_CheckedChanged;
                Grid.SetRow(chk, i);
                Grid.SetColumn(chk, 1);
                grdPulse.Children.Add(chk);

                _pulseChannelUseControl.Add(chk);

                var txb = new TextBox() { Tag = id };
                txb.TextChanged += PulseChannelFlow_TextChanged;
                Grid.SetRow(txb, i);
                Grid.SetColumn(txb, 2);

                txb.Style = _pulseChannelFlowValid;

                grdPulse.Children.Add(txb);

                _pulseChannelFlowControl.Add(txb);
            }
        }

        public void Load(string filename)
        {
            var setup = PulseSetup.Load(filename);
            if (setup != null)
            {
                Filename = filename;
                tblFileName.Text = filename;

                var sessions = setup.Sessions.ToList();
                foreach (var session in sessions)
                {
                    _sessions.Add(session);
                    lsvSessions.Items.Add($"#{_sessions.Count}");
                }

                _sessionIndex = 0;
                lsvSessions.SelectedIndex = _sessionIndex;

                grdSession.IsEnabled = true;
                btnAddPulse.IsEnabled = true;
            }
        }

        // Internal

        const float PULSE_CHANNEL_MIN = 0;
        const float PULSE_CHANNEL_MAX = 1500;

        readonly List<CheckBox> _pulseChannelUseControl = new();
        readonly List<TextBox> _pulseChannelFlowControl = new();

        readonly Style? _pulseChannelFlowValid;
        readonly Style? _pulseChannelFlowInvalid;

        List<SessionProps> _sessions = new ();
        int _sessionIndex = -1;
        int _pulseIndex = -1;
        float _lastDmsDelay = 5f;

        SessionProps? Session => _sessionIndex >= 0 ? _sessions[_sessionIndex] : null;
        PulseChannelProps[]? Channels => _sessionIndex >= 0 && _pulseIndex >= 0 ? _sessions[_sessionIndex].Pulses[_pulseIndex].Channels : null;

        private void UpdateChannelFlow(int id, float value)
        {
            if (Channels != null)
            {
                for (int i = 0; i < Channels.Length; i++)
                {
                    if (Channels[i].Id == id)
                    {
                        Channels[i] = Channels[i] with { Flow = value };
                        break;
                    }
                }
            }
        }

        private void UpdateChannelUse(int id, bool value)
        {
            if (Channels != null)
            {
                for (int i = 0; i < Channels.Length; i++)
                {
                    if (Channels[i].Id == id)
                    {
                        Channels[i] = Channels[i] with { Active = value };
                        break;
                    }
                }
            }
        }

        private void PulseChannelUse_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                var id = int.Parse((string)chk.Tag);
                UpdateChannelUse(id, chk.IsChecked ?? false);
            }
        }

        private void PulseChannelFlow_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox txb)
            {
                var id = int.Parse((string)txb.Tag);
                if (float.TryParse(txb.Text, out float value) && value >= PULSE_CHANNEL_MIN && value <= PULSE_CHANNEL_MAX)
                {
                    txb.Style = _pulseChannelFlowValid;
                    UpdateChannelFlow(id, value);
                }
                else
                {
                    txb.Style = _pulseChannelFlowInvalid;
                }
            }
        }

        // UI

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Storage.Instance.BindScaleToZoomLevel(sctScale);
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            Storage.Instance.UnbindScaleToZoomLevel(sctScale);
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            var newFilename = InputBox.Show(Title, "Type file name for the new setup:", "");
            if (newFilename == null)
            {
                return;
            }

            newFilename = newFilename.Trim().ToPath();
            if (newFilename.Length == 0)
            {
                return;
            }

            if (!Path.HasExtension(newFilename))
            {
                newFilename += ".txt";
            }

            if (File.Exists("Properties/" + newFilename))
            {
                MsgBox.Error(Title, $"File '{newFilename}' exists already");
                return;
            }

            Filename = "Properties/" + newFilename;
            tblFileName.Text = Filename;

            lsvPulses.Items.Clear();
            lsvSessions.Items.Clear();

            _pulseIndex = -1;
            _sessionIndex = -1;
            _sessions = new List<SessionProps> { };

            SessionAdd_Click(this, new RoutedEventArgs());
        }

        private void SessionAdd_Click(object sender, RoutedEventArgs e)
        {
            var session = new SessionProps(40, new PulseIntervals(10, 60, 10, 10));
            if (_sessionIndex >= 0)
            {
                foreach (var pulse in _sessions[_sessionIndex].Pulses)
                {
                    List<PulseChannelProps> channels = new();
                    foreach (var channel in pulse.Channels)
                    {
                        channels.Add(new PulseChannelProps(channel.Id, channel.Flow, channel.Active));
                    }
                    session.AddPulse(new PulseProps(channels.ToArray()));
                }
            }
            else
            {
                var pulseChannelProps = new List<PulseChannelProps>() { new PulseChannelProps(1, 10f, true) };
                for (int i = 2; i <= PulseChannels.Count; i++)
                {
                    pulseChannelProps.Add(new PulseChannelProps(i, 0f, false));
                }
                session.AddPulse(new PulseProps(pulseChannelProps.ToArray()));
            }

            _sessions.Add(session);
            _sessionIndex = _sessions.Count - 1;

            lsvSessions.Items.Add($"#{_sessions.Count}");
            lsvSessions.SelectedIndex = _sessionIndex;

            grdSession.IsEnabled = true;
            btnAddPulse.IsEnabled = true;
        }

        private void PulseAdd_Click(object sender, RoutedEventArgs e)
        {
            var channels = new List<PulseChannelProps>();

            foreach (var channel in Channels!)
            {
                channels.Add(new PulseChannelProps(channel.Id, channel.Flow, channel.Active));
            }

            Session!.AddPulse(new PulseProps(channels.ToArray()));
            _pulseIndex = Session!.Pulses.Length - 1;

            lsvPulses.Items.Add($"#{Session!.Pulses.Length}");
            lsvPulses.SelectedIndex = _pulseIndex;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Sessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _sessionIndex = lsvSessions.SelectedIndex;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionHumidity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionInitialPause)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionPulseDuration)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionDMSDelay)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseSessionDMS)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionFinalPause)));

            lsvPulses.Items.Clear();

            if (_sessionIndex >= 0)
            {
                _pulseIndex = 0;
                int i = 0;
                foreach (var pulse in Session!.Pulses)
                {
                    lsvPulses.Items.Add($"#{i+1}");
                    i++;
                }
                lsvPulses.SelectedIndex = _pulseIndex;
            }
            else
            {
                _pulseIndex = -1;
            }
        }

        private void Sessions_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                if (_sessionIndex >= 0 && lsvSessions.Items.Count > 1)
                {
                    _sessions.RemoveAt(_sessionIndex);

                    int index = Math.Max(0, lsvSessions.SelectedIndex - 1);
                    lsvSessions.Items.RemoveAt(lsvSessions.SelectedIndex);
                    lsvSessions.SelectedIndex = index;
                }
            }
        }

        private void Pulses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _pulseIndex = lsvPulses.SelectedIndex;

            grdPulse.IsEnabled = Session != null;

            foreach (var chk in _pulseChannelUseControl)
            {
                var id = int.Parse((string)chk.Tag);
                chk.IsChecked = Channels?.FirstOrDefault(c => c.Id == id)?.Active ?? false;
            }

            foreach (var txb in _pulseChannelFlowControl)
            {
                var id = int.Parse((string)txb.Tag);
                txb.Text = Channels?.FirstOrDefault(c => c.Id == id)?.Flow.ToString("F2") ?? "";
                txb.Style = _pulseChannelFlowValid;
            }
        }

        private void Pulses_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                if (_sessionIndex >= 0 && _pulseIndex >= 0 && lsvPulses.Items.Count > 1)
                {
                    _sessions[_sessionIndex].RemovePulse(_pulseIndex);

                    int index = Math.Max(0, lsvPulses.SelectedIndex - 1);
                    lsvPulses.Items.RemoveAt(lsvPulses.SelectedIndex);
                    lsvPulses.SelectedIndex = index;
                }
            }
        }
    }
}
