﻿using Microsoft.Win32;
using Smop.Common;
using Smop.MainApp.Controllers;
using Smop.MainApp.Utils;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class PulseGeneratorSettings : UserControl
{
    public PulseSetup? Setup { get; private set; } = null;

    public event EventHandler? Changed;

    public PulseGeneratorSettings()
    {
        InitializeComponent();
    }

    // Internal

    string? _setupFileName = null;

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

        txbSetupFile.Text = _setupFileName;
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
            Filter = "Any file|*",
            FileName = Path.GetFileName(settings.Pulses_SetupFilename.Trim()),
            InitialDirectory = Path.GetDirectoryName(settings.Pulses_SetupFilename.Trim()) ?? AppDomain.CurrentDomain.BaseDirectory
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
            settings.Pulses_SetupFilename = "Properties/pulse-setup.txt";
        }

        editor.Load(settings.Pulses_SetupFilename.Trim());

        if (editor.ShowDialog() == true)
        {
            var filename = editor.Filename ?? settings.Pulses_SetupFilename.Trim();

            var setup = new PulseSetup() { Sessions = editor.Sessions };
            setup.Save(filename);

            LoadPulseSetup(filename);
        }

        return;
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
