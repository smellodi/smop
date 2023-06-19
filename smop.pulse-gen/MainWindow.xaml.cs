using Smop.PulseGen.Logging;
using Smop.PulseGen.Pages;
using Smop.PulseGen.Test;
using Smop.PulseGen.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Smop.PulseGen;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

		var settings = Properties.Settings.Default;

		_connectPage.Next += ConnectPage_Next;
		_setupPage.Next += SetupPage_Next;
        _pulsePage.Next += Page_Next;
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

		string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "X";
		Title += $"   v{version}";
	}

	// Internal

	private readonly Connect _connectPage = new();
	private readonly Setup _setupPage = new();
    private readonly Pulse _pulsePage = new();
    private readonly Finished _finishedPage = new();

	private readonly Storage _storage = Storage.Instance;

	private bool IsInFullScreen => WindowStyle == WindowStyle.None;

	private SavingResult SaveData(bool canCancel)
	{
        var eventLogger = EventLogger.Instance;
        var odorDisplayLogger = OdorDisplayLogger.Instance;
        var smellInspLogger = SmellInspLogger.Instance;
        var ionVisionLogger = IonVisionLogger.Instance;

        /*
		var result = SavingResult.None;

        var timestamp = $"{DateTime.Now:u}";
		LoggerStorage? reference = null;

		if (eventLogger.HasRecords)
		{
			result = eventLogger.SaveTo("events", timestamp, canCancel, null);
			reference = eventLogger.File;
		}

		bool skipOtherLogfile = result == SavingResult.Discard || result == SavingResult.Cancel;

		if (odorDisplayLogger.HasRecords && !skipOtherLogfile)
		{
			result = odorDisplayLogger.SaveTo("od", timestamp, canCancel, reference);
		}
        if (smellInspLogger.HasRecords && !skipOtherLogfile)
        {
            result = smellInspLogger.SaveTo("snt", timestamp, canCancel, reference);
        }
        if (ionVisionLogger.HasRecords && !skipOtherLogfile)
        {
            result = ionVisionLogger.SaveTo("dms", timestamp, canCancel, reference);
        }*/

        var logs = new List<ILog>();
		if (eventLogger.HasRecords) logs.Add(eventLogger);
        if (odorDisplayLogger.HasRecords) logs.Add(odorDisplayLogger);
        if (smellInspLogger.HasRecords) logs.Add(smellInspLogger);
        if (ionVisionLogger.HasRecords) logs.Add(ionVisionLogger);

		var result = Logger.Save(logs.ToArray());

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

	private void ConnectPage_Next(object? sender, EventArgs e)
	{
		Content = _setupPage;
	}

	private void SetupPage_Next(object? sender, PulseSetup setup)
	{
        Content = _pulsePage;
        (_pulsePage as ITest)?.Start(setup);
    }

    private void Page_Next(object? sender, Navigation next)
    {
        if (IsInFullScreen)
        {
            ToggleFullScreen();
        }

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

            Content = _setupPage;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MW] Unrecognized navigation target '{next}'");
        }

        (_pulsePage as ITest)?.Dispose();
    }

	private void FinishedPage_RequestSaving(object? sender, Pages.Finished.RequestSavingArgs e)
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
				"HOME page\n" +
				"F4 - starts Odor Pulses procedure\n\n" +
				"TEST page\n" +
				"F2 - forces the test to finish\n" +
				"F4 - toggles status bar visibility\n\n" +
				"RESULT page\n" +
				"F4 - toggles result visibility\n\n" +
				"Any page\n" +
				"F9 - zooms out\n" +
				"F10 - zooms in\n" +
				"F11 - toggles full screen\n");
		}
		else if (e.Key == Key.OemMinus)
		{
			_storage.ZoomOut();
		}
		else if (e.Key == Key.OemPlus)
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

        (_pulsePage as ITest)?.Dispose();

        await Task.Delay(100);
    }
}
