using Smop.MainApp.Controllers;
using Smop.MainApp.Controllers.HumanTests;
using Smop.MainApp.Dialogs;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Smop.MainApp.Controls;

public partial class HumanTestsSettings : UserControl
{
    public Settings Settings { get; } = new();

    public HumanTestsSettings()
    {
        InitializeComponent();

        var mixComps = Settings.MixtureComponents;
        foreach (var _ in mixComps)
        {
            grdMixtures.RowDefinitions.Add(new RowDefinition());
        }

        var abbrs = OdorDisplayHelper.GetOdorAbbreviations(mixComps[0].ShortOdorNames);
        int i = 1;
        foreach (var abbr in abbrs)
        {
            var lbl = new Label() { Content = abbr };
            Grid.SetRow(lbl, 0);
            Grid.SetColumn(lbl, i++);
            grdMixtures.Children.Add(lbl);
        }

        int row = 1;

        Style valueStyle = (Style)FindResource("MixFlow");

        foreach (var mixComp in mixComps)
        {
            UIElement? header = null;
            if (mixComp.CanBeLoadedFromServer)
            {
                var btn = new Button()
                {
                    Content = mixComps[row - 1].Name,
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                btn.Click += (s, e) =>
                {
                    var pulseSetupText = LoadFromServer();
                    if (!string.IsNullOrEmpty(pulseSetupText))
                    {
                        var pulseSetup = PulseSetup.LoadFromContent(pulseSetupText);
                        if (pulseSetup != null)
                        {
                            LoadMixtureComponentsFromPulseSetup(mixComp, pulseSetup);
                        }
                        else
                        {
                            MsgBox.Error(App.Name, "Failed to load the pulse parameters.");
                        }
                    }
                };
                header = btn;
            }
            else
            {
                header = new Label() { Content = mixComps[row - 1].Name };
            }

            Grid.SetRow(header, row);
            Grid.SetColumn(header, 0);
            grdMixtures.Children.Add(header);

            var column = 1;
            var knownOdors = new KnownOdors();
            foreach (var knownOdor in knownOdors)
            {
                if (mixComp.ShortOdorNames.Contains(knownOdor.ShortKnownName))
                {
                    var txb = new TextBox();
                    txb.Style = valueStyle;
                    Grid.SetRow(txb, row);
                    Grid.SetColumn(txb, column++);
                    grdMixtures.Children.Add(txb);

                    var path = mixComp.GetComponentName(knownOdor.ShortKnownName);
                    var textBinding = new Binding(path)
                    {
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        Source = mixComp
                    };
                    textBinding.ValidationRules.Add(new Utils.RangeRule() { Min = 0, Max = 100, IsInteger = false });
                    txb.SetBinding(TextBox.TextProperty, textBinding);
                }
            }

            row++;
        }
    }

    public void AddOdorChannel(OdorChannel odorChannel)
    {
        Settings.Channels.Add(odorChannel.ID, odorChannel.Name);
    }

    // Internal

    private string LoadFromServer()
    {
        string text = string.Empty;

        try
        {
            GoogleDriveService gdrive = GoogleDriveService.Instance;
            var files = gdrive.Texts;

            if (files.Length > 0)
            {
                var filePicker = new GoogleDriveFilePicker(files);
                if (filePicker.ShowDialog() == true)
                {
                    var id = filePicker.SelectedFile?.Id;
                    if (!string.IsNullOrEmpty(id))
                    {
                        text = gdrive.ReadFileSync(id);
                    }
                }
            }
            else
            {
                MsgBox.Error(App.Name, "Google Drive is unaccessible or has no recipes.");
            }
        }
        catch (ApplicationException)
        {
            MsgBox.Error(App.Name, "Failed to access Google Drive service. Please check your credentials and try again.");
        }
        catch (Exception ex)
        {
            MsgBox.Error(App.Name, $"Unexpected error while accessing Google Drive service: {ex.Message}");
        }

        return text;
    }

    private void LoadMixtureComponentsFromPulseSetup(MixtureComponents mixComp, PulseSetup pulseSetup, int sessionId = 0, int pulseId = 0)
    {
        try
        {
            if (pulseSetup.Sessions.Length <= sessionId || pulseSetup.Sessions[sessionId].Pulses.Count <= pulseId)
            {
                throw new Exception("No pulse data found in the file.");
            }

            var pulse = pulseSetup.Sessions[sessionId].Pulses[pulseId];
            foreach (var channel in pulse.Channels)
            {
                var channelName = Settings.Channels[(OdorDisplay.Device.ID)channel.Id];
                mixComp.SetComponent(channelName, channel.Flow);
            }
        }
        catch (Exception ex)
        {
            MsgBox.Error(App.Name, ex.Message);
        }
    }
}
