using Smop.PulseGen.Generator;
using Smop.PulseGen.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Smop.PulseGen.Dialogs
{
    /// <summary>
    /// Interaction logic for PulseSetupEditor.xaml
    /// </summary>
    public partial class PulseSetupEditor : Window, INotifyPropertyChanged
    {
        public SessionProps[] Sessions => _sessions.ToArray();

        public string? Filename { get; private set; } = null;

        // Session props
        public string SessionHumidity
        {
            get => Session?.Humidity.ToString("F1") ?? "";
            set { if (_sessionIndex >= 0 && float.TryParse(value, out float v)) _sessions[_sessionIndex].Humidity = v; }
        }
        public string SessionInitialPause
        {
            get => Session?.Intervals.InitialPause.ToString("F1") ?? "";
            set { if (_sessionIndex >= 0 && float.TryParse(value, out float v)) _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { InitialPause = v }; }
        }

        public string SessionPulseDuration
        {
            get => Session?.Intervals.Pulse.ToString("F1") ?? "";
            set { if (_sessionIndex >= 0 && float.TryParse(value, out float v)) _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { Pulse = v }; }
        }

        public string SessionDMSDelay
        {
            get => Session?.Intervals.DmsDelay >= 0 ? Session?.Intervals.DmsDelay.ToString("F1") ?? "" : "";
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

        public string SessionFinalPause
        {
            get => Session?.Intervals.FinalPause.ToString("F1") ?? "";
            set { if (_sessionIndex >= 0 && float.TryParse(value, out float v)) _sessions[_sessionIndex].Intervals = _sessions[_sessionIndex].Intervals with { FinalPause = v }; }
        }

        // Pulses channel uses

        public bool PulseChannel1Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 1)?.Active ?? false;
            set => UpdateChannelUse(1, value);
        }

        public bool PulseChannel2Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 2)?.Active ?? false;
            set => UpdateChannelUse(2, value);
        }

        public bool PulseChannel3Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 3)?.Active ?? false;
            set => UpdateChannelUse(3, value);
        }

        public bool PulseChannel4Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 4)?.Active ?? false;
            set => UpdateChannelUse(4, value);
        }

        public bool PulseChannel5Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 5)?.Active ?? false;
            set => UpdateChannelUse(5, value);
        }
        /*
        public bool PulseChannel6Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 6)?.Active ?? false;
            set => UpdateChannelUse(6, value);
        }

        public bool PulseChannel7Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 7)?.Active ?? false;
            set => UpdateChannelUse(7, value); 
        }

        public bool PulseChannel8Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 8)?.Active ?? false;
            set => UpdateChannelUse(8, value);
        }

        public bool PulseChannel9Use
        {
            get => Channels?.FirstOrDefault(c => c.Id == 9)?.Active ?? false;
            set => UpdateChannelUse(9, value);
        }*/


        // Pulse channel flows

        public string PulseChannel1Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 1)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(1, value);
        }

        public string PulseChannel2Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 2)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(2, value);
        }

        public string PulseChannel3Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 3)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(3, value);
        }

        public string PulseChannel4Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 4)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(4, value);
        }

        public string PulseChannel5Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 5)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(5, value);
        }
        /*
        public string PulseChannel6Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 6)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(6, value);
        }

        public string PulseChannel7Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 7)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(7, value);
        }

        public string PulseChannel8Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 8)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(8, value);
        }

        public string PulseChannel9Flow
        {
            get => Channels?.FirstOrDefault(c => c.Id == 9)?.Flow.ToString("F2") ?? "";
            set => UpdateChannelFlow(9, value);
        }
        */

        public event PropertyChangedEventHandler? PropertyChanged;

        public PulseSetupEditor()
        {
            InitializeComponent();
            DataContext = this;
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

        List<SessionProps> _sessions = new ();
        int _sessionIndex = -1;
        int _pulseIndex = -1;

        SessionProps? Session => _sessionIndex >= 0 ? _sessions[_sessionIndex] : null;
        PulseChannelProps[]? Channels => _sessionIndex >= 0 && _pulseIndex >= 0 ? _sessions[_sessionIndex].Pulses[_pulseIndex].Channels : null;

        private void UpdateChannelFlow(int id, string value)
        {
            if (Channels != null && float.TryParse(value, out float v))
            {
                for (int i = 0; i < Channels.Length; i++)
                {
                    if (Channels[i].Id == id)
                    {
                        Channels[i] = Channels[i] with { Flow = v };
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

        // UI

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

            if (File.Exists("Properties\\" + newFilename))
            {
                MsgBox.Error(Title, $"File '{newFilename}' exists already");
                return;
            }

            Filename = "Properties\\" + newFilename;
            tblFileName.Text = Filename;

            lsvPulses.Items.Clear();
            lsvSessions.Items.Clear();

            _pulseIndex = -1;
            _sessionIndex = -1;
            _sessions = new List<SessionProps> { };

            SessionAdd_Click(this, new RoutedEventArgs());
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {

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
                session.AddPulse(new PulseProps(new PulseChannelProps[] {
                    new PulseChannelProps(1, 10f, true),
                    new PulseChannelProps(2, 0f, false),
                    new PulseChannelProps(3, 0f, false),
                    new PulseChannelProps(4, 0f, false),
                    new PulseChannelProps(5, 0f, false),
                    new PulseChannelProps(6, 0f, false),
                    new PulseChannelProps(7, 0f, false),
                    new PulseChannelProps(8, 0f, false),
                    new PulseChannelProps(9, 0f, false),
                }));
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

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel1Use)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel2Use)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel3Use)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel4Use)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel5Use)));
            /*
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel6Use)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel7Use)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel8Use)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel9Use)));
            */

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel1Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel2Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel3Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel4Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel5Flow)));
            /*
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel6Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel7Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel8Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PulseChannel9Flow)));
            */
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
