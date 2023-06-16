using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Smop.PulseGen.Controls;

public partial class StageDisplay : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    #region IsCurrent property

    [Description("Is current"), Category("Common Properties")]
    public bool IsCurrent
    {
        get => (bool)GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    public static readonly DependencyProperty IsCurrentProperty = DependencyProperty.Register(
        nameof(IsCurrent),
        typeof(bool),
        typeof(StageDisplay),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as StageDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsCurrent)))
        ))
    );

    #endregion 

    #region Text property

    [Description("Indicator label"), Category("Common Properties")]
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StageDisplay),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as StageDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Text)))
        ))
    );

    #endregion 

    #region Duration property

    [Description("Duration, ms"), Category("Common Properties")]
    public int Duration
    {
        get => (int)GetValue(DurationProperty);
        set 
        {
            SetValue(DurationProperty, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationUnits)));
        }
    }

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration),
        typeof(int),
        typeof(StageDisplay),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as StageDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Duration)))
        ))
    );

    #endregion 

    #region Delay property

    [Description("Start delay, ms"), Category("Common Properties")]
    public int Delay
    {
        get => (int)GetValue(DelayProperty);
        set
        {
            SetValue(DelayProperty, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayUnits)));
        }
    }

    public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(
        nameof(Delay),
        typeof(int),
        typeof(StageDisplay),
        new FrameworkPropertyMetadata(new PropertyChangedCallback(
            (s, e) => (s as StageDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Delay)))
        ))
    );

    #endregion 

    public string DurationValue => Duration > 0 ? IntervalToStr(Duration, out _durationUnitsAreMs) : "[ inactive ]";
    public string DelayValue => IntervalToStr(Delay, out _delayUnitsAreMs);
    public string DurationUnits => Duration > 0 ? (_durationUnitsAreMs ? "ms" : "seconds") : "";
    public string DelayUnits => _delayUnitsAreMs ? "ms" : "seconds";


    public StageDisplay()
    {
        InitializeComponent();

        IsCurrent = false;
        Text = "";
        Duration = 0;
        Delay = 0;
    }

    // Internal

    bool _durationUnitsAreMs = true;
    bool _delayUnitsAreMs = true;

    private string IntervalToStr(int ms, out bool isShownAsMs)
    {
        isShownAsMs = ms < 1000;
        return isShownAsMs ? ms.ToString() : ((double)ms / 1000).ToString("0.##");
    }
}
