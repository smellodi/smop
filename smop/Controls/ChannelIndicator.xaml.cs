using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Smop.Controls
{
    public partial class ChannelIndicator : UserControl, INotifyPropertyChanged
    {
        public enum DataSource { MFC, Valve, SourceTemp, ChassisHeater, MFCDryAir, InputHumidity, OutputHumidity, Pressure, PID }

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

        [Description("Data source"), Category("Common Properties")]
        public DataSource Source
        {
            get => (DataSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(DataSource),
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

        public string ValueStr => double.IsFinite(Value) ? Value.ToString($"F{Precision}") : "-";
        public bool IsWarningVisible => double.IsFinite(WarningThreshold) && double.IsFinite(Value) && WarningThreshold < Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ChannelIndicator()
        {
            InitializeComponent();
            WarningThreshold = double.PositiveInfinity;
        }
    }
}
