﻿using Smop.Utils;
using System;
using System.Windows;
using System.Windows.Input;
using WPFLocalizeExtension.Engine;

namespace Smop
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var settings = Properties.Settings.Default;

            LocalizeDictionary.Instance.MergedAvailableCultures.RemoveAt(0);
            LocalizeDictionary.Instance.Culture = System.Globalization.CultureInfo.GetCultureInfo(settings.Language);
            LocalizeDictionary.Instance.OutputMissingKeys = true;
            LocalizeDictionary.Instance.MissingKeyEvent += (s, e) => e.MissingKeyResult = $"[MISSING] {e.Key}";

            _connectPage.Next += ConnectPage_Next;
            _homePage.Next += SetupPage_Next;
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
        private readonly Pages.Home _homePage = new();
        private readonly Pages.Finished _finishedPage = new();

        private Tests.ITestNavigator? _testNavigator = null;

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
            Content = _homePage;
        }

        private void SetupPage_Next(object? sender, Tests.Test test)
        {
            _testNavigator = test switch
            {
                //Tests.Test.Threshold => new Tests.ThresholdTest.Navigator(),
                _ => throw new NotImplementedException($"The test '{test}' logic is not implemented yet"),
            };

            _testNavigator.PageDone += Test_PageDone;

            Content = _testNavigator.Start();
        }

        private void FinishedPage_Next(object? sender, bool exit)
        {
            if (exit)
            {
                Close();
            }
            else
            {
                Content = _homePage;

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
                MsgBox.Warn(Title, L10n.T("NoDataToSave"), MsgBox.Button.OK);
            }
        }

        private void Test_PageDone(object? sender, Tests.PageDoneEventArgs e)
        {
            bool finilizeTest = false;

            if (!e.CanContinue)
            {
                Content = _homePage;

                _testNavigator?.Interrupt();
                finilizeTest = true;

                SyncLogger.Instance.Finilize();
                SyncLogger.Instance.Clear();
                FlowLogger.Instance.Clear();
            }
            else
            {
                var page = _testNavigator?.NextPage(e.Data);
                if (page == null)
                {
                    _finishedPage.TestName = _testNavigator?.Name ?? "";

                    Content = _finishedPage;

                    finilizeTest = true;

                    DispatchOnce.Do(0.1, () => Dispatcher.Invoke(SaveLoggingData, true));  // let the page to change, then try to save data
                }
                else
                {
                    Content = page;
                }
            }

            if (finilizeTest)
            {
                _testNavigator!.PageDone -= Test_PageDone;
                _testNavigator.Dispose();
                _testNavigator = null;

                if (IsInFullScreen)
                {
                    ToggleFullScreen();
                }

                GC.Collect();
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
                    "F3 - shows single-pulse-creation dialog\n" +
                    "F4 - starts Odor Pulses procedure\n\n" +
                    "TEST page\n" +
                    "F2 - forces the test to finish\n" +
                    "F4 - toggles status bar visibility\n\n" +
                    "TLX and SUS pages\n" +
                    "F2 - simulates questionnaire choises\n\n" +
                    "RESULT page\n" +
                    "F4 - toggles result visibility\n\n" +
                    "Any page\n" +
                    "F5 - starts the pump\n" +
                    "F6 - stops the pump\n" +
                    "F7 - clanks with the fake trigger\n" +
                    "F9 - zooms out\n" +
                    "F10 - zooms in\n" +
                    "F11 - toggles full screen\n" +
                    "F12 - shows COM, data and debug window\n");
            }
            else if (e.Key == Key.F2)
            {
                _testNavigator?.Emulate(Tests.EmulationCommand.ForceToFinishWithResult);
            }
            else if (e.Key == Key.F5)
            {
                OdorDisplay.MFC.Instance.Pump = OdorDisplay.DeviceState.On;
            }
            else if (e.Key == Key.F6)
            {
                OdorDisplay.MFC.Instance.Pump = OdorDisplay.DeviceState.Off;
            }
            else if (e.Key == Key.F7)
            {
                //Comm.MFC.Instance.SetValve(Comm.DeviceOutputID.NoiseValve, Comm.DeviceState.On, 0.5);
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OdorDisplay.PID.Instance.Stop();

            if (_testNavigator != null)
            {
                _testNavigator.Interrupt();

                if (_testNavigator.HasStarted)
                {
                    SaveLoggingData(false);
                }
            }

            var settings = Properties.Settings.Default;
            settings.MainWindow_X = Left;
            settings.MainWindow_Y = Top;
            settings.MainWindow_Width = Width;
            settings.MainWindow_Height = Height;
            settings.MainWindow_IsMaximized = WindowState == WindowState.Maximized;
            settings.Save();

            System.Threading.Thread.Sleep(100);

            Application.Current.Shutdown();
        }
    }
}
