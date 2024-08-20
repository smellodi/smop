using Microsoft.Win32;
using Smop.Common;
using Smop.MainApp.Controllers;
using Smop.MainApp.Utils;
using Smop.MainApp.Utils.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Smop.MainApp.Controls;

public partial class PulseGeneratorSettings : UserControl
{
    public PulseSetup? Setup { get; private set; } = null;

    public event EventHandler? Changed;

    public event EventHandler<OdorChannel>? OdorNameChanging;
    public event EventHandler<OdorChannel>? OdorNameChanged;

    public PulseGeneratorSettings()
    {
        InitializeComponent();
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

        var nameBinding = new Binding(nameof(OdorChannel.Name))
        {
            Source = odorChannel,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        BindingOperations.SetBinding(txbName, TextBox.TextProperty, nameBinding);


        var container = new Grid();
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(40) });
        container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(lblID, 0);
        Grid.SetColumn(txbName, 1);

        container.Children.Add(lblID);
        container.Children.Add(txbName);

        stpOdorChannels.Children.Add(container);
    }
    
    // Internal

    string? _setupFileName = null;

    string _currentInput = "";
    string? _currentSuggestion = null;

    private void ShowOdorNameSuggestions(TextBox txb)
    {
        var input = txb.Text.ToLower();
        var inputLength = input.Length;

        if (input.Length > _currentInput.Length && input != _currentSuggestion)
        {
            _currentSuggestion = OdorChannels.KnownOdorNames.FirstOrDefault(x => x.StartsWith(input));
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

    private void LoadPulseSetup(string filename)
    {
        _setupFileName = null;

        if (string.IsNullOrEmpty(filename))
        {
            DispatchOnce.Do(0.5, () => Dispatcher.Invoke(() => EditPulseSetup_Click(this, new RoutedEventArgs())));
            return;
        }

        if (!File.Exists(filename))
        {
            return;
        }

        Setup = PulseSetup.Load(filename);
        if (Setup == null)
        {
            return;
        }
        else if (chkRandomize.IsChecked == true)
        {
            Setup.Randomize();
        }

        _setupFileName = filename;

        txbSetupFile.Text = _setupFileName.ToFileNameOnly();
        txbSetupFile.ToolTip = _setupFileName;
        txbSetupFile.ScrollToHorizontalOffset(double.MaxValue);

        var settings = Properties.Settings.Default;
        settings.Pulses_SetupFilename = _setupFileName;
        settings.Save();

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // UI

    private void ChoosePulseSetupFile_Click(object? sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;
        var ofd = new OpenFileDialog
        {
            Filter = "Text files|*.txt",
            FileName = Path.GetFileName(settings.Pulses_SetupFilename.Trim()),
            InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(settings.Pulses_SetupFilename.Trim())) ?? AppDomain.CurrentDomain.BaseDirectory
        };

        if (ofd.ShowDialog() ?? false)
        {
            var filename = IoHelper.GetShortestFilePath(ofd.FileName);
            LoadPulseSetup(filename);
        }
    }

    private void EditPulseSetup_Click(object? sender, RoutedEventArgs e)
    {
        var editor = new Dialogs.PulseSetupEditor();

        var settings = Properties.Settings.Default;
        if (string.IsNullOrEmpty(settings.Pulses_SetupFilename.Trim()) || !File.Exists(settings.Pulses_SetupFilename.Trim()))
        {
            settings.Pulses_SetupFilename = Path.Combine("Assets", "pulses", "default.txt");
        }

        editor.Load(settings.Pulses_SetupFilename.Trim());

        if (editor.ShowDialog() == true)
        {
            var filename = editor.Filename ?? settings.Pulses_SetupFilename.Trim();

            var setup = new PulseSetup() { Sessions = editor.Sessions };
            setup.Save(filename);

            LoadPulseSetup(filename);
        }
    }

    private void Randomize_CheckedChanged(object sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;
        settings.Pulses_Randomize = chkRandomize.IsChecked == true;
        settings.Save();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = Properties.Settings.Default;
        chkRandomize.IsChecked = settings.Pulses_Randomize;

        if (Visibility == Visibility.Visible)
        {
            LoadPulseSetup(settings.Pulses_SetupFilename.Trim());
        }
    }
}
