using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SMOP.Controls
{
    public partial class PauseDisplay : UserControl, INotifyPropertyChanged
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
            typeof(PauseDisplay),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as PauseDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(IsCurrent)))
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
            typeof(PauseDisplay),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as PauseDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Text)))
            ))
        );

        #endregion 

        #region Value property

        [Description("Indicator value"), Category("Common Properties")]
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(PauseDisplay),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(
                (s, e) => (s as PauseDisplay)?.PropertyChanged?.Invoke(s, new PropertyChangedEventArgs(nameof(Value)))
            ))
        );

        #endregion 

        public PauseDisplay()
        {
            InitializeComponent();

            IsCurrent = false;
            Text = "";
            Value = 0;
        }
    }
}
