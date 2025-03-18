using Smop.MainApp.Utils.Extensions;
using Smop.OdorDisplay;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Smop.MainApp.Controls;

public partial class ChannelIndicator : UserControl, INotifyPropertyChanged
{
    #region Title property

    [Description("Title"), Category("Common Properties")]
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(ChannelIndicator),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(TitleProperty_Changed)));

    private static void TitleProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ChannelIndicator instance)
        {
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    #endregion

    #region Value property

    [Description("Value"), Category("Common Properties")]
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(ChannelIndicator),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(ValueProperty_Changed)));

    private static void ValueProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ChannelIndicator instance)
        {
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(Value)));
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(ValueStr)));
        }
    }

    #endregion

    #region Units property

    [Description("Value"), Category("Common Properties")]
    public string Units
    {
        get => (string)GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    public static readonly DependencyProperty UnitsProperty = DependencyProperty.Register(
        nameof(Units),
        typeof(string),
        typeof(ChannelIndicator),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(UnitsProperty_Changed)));

    private static void UnitsProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ChannelIndicator instance)
        {
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(Units)));
        }
    }

    #endregion

    #region IsActive property

    [Description("Is the indicator active"), Category("Common Properties")]
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(ChannelIndicator),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(IsActiveProperty_Changed)));

    private static void IsActiveProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ChannelIndicator instance)
        {
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }

    #endregion

    #region Precision property

    [Description("Precision"), Category("Common Properties")]
    public int Precision
    {
        get => (int)GetValue(PrecisionProperty);
        set => SetValue(PrecisionProperty, Math.Max(0, value));
    }

    public static readonly DependencyProperty PrecisionProperty = DependencyProperty.Register(
        nameof(Precision),
        typeof(int),
        typeof(ChannelIndicator),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(PrecisionProperty_Changed)));

    private static void PrecisionProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ChannelIndicator instance)
        {
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(Precision)));
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(ValueStr)));
        }
    }

    #endregion

    #region Source property

    [Description("Data source ID"), Category("Common Properties")]
    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(ChannelIndicator));

    #endregion

    #region Warning threshold property

    [Description("Warning threshold"), Category("Common Properties")]
    public double WarningThreshold
    {
        get => (double)GetValue(WarningThresholdProperty);
        set => SetValue(WarningThresholdProperty, value);
    }

    public static readonly DependencyProperty WarningThresholdProperty = DependencyProperty.Register(
        nameof(WarningThreshold),
        typeof(double),
        typeof(ChannelIndicator),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(WarningThresholdProperty_Changed)));

    private static void WarningThresholdProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ChannelIndicator instance)
        {
            instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(IsWarningVisible)));
        }
    }

    #endregion

    #region ChannelCount property

    [Description("ChannelCount"), Category("Common Properties")]
    public int ChannelCount
    {
        get => (int)GetValue(ChannelCountProperty);
        set => SetValue(ChannelCountProperty, value);
    }

    public static readonly DependencyProperty ChannelCountProperty = DependencyProperty.Register(
        nameof(ChannelCount),
        typeof(int),
        typeof(ChannelIndicator),
        new FrameworkPropertyMetadata(new PropertyChangedCallback((s, e) =>
            {
                if (s is ChannelIndicator channelIndicator)
                {
                    channelIndicator.cmdChannels.Items.Clear();
                    for (int i = 0; i < channelIndicator.ChannelCount; i++)
                    {
                        channelIndicator.cmdChannels.Items.Add($"{i + 1}");
                    }
                    if (channelIndicator.ChannelCount > 0)
                    {
                        channelIndicator.cmdChannels.SelectedIndex = 0;
                    }
                    channelIndicator.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(ChannelCount)));
                }
            }
        ))
    );

    #endregion

    public bool HasLeftBorder
    {
        get => _hasLeftBorder;
        set
        {
            _hasLeftBorder = value;
            lblMain.Style = (_hasLeftBorder ? FindResource("Channel") : FindResource("NoLeftBorder")) as Style;
        }
    }

    public bool IsThermistor => Units.Contains("°");
    public bool IsMonometer => Units.Contains("mBar");

    public SolidColorBrush HeaderColor => new(ChannelID switch
    {
        Device.ID.Base => (Color)Application.Current.FindResource("ColorLight"),
        Device.ID.DilutionAir => ((Color)Application.Current.FindResource("ColorLight")).Darker(15),
        Device.ID.Odor1 => (Color)Application.Current.FindResource("ColorLightDarker"),
        Device.ID.Odor2 => (Color)Application.Current.FindResource("ColorDarkLighter"),
        Device.ID.Odor3 => (Color)Application.Current.FindResource("ColorDark"),
        Device.ID.Odor4 => (Color)Application.Current.FindResource("ColorDarkDarker"),
        Device.ID.Odor5 => (Color)Application.Current.FindResource("ColorLightDarker"),
        Device.ID.Odor6 => (Color)Application.Current.FindResource("ColorDarkLighter"),
        Device.ID.Odor7 => (Color)Application.Current.FindResource("ColorDark"),
        Device.ID.Odor8 => (Color)Application.Current.FindResource("ColorDarkDarker"),
        Device.ID.Odor9 => (Color)Application.Current.FindResource("ColorLight"),
        _ => Colors.White
    });

    public Device.ID ChannelID { get; init; }

    public string ValueStr => double.IsFinite(Value) ? Value.ToString($"F{Precision}") : "-";
    public bool IsWarningVisible => double.IsFinite(WarningThreshold) && double.IsFinite(Value) && WarningThreshold < Value;

    public event EventHandler<int>? ChannelIdChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ChannelIndicator()
    {
        InitializeComponent();
        WarningThreshold = double.PositiveInfinity;

        cmdChannels.SelectionChanged += Channels_SelectionChanged;
    }

    // Internal

    bool _hasLeftBorder = true;

    private void Channels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ChannelIdChanged?.Invoke(this, int.Parse((string)cmdChannels.SelectedItem) - 1);
    }
}
