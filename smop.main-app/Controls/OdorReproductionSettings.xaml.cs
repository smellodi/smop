using Smop.MainApp.Controllers;
using System.Globalization;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Smop.MainApp.Logging;

namespace Smop.MainApp.Controls;

[ValueConversion(typeof(string), typeof(bool))]
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string str = (string)value;
        return !string.IsNullOrWhiteSpace(str);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class OdorReproductionSettings : UserControl
{
    public string MLStatus
    {
        get => tblMLStatus.Text;
        set => tblMLStatus.Text = value;
    }

    public bool IsMLConnected
    {
        get => cclMLConnIndicator.IsConnected;
        set => cclMLConnIndicator.IsConnected = value;
    }

    public static int MaxIterations
    {
        get => Properties.Settings.Default.Reproduction_MaxIterations;
        set
        {
            Properties.Settings.Default.Reproduction_MaxIterations = value;
            Properties.Settings.Default.Save();
        }
    }

    public static float Threshold
    {
        get => Properties.Settings.Default.Reproduction_Threshold;
        set
        {
            Properties.Settings.Default.Reproduction_Threshold = value;
            Properties.Settings.Default.Save();
        }
    }

    public static bool SendPID
    {
        get => Properties.Settings.Default.Reproduction_UsePID;
        set
        {
            Properties.Settings.Default.Reproduction_UsePID = value;
            Properties.Settings.Default.Save();
        }
    }

    public static bool UseDmsCache
    {
        get => DmsCache.IsEnabled;
        set => DmsCache.IsEnabled = value;
    }

    /*public float SniffingDelay
    {
        get => Properties.Settings.Default.Reproduction_SniffingDelay;
        set
        {
            Properties.Settings.Default.Reproduction_SniffingDelay = value;
            Properties.Settings.Default.Save();
        }
    }*/

    public static float Humidity
    {
        get => Properties.Settings.Default.Reproduction_Humidity;
        set
        {
            Properties.Settings.Default.Reproduction_Humidity = value;
            Properties.Settings.Default.Save();
        }
    }

    public event EventHandler<OdorChannel>? OdorNameChanged;

    public OdorReproductionSettings()
    {
        InitializeComponent();

        DataContext = this;
    }

    public void AddOdorChannel(OdorChannel odorChannel)
    {
        var lblID = new Label()
        {
            Content = "#" + odorChannel.ID.ToString()[4..]
        };

        var txbName = new TextBox()
        {
            Style = FindResource("OdorName") as Style,
            ToolTip = "Enter a name of the odor loaded into this channel,\nor leave it blank if the channel is not used"
        };
        txbName.TextChanged += (s, e) => OdorNameChanged?.Invoke(this, odorChannel);

        var txbFlow = new TextBox()
        {
            Style = FindResource("Value") as Style
        };


        var nameBinding = new Binding(nameof(OdorChannel.Name))
        {
            Source = odorChannel,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        BindingOperations.SetBinding(txbName, TextBox.TextProperty, nameBinding);

        var nameToBoolBinding = new Binding(nameof(OdorChannel.Name))
        {
            Source = odorChannel,
            Converter = new StringToBoolConverter()
        };
        BindingOperations.SetBinding(txbFlow, IsEnabledProperty, nameToBoolBinding);

        var flowBinding = new Binding(nameof(OdorChannel.Flow))
        {
            Source = odorChannel,
            StringFormat = "0.#",
            Mode = BindingMode.TwoWay,
        };
        flowBinding.ValidationRules.Add(new Utils.RangeRule() { Min = 0, IsInteger = false });
        BindingOperations.SetBinding(txbFlow, TextBox.TextProperty, flowBinding);


        var container = new Grid();
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40) });
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(lblID, 0);
        Grid.SetColumn(txbName, 1);

        container.Children.Add(lblID);
        container.Children.Add(txbName);

        stpOdorChannels.Children.Add(container);

        stpFlows.Children.Add(txbFlow);
    }
}
