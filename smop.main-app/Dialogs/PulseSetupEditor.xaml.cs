using Smop.MainApp.Controllers;
using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Dialogs;

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
            if (Session != null && float.TryParse(value, out float v))
                Session.Humidity = v;
        }
    }
    public string SessionInitialPause
    {
        get => Session?.Intervals.InitialPause.ToString("F1") ?? "";
        set
        {
            if (Session != null && float.TryParse(value, out float v))
                Session.Intervals = Session.Intervals with { InitialPause = v };
        }
    }

    public string SessionPulseDuration
    {
        get => Session?.Intervals.Pulse.ToString("F1") ?? "";
        set
        {
            if (Session != null && float.TryParse(value, out float v))
                Session.Intervals = Session.Intervals with { Pulse = v };
        }
    }

    public string SessionDMSDelay
    {
        get => Session?.Intervals.DmsDelay >= 0 ? Session.Intervals.DmsDelay.ToString("F1") : "";
        set
        {
            if (Session != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    Session.Intervals = Session.Intervals with { DmsDelay = -1 };
                }
                else if (float.TryParse(value, out float v))
                {
                    Session.Intervals = Session.Intervals with { DmsDelay = v };
                }
            }
        }
    }

    public bool UseSessionDMS
    {
        get => Session?.Intervals.DmsDelay >= 0;
        set
        {
            if (Session != null)
            {
                if (value)
                {
                    if (Session.Intervals.DmsDelay < 0)
                    {
                        Session.Intervals = Session.Intervals with { DmsDelay = _lastDmsDelay };
                    }
                }
                else
                {
                    _lastDmsDelay = Session.Intervals.DmsDelay >= 0 ? Session.Intervals.DmsDelay : 5f;
                    Session.Intervals = Session.Intervals with { DmsDelay = -1 };
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
            if (Session != null && float.TryParse(value, out float v))
                Session.Intervals = Session.Intervals with { FinalPause = v };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PulseSetupEditor()
    {
        InitializeComponent();

        DialogTools.HideWindowButtons(this);
        DialogTools.SetCentralPosition(this);

        _pulseChannelFlowValid = FindResource("TextBoxWithoutError") as Style;
        _pulseChannelFlowInvalid = FindResource("TextBoxWithError") as Style;

        var channels = new OdorChannels();

        for (int i = 1; i <= PulseChannels.MaxCount; i++)
        {
            var id = i.ToString();

            var channelID = Enum.Parse<OdorDisplay.Device.ID>($"Odor{i}");
            var channel = channels.FirstOrDefault(c => c.ID == channelID);

            grdPulse.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            var label = new Label();
            if (channel?.Name.Length > 0)
            {
                label.Content = channel.Name;
            }
            else
            {
                label.Content = id;
            }
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            grdPulse.Children.Add(label);

            var chk = new CheckBox() { Tag = id, Width = 26, IsTabStop = false };
            chk.Checked += PulseChannelUse_CheckedChanged;
            chk.Unchecked += PulseChannelUse_CheckedChanged;
            Grid.SetRow(chk, i);
            Grid.SetColumn(chk, 1);
            grdPulse.Children.Add(chk);

            var txb = new TextBox() { Tag = id };
            txb.TextChanged += PulseChannelFlow_TextChanged;
            Grid.SetRow(txb, i);
            Grid.SetColumn(txb, 2);

            txb.Style = _pulseChannelFlowValid;

            grdPulse.Children.Add(txb);

            _pulseChannelControls.Add((chk, txb));
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

    readonly List<(CheckBox, TextBox)> _pulseChannelControls = new();

    readonly Style? _pulseChannelFlowValid;
    readonly Style? _pulseChannelFlowInvalid;

    List<SessionProps> _sessions = new();
    int _sessionIndex = -1;
    int _pulseIndex = -1;
    float _lastDmsDelay = 5f;

    SessionProps? Session => _sessionIndex >= 0 ? _sessions[_sessionIndex] : null;
    PulseChannelProps[] Channels => Session != null && _pulseIndex >= 0 ? Session.Pulses[_pulseIndex].Channels : Array.Empty<PulseChannelProps>();

    private void UpdateChannelFlow(int id, float value)
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

    private void UpdateChannelUse(int id, bool value)
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

                var (chk, _) = _pulseChannelControls.FirstOrDefault(kv => kv.Item2 == txb);
                if (chk != null)
                    chk.IsChecked = value > 0;
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

        var settings = Properties.Settings.Default;
        settings.PulseEditor_Height = ActualHeight;
        settings.Save();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        Height = Properties.Settings.Default.PulseEditor_Height;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var newFilename = InputBox.Show(Title, "Type file name for the new setup:");
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

        var folder = Path.GetDirectoryName(Filename);

        var pathToNewFilename = Path.Combine(folder ?? "Assets/pulses", newFilename);
        if (File.Exists(pathToNewFilename))
        {
            MsgBox.Error(Title, $"File '{newFilename}' exists already");
            return;
        }

        Filename = pathToNewFilename;
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
        var newSession = new SessionProps(40, new PulseIntervals(10, 60, 10, 10));
        var currentSession = Session;
        
        if (currentSession != null)
        {
            newSession.Humidity = currentSession.Humidity;
            newSession.Intervals = currentSession.Intervals with { };
        }

        /*
        if (currentSession != null)     // lets copy pulse to the newly created session
        {
            foreach (var pulse in currentSession.Pulses)
            {
                List<PulseChannelProps> channels = new();
                foreach (var channel in pulse.Channels)
                {
                    channels.Add(new PulseChannelProps(channel.Id, channel.Flow, channel.Active));
                }
                newSession.AddPulse(new PulseProps(channels.ToArray()));
            }
        }
        else      // if there is no current session, we must create one pulse
        {*/     
        var pulseChannelProps = new List<PulseChannelProps>() { new(1, 10f, true) };
        for (int i = 2; i <= PulseChannels.MaxCount; i++)
        {
            pulseChannelProps.Add(new PulseChannelProps(i, 0f, false));
        }
        newSession.AddPulse(new PulseProps(pulseChannelProps.ToArray()));
        //}

        _sessions.Add(newSession);
        _sessionIndex = _sessions.Count - 1;

        lsvSessions.Items.Add($"#{_sessions.Count}");
        lsvSessions.SelectedIndex = _sessionIndex;

        grdSession.IsEnabled = true;
        btnAddPulse.IsEnabled = true;
    }

    private void PulseAdd_Click(object sender, RoutedEventArgs e)
    {
        var channels = new List<PulseChannelProps>();

        foreach (var channel in Channels)
        {
            channels.Add(new PulseChannelProps(channel.Id, channel.Flow, channel.Active));
        }

        if (Session != null)
        {
            Session.AddPulse(new PulseProps(channels.ToArray()));
            _pulseIndex = Session.Pulses.Length - 1;

            lsvPulses.Items.Add($"#{Session.Pulses.Length}");
            lsvPulses.SelectedIndex = _pulseIndex;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Sessions_SelectionChanged(object sender, SelectionChangedEventArgs? e)
    {
        if (Session == null)
            return;

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
            foreach (var pulse in Session.Pulses)
            {
                lsvPulses.Items.Add($"#{i + 1}");
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
                var sessionIndex = Math.Min(_sessionIndex, _sessions.Count - 1);

                _sessionIndex = -1;     // to disable UI update
                lsvSessions.Items.RemoveAt(lsvSessions.SelectedIndex);

                _sessionIndex = sessionIndex;
                lsvSessions.SelectedIndex = sessionIndex;   // funny that this line does not trigger Sessions_SelectionChanged automatically
                Sessions_SelectionChanged(sender, null);
            }
        }
    }

    private void Pulses_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _pulseIndex = lsvPulses.SelectedIndex;

        grdPulse.IsEnabled = Session != null;

        foreach (var (chk, txb) in _pulseChannelControls)
        {
            var id = int.Parse((string)chk.Tag);
            chk.IsChecked = Channels.FirstOrDefault(c => c.Id == id)?.Active ?? false;
            txb.Text = Channels.FirstOrDefault(c => c.Id == id)?.Flow.ToString("F2") ?? "";
            txb.Style = _pulseChannelFlowValid;
        }
    }

    private void Pulses_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete)
        {
            var currentSession = Session;
            if (currentSession != null && _pulseIndex >= 0 && lsvPulses.Items.Count > 1)
            {
                currentSession.RemovePulse(_pulseIndex);
                int pulseIndex = Math.Min(_pulseIndex, currentSession.Pulses.Length - 1);

                _pulseIndex = -1;
                lsvPulses.Items.RemoveAt(lsvPulses.SelectedIndex);

                _pulseIndex = pulseIndex;
                lsvPulses.SelectedIndex = pulseIndex;
            }
        }
    }
}
