using Smop.MainApp.Logging;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Pages;

public partial class Finished : Page, IPage<Navigation>
{
    public class RequestSavingArgs : EventArgs
    {
        public SavingResult Result { get; set; }
        public RequestSavingArgs(SavingResult result)
        {
            Result = result;
        }
    }

    public event EventHandler<Navigation>? Next;
    public event EventHandler<RequestSavingArgs>? RequestSaving;

    public Finished()
    {
        InitializeComponent();

        DataContext = this;
    }

    public void DisableSaving()
    {
        btnSaveData.IsEnabled = false;
    }

    // Internal

    private bool HasDecisionAboutData()
    {
        if (!EventLogger.Instance.HasRecords)
        {
            return true;
        }

        RequestSavingArgs args = new(SavingResult.Cancel);
        RequestSaving?.Invoke(this, args);

        return args.Result != SavingResult.Cancel;
    }


    // UI events

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .UnbindScaleToZoomLevel(sctScale)
            .UnbindContentToZoomLevel(lblZoom)
            .UnbindVisibilityToDebug(lblDebug);
    }

    private void Page_GotFocus(object sender, RoutedEventArgs e)
    {
        btnSaveData.IsEnabled = true;
    }

    private void SaveData_Click(object sender, RoutedEventArgs e)
    {
        RequestSaving?.Invoke(this, new(SavingResult.Cancel));
    }

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        if (HasDecisionAboutData())
        {
            Next?.Invoke(this, Storage.Instance.SetupPage);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (HasDecisionAboutData())
        {
            Next?.Invoke(this, Navigation.Exit);
        }
    }
}
