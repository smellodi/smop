using Smop.MainApp.Controllers;
using System.Windows;

namespace Smop.MainApp.Dialogs;

public partial class PauseEstimatorEditor : Window
{
    public PauseEstimator PauseEstimator { get; } = new();

    public PauseEstimatorEditor()
    {
        InitializeComponent();

        DialogTools.HideWindowButtons(this);
        DialogTools.SetCentralPosition(this);
    }

    // Internal

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
        PauseEstimator.Save();

        DialogResult = true;
    }
}
