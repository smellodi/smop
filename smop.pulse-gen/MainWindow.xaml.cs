using System;
using System.Windows;
using System.Windows.Input;
using Smop.PulseGen.Utils;
using Smop.PulseGen.Logging;
using Smop.PulseGen.Test;
using System.Threading.Tasks;

namespace Smop.PulseGen;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

		var settings = Properties.Settings.Default;

		_connectPage.Next += ConnectPage_Next;
		_setupPage.Next += SetupPage_Next;
        _pulsePage.Next += PulsePage_Next;
        _finishedPage.Next += FinishedPage_Next;
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

	private readonly Pages.Connect _connectPage = new();
	private readonly Pages.Setup _setupPage = new();
    private readonly Pages.Pulse _pulsePage = new();
    private readonly Pages.Finished _finishedPage = new();

	private readonly Storage _storage = Storage.Instance;

	private bool IsInFullScreen => WindowStyle == WindowStyle.None;

	private SavingResult? SaveLoggingData(bool canCancel)
	{
		SavingResult? result = null;

		FlowLogger flowLogger = FlowLogger.Instance;
		SyncLogger syncLogger = SyncLogger.Instance;
		syncLogger.Finilize();

		var savingResult = SavingResult.None;
		var timestamp = $"{DateTime.Now:u}";
		LoggerStorage? reference = null;

		bool noData = true;

		if (flowLogger.HasTestRecords)
		{
			noData = false;

			savingResult = flowLogger.SaveTo("events", timestamp, canCancel, null);
			if (savingResult != SavingResult.Cancel)
			{
				_finishedPage.DisableSaving();
			}

			reference = flowLogger.File;
			result = savingResult;
		}

		bool skipOtherLogfile = savingResult == SavingResult.Discard || savingResult == SavingResult.Cancel;

		if (syncLogger.HasRecords && !skipOtherLogfile)
		{
			noData = false;

			savingResult = syncLogger.SaveTo("data", timestamp, canCancel, reference);
			if (savingResult != SavingResult.Cancel)
			{
				_finishedPage.DisableSaving();
			}

			result = savingResult;
		}

		if (noData)
		{
			result = SavingResult.None;
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

	private void SetupPage_Next(object? sender, PulseSetup e)
	{
        Content = _pulsePage;
        (_pulsePage as ITest)?.Start(e);
    }

    private void PulsePage_Next(object? sender, bool e)
    {
        if (IsInFullScreen)
        {
            ToggleFullScreen();
        }

        if (e)
		{
            Content = _finishedPage;
            DispatchOnce.Do(0.1, () => Dispatcher.Invoke(SaveLoggingData, true));  // let the page to change, then try to save data
        }
        else
        {
            SyncLogger.Instance.Clear();
            FlowLogger.Instance.Clear();

            Content = _setupPage;
        }

        (_pulsePage as ITest)?.Dispose();
    }

    private void FinishedPage_Next(object? sender, bool exit)
	{
		if (exit)
		{
			Close();
		}
		else
		{
			Content = _setupPage;

			FlowLogger.Instance.Clear();
			SyncLogger.Instance.Clear();
		}
	}

	private void FinishedPage_RequestSaving(object? sender, Pages.Finished.RequestSavingArgs e)
	{
		var savingResult = SaveLoggingData(true);
		e.Result = savingResult ?? e.Result;

		if (savingResult == null)
		{
			_finishedPage.DisableSaving();
			MsgBox.Warn(Title, "No data to save", MsgBox.Button.OK);
		}
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

		Application.Current.Shutdown();

        await Task.Delay(100);

        (_pulsePage as ITest)?.Dispose();

        await Task.Delay(200);
    }
}
