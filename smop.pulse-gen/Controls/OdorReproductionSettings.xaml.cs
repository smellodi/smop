using Smop.PulseGen.Reproducer;
using System.Windows;
using System.Windows.Controls;

namespace Smop.PulseGen.Controls;

public partial class OdorReproductionSettings : UserControl
{
    public OdorReproductionSettings()
    {
        InitializeComponent();
    }

    public void AddGas(Gas gas)
    {
        var txb = new TextBox()
        {
            FontSize = 14,
            Text = gas.Name
        };
        txb.TextChanged += (s, e) => gas.Name = txb.Text;

        var style = FindResource("Setting") as Style;
        var uc = new UserControl()
        {
            Style = style,
            Tag = "#" + gas.ChannelID.ToString()[^1],
            Content = txb
        };

        stpGases.Children.Add(uc);
    }

    // Internal

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;
        txbMaxIterations.Text = settings.Reproduction_MaxIterations.ToString();
        txbThreshold.Text = settings.Reproduction_Threshold.ToString("F4");
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
            txbThreshold.Text = settings.Reproduction_Threshold.ToString("F4");
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
