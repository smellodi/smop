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

    public float SniffingDelay
    {
        get => Properties.Settings.Default.Reproduction_SniffingDelay;
        set
        {
            Properties.Settings.Default.Reproduction_SniffingDelay = value;
            Properties.Settings.Default.Save();
        }
    }

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
            Style = FindResource("GasName") as Style
        };

        var txbFlow = new TextBox()
        {
            Style = FindResource("Value") as Style
        };


        var nameBinding = new Binding("Name")
        {
            Source = gas,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        BindingOperations.SetBinding(txbName, TextBox.TextProperty, nameBinding);

        var nameToBoolBinding = new Binding("Name")
        {
            Source = gas,
            Converter = new StringToBoolConverter()
        };
        BindingOperations.SetBinding(txbFlow, IsEnabledProperty, nameToBoolBinding);

        var nameBinding2 = new Binding("Flow")
        {
            Source = gas,
            StringFormat = "0.#"
        };
        nameBinding2.ValidationRules.Add(new Validators.RangeRule() { Min = 0, IsInteger = false });
        BindingOperations.SetBinding(txbFlow, TextBox.TextProperty, nameBinding2);


        var container = new Grid();
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40) });
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40) });

        Grid.SetColumn(lblID, 0);
        Grid.SetColumn(txbName, 1);
        Grid.SetColumn(txbFlow, 2);

        container.Children.Add(lblID);
        container.Children.Add(txbName);
        container.Children.Add(txbFlow);

        stpGases.Children.Add(container);
    }
}
