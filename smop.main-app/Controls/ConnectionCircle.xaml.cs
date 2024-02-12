using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Smop.MainApp.Controls;

public partial class ConnectionCircle : UserControl, INotifyPropertyChanged
{
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IndicatorColor)));
        }
    }

    public Brush IndicatorColor => _isConnected ?
        new SolidColorBrush(Color.FromRgb(32, 160, 32)) :
        new SolidColorBrush(Color.FromRgb(160, 160, 160));

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConnectionCircle()
    {
        InitializeComponent();
        DataContext = this;
    }

    // Internal
    bool _isConnected = false;
}
