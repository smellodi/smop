using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class StageDisplay : UserControl, INotifyPropertyChanged
{
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

    [Description("Duration, s"), Category("Common Properties")]
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

    #region Flow property

    [Description("Flow, ccm"), Category("Common Properties")]
    public double Flow
    {
        get => (double)GetValue(FlowProperty);
        set
        {
            SetValue(FlowProperty, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowValue)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowUnits)));
        }
    }

    public static readonly DependencyProperty FlowProperty = DependencyProperty.Register(
        nameof(Flow),
        typeof(double),
        typeof(StageDisplay),
        new FrameworkPropertyMetadata(-1d, new PropertyChangedCallback(
            (s, e) => (s as StageDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Flow)))
        ))
    );

    #endregion

    public string DurationValue => Duration > 0 ? IntervalToStr(Duration, out _durationUnitsAreMs) : "";
    public string FlowValue => Flow >= 0 ? FlowToStr(Flow, out _flowUnitsAreCcm) : "";
    public string DurationUnits => Duration > 0 ? (_durationUnitsAreMs ? "ms" : "seconds") : "";
    public string FlowUnits => Flow >= 0 ? (_flowUnitsAreCcm ? "ccm" : "l/min") : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public StageDisplay()
    {
        InitializeComponent();
    }

    // Internal

    bool _durationUnitsAreMs = true;
    bool _flowUnitsAreCcm = true;

    private static string IntervalToStr(int ms, out bool isShownAsMs)
    {
        isShownAsMs = ms < 1000;
        return isShownAsMs ? ms.ToString() : ((double)ms / 1000).ToString("0.##");
    }

    private static string FlowToStr(double ccm, out bool isShownAsCcm)
    {
        isShownAsCcm = ccm < 1000;
        return isShownAsCcm ? ccm.ToString("0.##") : ((double)ccm / 1000).ToString("0.##");
    }
}
