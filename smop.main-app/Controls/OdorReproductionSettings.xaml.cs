using Smop.MainApp.Controllers;
using Smop.MainApp.Dialogs;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Smop.MainApp.Controls;

[ValueConversion(typeof(string), typeof(bool))]
file class StringToBoolConverter : IValueConverter
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
    [Flags]
    public enum MeasurementSouce { None = 0, DMS = 1, SNT = 2 };

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

    public static string CmdParams
    {
        get => Properties.Settings.Default.Reproduction_ML_CmdParams;
        set
        {
            Properties.Settings.Default.Reproduction_ML_CmdParams = value;
            Properties.Settings.Default.Save();
        }
    }

    public static string Algorithm
    {
        get => Properties.Settings.Default.Reproduction_ML_Algorithm;
        set
        {
            Properties.Settings.Default.Reproduction_ML_Algorithm = value;
            Properties.Settings.Default.Save();
        }
    }

    public static int MaxIterations
    {
        get => Properties.Settings.Default.Reproduction_ML_MaxIterations;
        set
        {
            Properties.Settings.Default.Reproduction_ML_MaxIterations = value;
            Properties.Settings.Default.Save();
        }
    }

    public static float Threshold
    {
        get => Properties.Settings.Default.Reproduction_ML_Threshold;
        set
        {
            Properties.Settings.Default.Reproduction_ML_Threshold = value;
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

    public static int SntSampleCount
    {
        get => Properties.Settings.Default.Reproduction_SntSampleCount;
        set
        {
            Properties.Settings.Default.Reproduction_SntSampleCount = value;
            Properties.Settings.Default.Save();
        }
    }

    public static float DmsSingleSV
    {
        get => Properties.Settings.Default.Reproduction_DmsSingleSV;
        set
        {
            Properties.Settings.Default.Reproduction_DmsSingleSV = value;
            Properties.Settings.Default.Save();
        }
    }

    /*public static bool UseDmsCache
    {
        get => Logging.DmsCache.IsEnabled;
        set => Logging.DmsCache.IsEnabled = value;
    }*/

    /*public float SniffingDelay
    {
        get => Properties.Settings.Default.Reproduction_SniffingDelay;
        set
        {
            Properties.Settings.Default.Reproduction_SniffingDelay = value;
            Properties.Settings.Default.Save();
        }
    }*/

    public event EventHandler<OdorChannel>? OdorNameChanging;
    public event EventHandler<OdorChannel>? OdorNameChanged;

    public OdorReproductionSettings()
    {
        InitializeComponent();

        cmbAlgorithm.ItemsSource = Enum.GetNames(typeof(ML.Algorithm));
        cmbAlgorithm.SelectedValue = nameof(ML.Algorithm.Euclidean);

        DataContext = this;

        txbCmdParams.IsEnabled = ML.Communicator.CanLaunchML;
    }

    public void SetMeasurementSource(MeasurementSouce sources)
    {
        uscSntSampleCount.Visibility = sources.HasFlag(MeasurementSouce.SNT) ? Visibility.Visible : Visibility.Collapsed;
        uscDmsSingleSV.Visibility = sources.HasFlag(MeasurementSouce.DMS) ? Visibility.Visible : Visibility.Collapsed;
        //uscUseDmsCache.Visibility = sources.HasFlag(MeasurementSouce.DMS) ? Visibility.Visible : Visibility.Collapsed;
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
        txbName.TextChanged += (s, e) =>
        {
            ShowOdorNameSuggestions(txbName);
            OdorNameChanging?.Invoke(this, odorChannel);
        };
        txbName.LostFocus += (s, e) => OdorNameChanged?.Invoke(this, odorChannel);

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

    // Internal

    readonly KnownOdors _knownOdors = new();

    string _currentInput = "";
    string? _currentSuggestion = null;

    private void ShowOdorNameSuggestions(TextBox txb)
    {
        var input = txb.Text.ToLower();
        var inputLength = input.Length;

        if (input.Length > _currentInput.Length && input != _currentSuggestion)
        {
            _currentSuggestion = _knownOdors.GetFullName(input);
            if (_currentSuggestion != null)
            {
                var currentText = txb.Text + _currentSuggestion[inputLength..];
                var selectionStart = inputLength;
                var selectionLength = _currentSuggestion.Length - inputLength;

                txb.Text = currentText;
                txb.Select(selectionStart, selectionLength);
            }
        }

        _currentInput = input;
    }

    // UI

    private void PauseEstimator_Click(object sender, RoutedEventArgs e)
    {
        PauseEstimatorEditor dialog = new();
        dialog.ShowDialog();
    }
}
