using Smop.MainApp.Reproducer;
using System.Globalization;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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

    public int MaxIterations
    {
        get => Properties.Settings.Default.Reproduction_MaxIterations;
        set
        {
            Properties.Settings.Default.Reproduction_MaxIterations = value;
            Properties.Settings.Default.Save();
        }
    }

    public float Threshold
    {
        get => Properties.Settings.Default.Reproduction_Threshold;
        set
        {
            Properties.Settings.Default.Reproduction_Threshold = value;
            Properties.Settings.Default.Save();
        }
    }

    public bool SendPID
    {
        get => Properties.Settings.Default.Reproduction_UsePID;
        set
        {
            Properties.Settings.Default.Reproduction_UsePID = value;
            Properties.Settings.Default.Save();
        }
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

    public float Humidity
    {
        get => Properties.Settings.Default.Reproduction_Humidity;
        set
        {
            Properties.Settings.Default.Reproduction_Humidity = value;
            Properties.Settings.Default.Save();
        }
    }

    public event EventHandler<Gas>? GasNameChanged;

    public OdorReproductionSettings()
    {
        InitializeComponent();

        DataContext = this;
    }

    public void AddGas(Gas gas)
    {
        var lblID = new Label()
        {
            Content = "#" + gas.ChannelID.ToString()[4..]
        };

        var txbName = new TextBox()
        {
            Style = FindResource("GasName") as Style,
            ToolTip = "Enter a name of the odor loaded into this channel,\nor leave it blank if the channel is not used"
        };
        txbName.TextChanged += (s, e) => GasNameChanged?.Invoke(this, gas);

        var txbFlow = new TextBox()
        {
            Style = FindResource("Value") as Style
        };


        var nameBinding = new Binding(nameof(Gas.Name))
        {
            Source = gas,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        BindingOperations.SetBinding(txbName, TextBox.TextProperty, nameBinding);

        var nameToBoolBinding = new Binding(nameof(Gas.Name))
        {
            Source = gas,
            Converter = new StringToBoolConverter()
        };
        BindingOperations.SetBinding(txbFlow, IsEnabledProperty, nameToBoolBinding);

        var flowBinding = new Binding(nameof(Gas.Flow))
        {
            Source = gas,
            StringFormat = "0.#",
            Mode = BindingMode.TwoWay,
        };
        flowBinding.ValidationRules.Add(new Validators.RangeRule() { Min = 0, IsInteger = false });
        BindingOperations.SetBinding(txbFlow, TextBox.TextProperty, flowBinding);


        var container = new Grid();
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40) });
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(lblID, 0);
        Grid.SetColumn(txbName, 1);

        container.Children.Add(lblID);
        container.Children.Add(txbName);

        stpGases.Children.Add(container);

        stpFlows.Children.Add(txbFlow);
    }
}
