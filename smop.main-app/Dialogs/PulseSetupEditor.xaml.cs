using Smop.MainApp.Controllers;
using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Smop.MainApp.Dialogs;

public partial class PulseSetupEditor : Window, INotifyPropertyChanged
{
    public SessionProps[] Sessions => _sessions.ToArray();

    public string? Filename { get; private set; } = null;

    // Session props
    public string SessionId
    {
        get => Session?.Id ?? $"{_sessions.Count + 1}";
        set => Session?.Id = string.IsNullOrEmpty(value) ? $"{_sessions.Count + 1}" : value;
    }
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
                Session.Intervals.InitialPause = v;
        }
    }

    public string SessionPulseDuration
    {
        get => Session?.Intervals.Pulse.ToString("F1") ?? "";
        set
        {
            if (Session != null && float.TryParse(value, out float v))
                Session.Intervals.Pulse = v;
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
                    Session.Intervals.DmsDelay = -1;
                }
                else if (float.TryParse(value, out float v))
                {
                    Session.Intervals.DmsDelay = v;
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
                        Session.Intervals.DmsDelay = _lastDmsDelay;
                    }
                }
                else
                {
                    _lastDmsDelay = Session.Intervals.DmsDelay >= 0 ? Session.Intervals.DmsDelay : 5f;
                    Session.Intervals.DmsDelay = -1;
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
                Session.Intervals.FinalPause = v;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PulseSetupEditor()
    {
        InitializeComponent();

        DialogTools.HideWindowButtons(this);
        DialogTools.SetCentralPosition(this);

        lsvSessions.ItemsSource = _sessions;

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
        var setup = PulseSetup.LoadFromFile(filename);
        if (setup != null)
        {
            Filename = filename;
            tblFileName.Text = filename;

            var sessions = setup.Sessions.ToList();
            foreach (var session in sessions)
            {
                _sessions.Add(session);
                //lsvSessions.Items.Add($"#{_sessions.Count}");
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

    readonly string[] FORBIDDEN_CHARS = [ " ", "=", ":", "\n", "\r" ];

    readonly List<(CheckBox, TextBox)> _pulseChannelControls = new();

    readonly Style? _pulseChannelFlowValid;
    readonly Style? _pulseChannelFlowInvalid;

    ObservableCollection<SessionProps> _sessions = [];
    int _sessionIndex = -1;
    int _pulseIndex = -1;
    float _lastDmsDelay = 5f;

    static Point _dragStartPoint;

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
                chk?.IsChecked = value > 0;
            }
            else
            {
                txb.Style = _pulseChannelFlowInvalid;
            }
        }
    }

    private static object? GetListViewItemUnderMouse(ListView listView, Point position)
    {
        HitTestResult hitTestResult = VisualTreeHelper.HitTest(listView, position);
        if (hitTestResult != null)
        {
            DependencyObject current = hitTestResult.VisualHit;
            while (current != null && current is not ListViewItem)
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return (current as ListViewItem)?.DataContext;
        }
        return null;
    }

    private static void OnMouseMove(MouseEventArgs e, ListView listview)
    {
        if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource.GetType() == typeof(TextBlock))
        {
            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (listview.SelectedItem != null)
                {
                    DragDrop.DoDragDrop(listview, listview.SelectedItem, DragDropEffects.Move);
                }
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

        Session?.Pulses.Clear();
        _sessions.Clear();

        _pulseIndex = -1;
        _sessionIndex = -1;
        _sessions = [];

        lsvSessions.ItemsSource = _sessions;

        SessionAdd_Click(this, new RoutedEventArgs());
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    // Sessions

    private void SessionAdd_Click(object sender, RoutedEventArgs e)
    {
        var newSession = new SessionProps((_sessions.Count + 1).ToString(), 40, new PulseIntervals(10, 60, 10, 10));
        var currentSession = Session;
        
        if (currentSession != null)
        {
            newSession.Humidity = currentSession.Humidity;
            newSession.Intervals = new PulseIntervals(
                currentSession.Intervals.InitialPause,
                currentSession.Intervals.Pulse,
                currentSession.Intervals.DmsDelay,
                currentSession.Intervals.FinalPause);
        }

        var pulseChannelProps = new List<PulseChannelProps>() { new(1, 10f, true) };
        for (int i = 2; i <= PulseChannels.MaxCount; i++)
        {
            pulseChannelProps.Add(new PulseChannelProps(i, 0f, false));
        }
        newSession.AddPulse(new PulseProps($"{newSession.Pulses.Count + 1}", pulseChannelProps.ToArray()));

        _sessions.Add(newSession);
        _sessionIndex = _sessions.Count - 1;

        lsvSessions.SelectedIndex = _sessionIndex;

        grdSession.IsEnabled = true;
        btnAddPulse.IsEnabled = true;
    }

    private void Sessions_SelectionChanged(object sender, SelectionChangedEventArgs? e)
    {
        _sessionIndex = lsvSessions.SelectedIndex;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionId)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionHumidity)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionInitialPause)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionPulseDuration)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionDMSDelay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseSessionDMS)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SessionFinalPause)));

        if (Session != null)
        {
            lsvPulses.SelectedIndex = -1;
            lsvPulses.ItemsSource = Session.Pulses;

            _pulseIndex = 0;
            lsvPulses.SelectedIndex = _pulseIndex;
        }
        else
        {
            lsvPulses.ItemsSource = null;
            _pulseIndex = -1;
        }
    }

    private void Sessions_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            if (_sessionIndex >= 0 && lsvSessions.Items.Count > 1)
            {
                var sessionIndex = Math.Min(_sessionIndex, _sessions.Count - 2);

                _sessions.RemoveAt(_sessionIndex);

                _sessionIndex = sessionIndex;
                lsvSessions.SelectedIndex = sessionIndex;
            }
        }
    }

    private void Sessions_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void Sessions_MouseMove(object sender, MouseEventArgs e)
    {
        OnMouseMove(e, lsvSessions);
    }

    private void Sessions_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(SessionProps)))
        {
            var droppedData = e.Data.GetData(typeof(SessionProps)) as SessionProps;
            var target = GetListViewItemUnderMouse(lsvSessions, e.GetPosition(lsvSessions));

            if (droppedData != null && lsvSessions.ItemsSource is ObservableCollection<SessionProps> sessions)
            {
                var targetIndex = target != null ? lsvSessions.Items.IndexOf(target) : lsvSessions.Items.Count;
                var sourceIndex = lsvSessions.Items.IndexOf(droppedData);

                if (sourceIndex != targetIndex)
                {
                    if (targetIndex < sourceIndex)
                    {
                        sessions.RemoveAt(sourceIndex);
                        sessions.Insert(targetIndex, droppedData);
                    }
                    else
                    {
                        sessions.Insert(targetIndex + 1, droppedData);
                        sessions.RemoveAt(sourceIndex);
                    }

                    lsvSessions.SelectedIndex = targetIndex < lsvSessions.Items.Count ? targetIndex : lsvSessions.Items.Count - 1;
                }
            }
        }
    }

    // Pulses

    private void PulseAdd_Click(object sender, RoutedEventArgs e)
    {
        var channels = new List<PulseChannelProps>();

        foreach (var channel in Channels)
        {
            channels.Add(new PulseChannelProps(channel.Id, channel.Flow, channel.Active));
        }

        if (Session != null)
        {
            Session.AddPulse(new PulseProps($"{Session.Pulses.Count + 1}", channels.ToArray()));
            _pulseIndex = Session.Pulses.Count - 1;

            lsvPulses.SelectedIndex = _pulseIndex;
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

    private void Pulses_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            var currentSession = Session;
            if (currentSession != null && _pulseIndex >= 0 && lsvPulses.Items.Count > 1)
            {
                int pulseIndex = Math.Min(_pulseIndex, currentSession.Pulses.Count - 2);
                currentSession.RemovePulse(_pulseIndex);

                _pulseIndex = pulseIndex;
                lsvPulses.SelectedIndex = pulseIndex;
            }
        }
    }

    private void Pulses_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void Pulses_MouseMove(object sender, MouseEventArgs e)
    {
        OnMouseMove(e, lsvPulses);
    }

    private void Pulses_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(PulseProps)))
        {
            var droppedData = e.Data.GetData(typeof(PulseProps)) as PulseProps;
            var target = GetListViewItemUnderMouse(lsvPulses, e.GetPosition(lsvPulses));

            if (droppedData != null && lsvPulses.ItemsSource is ObservableCollection<PulseProps> pulses)
            {
                var targetIndex = target != null ? lsvPulses.Items.IndexOf(target) : lsvPulses.Items.Count;
                var sourceIndex = lsvPulses.Items.IndexOf(droppedData);

                if (sourceIndex != targetIndex)
                {
                    var itemsSource = lsvPulses.ItemsSource as System.Collections.IList ?? lsvPulses.Items;
                    if (itemsSource != null)
                    {
                        if (targetIndex < sourceIndex)
                        {
                            pulses.RemoveAt(sourceIndex);
                            pulses.Insert(targetIndex, droppedData);
                        }
                        else
                        {
                            pulses.Insert(targetIndex + 1, droppedData);
                            pulses.RemoveAt(sourceIndex);
                        }

                        lsvPulses.SelectedIndex = targetIndex < lsvPulses.Items.Count ? targetIndex : lsvPulses.Items.Count - 1;
                    }
                }
            }
        }
    }

    private void SessionId_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var c in FORBIDDEN_CHARS)
        {
            if (e.Text.Contains(c))
            {
                e.Handled = true; // Mark the input as handled => it won't appear
                return;
            }
        }
    }

    private void SessionId_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Block spacebar key
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }

        // Optionally block '=' key directly (Shift + 0 on some layouts)
        if (e.Key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            e.Handled = true;
        }
    }
}
