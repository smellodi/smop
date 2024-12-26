﻿using Smop.MainApp.Controllers;
using System.ComponentModel;
using System.Windows;

namespace Smop.MainApp.Dialogs;

public partial class PauseEstimatorEditor : Window
{
    public PauseEstimatorEditor()
    {
        InitializeComponent();

        DialogTools.HideWindowButtons(this);

        DataContext = _pauseEstimator;
    }

    // Internal

    PauseEstimator _pauseEstimator = new();

    // UI

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.BindScaleToZoomLevel(sctScale);
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.UnbindScaleToZoomLevel(sctScale);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        _pauseEstimator.Save();

        DialogResult = true;
    }
}
