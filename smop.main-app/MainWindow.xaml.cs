using Smop.Common;
using Smop.MainApp.Dialogs;
using Smop.MainApp.Logging;
using Smop.MainApp.Pages;
using Smop.OdorDisplay.Packets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Smop.MainApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var settings = Properties.Settings.Default;

        _connectPage.Next += Page_Next;
        _setupPage.Next += SetupPage_Next;
        _pulsePage.Next += Page_Next;
        _reproductionPage.Next += Page_Next;
        _humanTestsComparisonPage.Next += Page_Next;
        _humanTestsOneOutPage.Next += Page_Next;
        _humanTestsRatingPage.Next += Page_Next;
        _finishedPage.Next += Page_Next;
        _finishedPage.RequestSaving += FinishedPage_RequestSaving;

        if (settings.MainWindow_IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        if (settings.MainWindow_Width > 0)
        {
            Left = settings.MainWindow_X;
            Top = settings.MainWindow_Y;
            Width = settings.MainWindow_Width;
            Height = settings.MainWindow_Height;
        }

        string version = Utils.Resources.GetVersion();
        Title = $"{App.Name}   v{version}";
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(MainWindow));

    readonly Connect _connectPage = new();
    readonly Setup _setupPage = new();
    readonly Pulse _pulsePage = new();
    readonly Reproduction _reproductionPage = new();
    readonly HumanTestComparison _humanTestsComparisonPage = new();
    readonly HumanTestOneOut _humanTestsOneOutPage = new();
    readonly HumanTestRating _humanTestsRatingPage = new();
    readonly Finished _finishedPage = new();

    readonly Storage _storage = Storage.Instance;

    bool IsInFullScreen => WindowStyle == WindowStyle.None;

    private SavingResult SaveData(bool canCancel)
    {
        var eventLogger = EventLogger.Instance;
        var odorDisplayLogger = OdorDisplayLogger.Instance;
        var smellInspLogger = SmellInspLogger.Instance;
        var ionVisionLogger = IonVisionLogger.Instance;

        var logs = new List<ILog>();
        if (eventLogger.HasRecords) logs.Add(eventLogger);
        if (odorDisplayLogger.HasRecords) logs.Add(odorDisplayLogger);
        if (smellInspLogger.HasRecords) logs.Add(smellInspLogger);
        if (ionVisionLogger.HasRecords) logs.Add(ionVisionLogger);

        var (result, _) = Logger.Save(logs.ToArray());

        if (result == SavingResult.None)
        {
            MsgBox.Warn(Title, "No data to save", MsgBox.Button.OK);
        }
        if (result != SavingResult.Cancel)
        {
            _finishedPage.DisableSaving();
        }

        return result;
    }

    private void ToggleFullScreen()
    {
        if (!IsInFullScreen)
        {
            var settings = Properties.Settings.Default;
            settings.MainWindow_IsMaximized = WindowState == WindowState.Maximized;
            settings.Save();

            Visibility = Visibility.Collapsed;

            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            Visibility = Visibility.Visible;
        }
        else
        {
            WindowState = Properties.Settings.Default.MainWindow_IsMaximized ? WindowState.Maximized : WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
        }
    }

    // Events handlers

    private void SetupPage_Next(object? sender, object? param)
    {
        if (param is Controllers.PulseSetup pulseSetup)
        {
            _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "Navigator", Navigation.Test, _pulsePage.Name));

            Content = _pulsePage;
            _pulsePage.Start(pulseSetup);
        }
        else if (param is Controllers.OdorReproducerController.Config config)
        {
            _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "Navigator", Navigation.Test, _reproductionPage.Name));

            Content = _reproductionPage;
            _reproductionPage.Start(config);
        }
        else if (param is Controllers.HumanTests.Settings humanTestSettings)
        {
            _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "Navigator", Navigation.Test, _humanTestsComparisonPage.Name));

            Content = _humanTestsComparisonPage;
            _humanTestsComparisonPage.Start(humanTestSettings);
            //Content = _humanTestsOneOutPage;
            //_humanTestsOneOutPage.Start(humanTestSettings);
            //Content = _humanTestsRatingPage;
            //_humanTestsRatingPage.Start(humanTestSettings);
        }
        else
        {
            throw new Exception($"Task type '{_storage.TaskType}' is not yet supported");
        }
    }

    private void Page_Next(object? sender, Navigation next)
    {
        if (IsInFullScreen)
        {
            ToggleFullScreen();
        }

        _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "Navigator", next));

        if (next == Navigation.Exit)
        {
            Close();
        }
        else if (next == Navigation.Finished)
        {
            Content = _finishedPage;

            DispatchOnce.Do(0.1, () => Dispatcher.Invoke(SaveData, true));  // let the page to change, then try to save data
        }
        else if (next == Navigation.Setup)
        {
            OdorDisplayLogger.Instance.Clear();
            EventLogger.Instance.Clear();
            IonVisionLogger.Instance.Clear();

            _storage.SetupPage = next;

            var odorDisplayCleanupFile = sender is Connect connectPage ? connectPage.OdorDisplayCleanupFile : null;
            _setupPage.Init(_storage.TaskType, odorDisplayCleanupFile);

            Content = _setupPage;
        }
        else if (next == Navigation.Test)
        {
            if (sender == _humanTestsComparisonPage)
            {
                _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "Navigator", Navigation.Test, _humanTestsOneOutPage.Name));

                Content = _humanTestsOneOutPage;
                _humanTestsOneOutPage.Start(_humanTestsComparisonPage.Settings!);
            }
            else if (sender == _humanTestsOneOutPage)
            {
                _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "Navigator", Navigation.Test, _humanTestsRatingPage.Name));

                Content = _humanTestsRatingPage;
                _humanTestsRatingPage.Start(_humanTestsOneOutPage.Settings!);
            }
        }
        else
        {
            _nlog.Error(LogIO.Text(Utils.Timestamp.Ms, "Navigator", "Error", $"Unrecognized navigation target '{next}'"));
        }

        _pulsePage.Dispose();
    }

    private void FinishedPage_RequestSaving(object? sender, Finished.RequestSavingArgs e)
    {
        var savingResult = SaveData(true);
        e.Result = savingResult;
    }

    // UI events

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Content = _connectPage;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            MsgBox.Notify(Title, "Developer and tester shortcuts:\n\n" +
                "CONNECTION page\n" +
                "F2 - starts simulator\n\n" +
                "SETUP page\n" +
                "F4 - starts the procedure\n\n" +
                "PULSES page\n" +
                "F2 - forces the test to finish\n\n" +
                "Any page\n" +
                "Ctrl + Scroll - zooms UI in/out\n" +
                "Page Up/Down - zooms UI in/out\n" +
                "F11 - toggles full screen\n");
        }
        else if (e.Key == Key.PageDown)
        {
            _storage.ZoomOut();
        }
        else if (e.Key == Key.PageUp)
        {
            _storage.ZoomIn();
        }
        else if (e.Key == Key.F11)
        {
            ToggleFullScreen();
        }
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            if (e.Delta > 0)
            {
                _storage.ZoomIn();
            }
            else
            {
                _storage.ZoomOut();
            }
        }
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = Properties.Settings.Default;
        settings.MainWindow_X = Left;
        settings.MainWindow_Y = Top;
        settings.MainWindow_Width = Width;
        settings.MainWindow_Height = Height;
        settings.MainWindow_IsMaximized = WindowState == WindowState.Maximized;
        settings.Save();

        _pulsePage.Dispose();

        if (OdorDisplay.CommPort.Instance.IsOpen)
        {
            OdorDisplay.CommPort.Instance.Request(new SetMeasurements(SetMeasurements.Command.Stop), out Ack? _, out Response? _);
        }

        NLog.LogManager.Shutdown();

        await Task.Delay(100);
    }
}
