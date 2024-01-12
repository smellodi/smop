using Smop.MainApp.Reproducer;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class OdorReproductionSettings : UserControl
{
    public OdorReproductionSettings()
    {
        InitializeComponent();
    }

    public void AddGas(Gas gas)
    {
        var lblID = new Label()
        {
            Content = "#" + gas.ChannelID.ToString()[4..]
        };

        var txbName = new TextBox()
        {
            FontSize = 14,
            Text = gas.Name,
            Margin = new Thickness(4, 0, 4, 0)
        };
        txbName.TextChanged += (s, e) => gas.Name = txbName.Text;

        var txbFlow = new TextBox()
        {
            FontSize = 14,
            Text = gas.Flow.ToString("0.#")
        };
        txbFlow.TextChanged += (s, e) =>
        {
            if (float.TryParse(txbFlow.Text, out float flow) && flow >= 0)
            {
                gas.Flow = flow;
            }
        };

        /*
        var ucStyle = FindResource("Setting") as Style;
        var uc = new UserControl()
        {
            Style = ucStyle,
            Tag = "#" + gas.ChannelID.ToString()[^1],
            Content = txbName
        };

        stpGases.Children.Add(uc);
        */

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

    // Internal

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;
        txbMaxIterations.Text = settings.Reproduction_MaxIterations.ToString();
        txbThreshold.Text = settings.Reproduction_Threshold.ToString("0.####");
        chkSendPID.IsChecked = settings.Reproduction_UsePID;
        txbSniffingDelay.Text = settings.Reproduction_SniffingDelay.ToString();
    }

    private void MaxIterations_TextChanged(object sender, TextChangedEventArgs e)
    {
        var settings = Properties.Settings.Default;

        if (int.TryParse(txbMaxIterations.Text, out int value) && value >= 0 && value < 100)
        {
            settings.Reproduction_MaxIterations = value;
            settings.Save();
        }
        else
        {
            txbMaxIterations.Text = settings.Reproduction_MaxIterations.ToString();
        }
    }

    private void Threshold_TextChanged(object sender, TextChangedEventArgs e)
    {
        var settings = Properties.Settings.Default;

        if (float.TryParse(txbThreshold.Text, out float value) && value >= 0)
        {
            settings.Reproduction_Threshold = value;
            settings.Save();
        }
        else
        {
            txbThreshold.Text = settings.Reproduction_Threshold.ToString("0.####");
        }
    }

    private void SendPID_Click(object sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;
        settings.Reproduction_UsePID = chkSendPID.IsChecked ?? false;
        settings.Save();
    }

    private void SniffingDelay_TextChanged(object sender, TextChangedEventArgs e)
    {
        var settings = Properties.Settings.Default;

        if (float.TryParse(txbSniffingDelay.Text, out float value) && value > 0)
        {
            settings.Reproduction_SniffingDelay = value;
            settings.Save();
        }
        else
        {
            txbThreshold.Text = settings.Reproduction_SniffingDelay.ToString();
        }
    }
}
