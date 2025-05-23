﻿using Smop.MainApp.Utils;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class WaitingInstruction : UserControl, INotifyPropertyChanged, IDisposable
{
    public class TimeUpdatedEventArgs(double passed, double remaining) : EventArgs
    {
        /// <summary>
        /// Time in seconds passed since the progress has started
        /// </summary>
        public double Passed => passed;

        /// <summary>
        /// Time in seconds remaining until the progress is finished
        /// </summary>
        public double Remaining => remaining;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<TimeUpdatedEventArgs>? TimeUpdated;

    #region Text property

    [Description("Instruction text"), Category("Common Properties")]
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(WaitingInstruction),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as WaitingInstruction)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Text)))
        ))
    );

    #endregion

    /// <summary>
    /// Waiting time in seconds
    /// </summary>
    public double WaitingTime
    {
        get => prbProgress.Maximum;
        set
        {
            double val = Math.Max(0, value);
            prbProgress.Maximum = val;
            prbProgress.Value = val * Progress;
        }
    }

    /// <summary>
    /// Value in the range 0..1
    /// </summary>
    public double Progress
    {
        get => prbProgress.Maximum > 0 ? prbProgress.Value / prbProgress.Maximum : 0;
        set
        {
            double val = Math.Max(0, Math.Min(value, 1));
            prbProgress.Value = WaitingTime * val;
            prbProgress.Visibility = val > 0 ? Visibility.Visible : Visibility.Hidden;
        }
    }

    public WaitingInstruction()
    {
        InitializeComponent();

        Unloaded += WaitingInstruction_Unloaded;

        _timer.Interval = UPDATE_INTERVAL * 1000;
        _timer.AutoReset = true;
        _timer.Elapsed += (s, e) => { try { Dispatcher.Invoke(UpdateProgress); } catch { } };
    }

    /// <summary>
    /// Starts waiting progress
    /// </summary>
    /// <param name="duration">Waiting time. If not set, or it is not positive value, then <see cref="WaitingTime"/> value is used</param>
    public void Start(double duration = 0)
    {
        Reset();

        Visibility = Visibility.Visible;

        if (duration > 0)
        {
            WaitingTime = duration;
        }

        prbProgress.Visibility = Visibility.Visible;

        _start = Timestamp.Sec;

        _timer.Start();
    }

    /// <summary>
    /// Sets the progress bar to 0 and hides it
    /// </summary>
    public void Reset()
    {
        prbProgress.Visibility = Visibility.Hidden;
        _timer.Stop();
        Progress = 0;
    }

    /// <summary>
    /// Resets the progress bar and hides the control
    /// </summary>
    public void Hide()
    {
        Reset();
        Visibility = Visibility.Hidden;
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    const double UPDATE_INTERVAL = 0.1;

    readonly System.Timers.Timer _timer = new();

    double _start = 0;

    private void UpdateProgress()
    {
        double duration = Timestamp.Sec - _start;
        double progress = Math.Min(1.0, duration / WaitingTime);

        Progress = progress;

        TimeUpdated?.Invoke(this, new TimeUpdatedEventArgs(duration, WaitingTime - duration));

        if (progress >= 1.0)
        {
            _timer.Stop();
        }
    }

    // Events handlers

    private void WaitingInstruction_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }
}
