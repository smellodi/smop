using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SMOP.Controls
{
    public partial class CheckButton : UserControl, INotifyPropertyChanged
    {
        #region Text property

        [Description("Text"), Category("Common Properties")]
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(CheckButton),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(TextProperty_Changed)));

        private static void TextProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is CheckButton instance)
            {
                instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(Text)));
            }
        }

        #endregion 

        #region IsChecked property

        [Description("Is the button checked"), Category("Common Properties")]
        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            nameof(IsChecked),
            typeof(bool),
            typeof(CheckButton),
            new FrameworkPropertyMetadata(new PropertyChangedCallback(IsActiveProperty_Changed)));

        private static void IsActiveProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is CheckButton instance)
            {
                instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? GroupName { get; set; }
        public object? UserData { get; set; }

        public CheckButton()
        {
            InitializeComponent();
        }
    }
}
